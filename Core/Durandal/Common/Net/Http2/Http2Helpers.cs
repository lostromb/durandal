using Durandal.Common.Cache;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2.Frames;
using Durandal.Common.Net.Http2.HPack;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2
{
    public static class Http2Helpers
    {
        // Cache used for common HTTP headers from Title-Case to lower-case so
        // we don't have to constantly reallocate / convert headers each time.
        private static readonly IReadThroughCache<string, string> LOWERCASE_HEADER_CACHE = 
            new MRUCache<string, string>((input) => input.ToLowerInvariant(), 100);
        
        public static List<HeaderField> ConvertResponseHeadersToHPack(
           IHttpHeaders headers,
           int responseCode)
        {
            List<HeaderField> hpackHeaders = new List<HeaderField>(headers.KeyCount + 1);
            hpackHeaders.Add(new HeaderField()
            {
                Name = Http2Constants.PSEUDOHEADER_STATUS_CODE,
                Sensitive = false,
                Value = responseCode.ToString(CultureInfo.InvariantCulture)
            });

            foreach (var header in headers)
            {
                bool isSensitive = string.Equals(header.Key, HttpConstants.HEADER_KEY_AUTHORIZATION, StringComparison.OrdinalIgnoreCase);
                foreach (var headerValue in header.Value)
                {
                    hpackHeaders.Add(new HeaderField()
                    {
                        Name = LOWERCASE_HEADER_CACHE.GetCache(header.Key),
                        Sensitive = isSensitive,
                        Value = headerValue
                    });
                }
            }

            return hpackHeaders;
        }

        public static List<HeaderField> ConvertResponseTrailersToHPack(IHttpHeaders trailers)
        {
            List<HeaderField> hpackHeaders = new List<HeaderField>(trailers.KeyCount);

            foreach (var trailer in trailers)
            {
                bool isSensitive = string.Equals(trailer.Key, HttpConstants.HEADER_KEY_AUTHORIZATION, StringComparison.OrdinalIgnoreCase);
                foreach (var headerValue in trailer.Value)
                {
                    hpackHeaders.Add(new HeaderField()
                    {
                        Name = LOWERCASE_HEADER_CACHE.GetCache(trailer.Key),
                        Sensitive = isSensitive,
                        Value = headerValue
                    });
                }
            }

            return hpackHeaders;
        }

        /// <summary>
        /// Converts regular HTTP headers to HTTP2 wire form, including pseudoheaders.
        /// </summary>
        /// <param name="headers">The headers to convert. May be null if you just want the pseudoheaders returned.</param>
        /// <param name="requestMethod"></param>
        /// <param name="requestFile"></param>
        /// <param name="remoteAuthority"></param>
        /// <param name="scheme"></param>
        /// <returns></returns>
        public static List<HeaderField> ConvertRequestHeadersToHPack(
           IHttpHeaders headers,
           string requestMethod,
           string requestFile,
           string remoteAuthority,
           string scheme)
        {
            int keyCount = 4;
            if (headers != null)
            {
                keyCount += headers.KeyCount;
            }

            List<HeaderField> hpackHeaders = new List<HeaderField>(keyCount + 4);
            hpackHeaders.Add(new HeaderField()
            {
                Name = Http2Constants.PSEUDOHEADER_METHOD,
                Sensitive = false,
                Value = requestMethod
            });
            hpackHeaders.Add(new HeaderField()
            {
                Name = Http2Constants.PSEUDOHEADER_PATH,
                Sensitive = false,
                Value = requestFile
            });
            hpackHeaders.Add(new HeaderField()
            {
                Name = Http2Constants.PSEUDOHEADER_AUTHORITY,
                Sensitive = false,
                Value = remoteAuthority
            });
            hpackHeaders.Add(new HeaderField()
            {
                Name = Http2Constants.PSEUDOHEADER_SCHEME,
                Sensitive = false,
                Value = scheme
            });

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    if (string.Equals(header.Key, HttpConstants.HEADER_KEY_UPGRADE, StringComparison.OrdinalIgnoreCase))
                    {
                        // Convert the Upgrade header into :protocol for websocket upgrades
                        hpackHeaders.Add(new HeaderField()
                        {
                            Name = Http2Constants.PSEUDOHEADER_PROTOCOL,
                            Sensitive = false,
                            Value = header.Value.Single(),
                        });

                        continue;
                    }

                    bool isSensitive = string.Equals(header.Key, HttpConstants.HEADER_KEY_AUTHORIZATION, StringComparison.OrdinalIgnoreCase);
                    foreach (var headerValue in header.Value)
                    {
                        hpackHeaders.Add(new HeaderField()
                        {
                            Name = LOWERCASE_HEADER_CACHE.GetCache(header.Key),
                            Sensitive = isSensitive,
                            Value = headerValue
                        });
                    }
                }
            }

            return hpackHeaders;
        }

        public static IEnumerable<Http2Frame> ConvertResponseHeadersToHeaderBlock(
            IHttpHeaders responseHeaders,
            int responseCode,
            int responseStreamId,
            Http2Settings currentSettings,
            HPackEncoder headerEncoder,
            bool endOfStream)
        {
            List<HeaderField> hpackHeaders = ConvertResponseHeadersToHPack(responseHeaders, responseCode);

            using (PooledBuffer<byte> scratchBuffer = BufferPool<byte>.Rent(currentSettings.MaxFrameSize))
            {
                // Compress the headers
                bool first = true;
                bool endHeaders;
                do
                {
                    HPackEncoder.Result hpackResult = headerEncoder.EncodeInto(scratchBuffer.AsArraySegment, hpackHeaders);
                    if (hpackResult.FieldCount == 0 && hpackHeaders.Count != 0)
                    {
                        // If we reached this point, it means we encountered a single header that was so large it
                        // couldn't fit inside of an entire frame.
                        // Retrying it in a continuation frame
                        // - is not valid since it's not allowed to send an empty fragment
                        // - won't to better, since buffer size is the same
                        int longestHeaderLength = 0;
                        string longestHeaderKey = string.Empty;
                        foreach (var header in hpackHeaders)
                        {
                            if (header.Value.Length > longestHeaderLength)
                            {
                                longestHeaderLength = header.Value.Length;
                                longestHeaderKey = header.Name;
                            }
                        }

                        throw new Http2ProtocolException("Encountered a header field \"" + longestHeaderKey + "\" whose length exceeds a single HTTP2 frame. The header cannot be sent.");
                    }

                    PooledBuffer<byte> headerPayload = BufferPool<byte>.Rent(hpackResult.UsedBytes);
                    ArrayExtensions.MemCopy(scratchBuffer.Buffer, 0, headerPayload.Buffer, 0, hpackResult.UsedBytes);
                    endHeaders = hpackResult.FieldCount == hpackHeaders.Count;

                    Http2Frame outgoingFrame;
                    if (first)
                    {
                        outgoingFrame =
                            Http2HeadersFrame.CreateOutgoing(
                                headerPayload,
                                responseStreamId,
                                endHeaders,
                                endStream: endOfStream);
                    }
                    else
                    {
                        outgoingFrame =
                            Http2ContinuationFrame.CreateOutgoing(
                                headerPayload,
                                responseStreamId,
                                endHeaders);
                    }

                    yield return outgoingFrame;

                    if (!endHeaders)
                    {
                        // OPT this is a bit ineffecient but it's a rare enough case anyways...
                        hpackHeaders.RemoveRange(0, hpackResult.FieldCount);
                    }
                } while (!endHeaders);
            }
        }

        public static IEnumerable<Http2Frame> ConvertResponseTrailersToTrailerBlock(
            HttpHeaders responseTrailers,
            int responseStreamId,
            Http2Settings currentSettings,
            HPackEncoder headerEncoder)
        {
            List<HeaderField> hpackHeaders = ConvertResponseTrailersToHPack(responseTrailers);

            using (PooledBuffer<byte> scratchBuffer = BufferPool<byte>.Rent(currentSettings.MaxFrameSize))
            {
                // Compress the trailers
                bool first = true;
                bool endTrailers;
                do
                {
                    HPackEncoder.Result hpackResult = headerEncoder.EncodeInto(scratchBuffer.AsArraySegment, hpackHeaders);
                    if (hpackResult.FieldCount == 0 && hpackHeaders.Count != 0)
                    {
                        // If we reached this point, it means we encountered a single header that was so large it
                        // couldn't fit inside of an entire frame.
                        // Retrying it in a continuation frame
                        // - is not valid since it's not allowed to send an empty fragment
                        // - won't to better, since buffer size is the same
                        int longestHeaderLength = 0;
                        string longestHeaderKey = string.Empty;
                        foreach (var header in hpackHeaders)
                        {
                            if (header.Value.Length > longestHeaderLength)
                            {
                                longestHeaderLength = header.Value.Length;
                                longestHeaderKey = header.Name;
                            }
                        }

                        throw new Http2ProtocolException("Encountered a trailer field \"" + longestHeaderKey + "\" whose length exceeds a single HTTP2 frame. The trailer cannot be sent.");
                    }

                    PooledBuffer<byte> trailerPayload = BufferPool<byte>.Rent(hpackResult.UsedBytes);
                    ArrayExtensions.MemCopy(scratchBuffer.Buffer, 0, trailerPayload.Buffer, 0, hpackResult.UsedBytes);
                    endTrailers = hpackResult.FieldCount == hpackHeaders.Count;

                    Http2Frame outgoingFrame;
                    if (first)
                    {
                        outgoingFrame =
                            Http2HeadersFrame.CreateOutgoing(
                                trailerPayload,
                                responseStreamId,
                                endTrailers,
                                endStream: true);
                    }
                    else
                    {
                        outgoingFrame =
                            Http2ContinuationFrame.CreateOutgoing(
                                trailerPayload,
                                responseStreamId,
                                endTrailers);
                    }

                    yield return outgoingFrame;

                    if (!endTrailers)
                    {
                        // OPT this is a bit ineffecient but it's a rare enough case anyways...
                        hpackHeaders.RemoveRange(0, hpackResult.FieldCount);
                    }
                } while (!endTrailers);
            }
        }

        public static IEnumerable<Http2Frame> ConvertRequestHeadersToHeaderBlock(
            IHttpHeaders requestHeaders,
            string requestMethod,
            string requestFile,
            string remoteAuthority,
            string scheme,
            int responseStreamId,
            Http2Settings currentSettings,
            HPackEncoder headerEncoder,
            bool endOfStream)
        {
            List<HeaderField> hpackHeaders = ConvertRequestHeadersToHPack(requestHeaders, requestMethod, requestFile, remoteAuthority, scheme);

            using (PooledBuffer<byte> scratchBuffer = BufferPool<byte>.Rent(currentSettings.MaxFrameSize))
            {
                // Compress the headers
                bool first = true;
                bool endHeaders;
                do
                {
                    HPackEncoder.Result hpackResult = headerEncoder.EncodeInto(scratchBuffer.AsArraySegment, hpackHeaders);
                    if (hpackResult.FieldCount == 0 && hpackHeaders.Count != 0)
                    {
                        // If we reached this point, it means we encountered a single header that was so large it
                        // couldn't fit inside of an entire frame.
                        // Retrying it in a continuation frame
                        // - is not valid since it's not allowed to send an empty fragment
                        // - won't to better, since buffer size is the same
                        int longestHeaderLength = 0;
                        string longestHeaderKey = string.Empty;
                        foreach (var header in hpackHeaders)
                        {
                            if (header.Value.Length > longestHeaderLength)
                            {
                                longestHeaderLength = header.Value.Length;
                                longestHeaderKey = header.Name;
                            }
                        }

                        throw new Http2ProtocolException("Encountered a header field \"" + longestHeaderKey + "\" whose length exceeds a single HTTP2 frame. The header cannot be sent.");
                    }

                    PooledBuffer<byte> headerPayload = BufferPool<byte>.Rent(hpackResult.UsedBytes);
                    ArrayExtensions.MemCopy(scratchBuffer.Buffer, 0, headerPayload.Buffer, 0, hpackResult.UsedBytes);
                    endHeaders = hpackResult.FieldCount == hpackHeaders.Count;

                    Http2Frame outgoingFrame;
                    if (first)
                    {
                        outgoingFrame =
                            Http2HeadersFrame.CreateOutgoing(
                                headerPayload,
                                responseStreamId,
                                endHeaders,
                                endStream: endOfStream);
                    }
                    else
                    {
                        outgoingFrame =
                            Http2ContinuationFrame.CreateOutgoing(
                                headerPayload,
                                responseStreamId,
                                endHeaders);
                    }

                    yield return outgoingFrame;

                    if (!endHeaders)
                    {
                        hpackHeaders.RemoveRange(0, hpackResult.FieldCount);
                    }
                } while (!endHeaders);
            }
        }

        public static IEnumerable<Http2Frame> ConvertRequestHeadersToPushPromiseHeaderBlock(
            HttpHeaders requestHeaders,
            string requestMethod,
            string requestFile,
            string localAuthority,
            string scheme,
            int responseStreamId,
            int promisedStreamId,
            Http2Settings currentSettings,
            HPackEncoder headerEncoder,
            bool endOfStream)
        {
            List<HeaderField> hpackHeaders = ConvertRequestHeadersToHPack(requestHeaders, requestMethod, requestFile, localAuthority, scheme);

            using (PooledBuffer<byte> scratchBuffer = BufferPool<byte>.Rent(currentSettings.MaxFrameSize))
            {
                // Compress the headers
                bool first = true;
                bool endHeaders;
                do
                {
                    HPackEncoder.Result hpackResult = headerEncoder.EncodeInto(scratchBuffer.AsArraySegment, hpackHeaders);
                    if (hpackResult.FieldCount == 0 && hpackHeaders.Count != 0)
                    {
                        int longestHeaderLength = 0;
                        string longestHeaderKey = string.Empty;
                        foreach (var header in hpackHeaders)
                        {
                            if (header.Value.Length > longestHeaderLength)
                            {
                                longestHeaderLength = header.Value.Length;
                                longestHeaderKey = header.Name;
                            }
                        }

                        throw new Http2ProtocolException("Encountered a header field \"" + longestHeaderKey + "\" whose length exceeds a single HTTP2 frame. The header cannot be sent.");
                    }

                    PooledBuffer<byte> headerPayload = BufferPool<byte>.Rent(hpackResult.UsedBytes);
                    ArrayExtensions.MemCopy(scratchBuffer.Buffer, 0, headerPayload.Buffer, 0, hpackResult.UsedBytes);
                    endHeaders = hpackResult.FieldCount == hpackHeaders.Count;

                    Http2Frame outgoingFrame;
                    if (first)
                    {
                        outgoingFrame =
                            Http2PushPromiseFrame.CreateOutgoing(
                                headerPayload,
                                responseStreamId,
                                promisedStreamId,
                                endHeaders);
                    }
                    else
                    {
                        outgoingFrame =
                            Http2ContinuationFrame.CreateOutgoing(
                                headerPayload,
                                responseStreamId,
                                endHeaders);
                    }

                    yield return outgoingFrame;

                    if (!endHeaders)
                    {
                        hpackHeaders.RemoveRange(0, hpackResult.FieldCount);
                    }
                } while (!endHeaders);
            }
        }

        public static Http2Settings ParseSettings(PooledBuffer<byte> payload, bool isServer)
        {
            if (payload == null)
            {
                return isServer ? Http2Settings.Default() : Http2Settings.ServerDefault();
            }

            return ParseSettings(payload.Buffer, 0, payload.Length , isServer);
        }

        public static Http2Settings ParseSettings(byte[] binary, int offset, int count, bool isServer)
        {
            Http2Settings returnVal = isServer ? Http2Settings.Default() : Http2Settings.ServerDefault();
            if (binary != null && count > 0)
            {
                for (int idx = 0; idx < count; idx += 6)
                {
                    ushort settingKey = BinaryHelpers.ByteArrayToUInt16BigEndian(binary, offset + idx);
                    uint settingValue = BinaryHelpers.ByteArrayToUInt32BigEndian(binary, offset + idx + 2);

                    switch ((Http2SettingName)settingKey)
                    {
                        case Http2SettingName.HeaderTableSize:
                            returnVal.HeaderTableSize = (int)settingValue;
                            break;
                        case Http2SettingName.EnablePush:
                            returnVal.EnablePush = settingValue != 0;
                            break;
                        case Http2SettingName.MaxConcurrentStreams:
                            returnVal.MaxConcurrentStreams = (int)settingValue;
                            break;
                        case Http2SettingName.InitialWindowSize:
                            returnVal.InitialWindowSize = (int)settingValue;
                            break;
                        case Http2SettingName.MaxFrameSize:
                            returnVal.MaxFrameSize = (int)settingValue;
                            break;
                        case Http2SettingName.MaxHeaderListSize:
                            returnVal.MaxHeaderListSize = (int)settingValue;
                            break;
                        case Http2SettingName.EnableConnectProtocol:
                            returnVal.EnableConnectProtocol = settingValue != 0;
                            break;
                        default: // Gracefully handle unknown or future use settings fields such as websocket support
                            break;
                    }
                }
            }

            return returnVal;
        }

        public static PooledBuffer<byte> SerializeSettings(Http2Settings settings, bool serializeAllSettings)
        {
            settings = settings.AssertNonNull(nameof(settings));

            // Only send the settings that are different from the default.
            int numChangedSettings;

            if (serializeAllSettings)
            {
                numChangedSettings = 7;
            }
            else
            {
                numChangedSettings =
                    ((settings.HeaderTableSize != Http2Constants.DEFAULT_HEADER_TABLE_SIZE) ? 1 : 0) +
                    ((!settings.EnablePush) ? 1 : 0) +
                    ((settings.MaxConcurrentStreams != Http2Constants.DEFAULT_MAX_CONCURRENT_STREAMS) ? 1 : 0) +
                    ((settings.InitialWindowSize != Http2Constants.DEFAULT_INITIAL_WINDOW_SIZE) ? 1 : 0) +
                    ((settings.MaxFrameSize != Http2Constants.DEFAULT_MAX_FRAME_SIZE) ? 1 : 0) +
                    ((settings.MaxHeaderListSize != Http2Constants.DEFAULT_MAX_HEADER_LIST_SIZE) ? 1 : 0) +
                    ((settings.EnableConnectProtocol != false) ? 1 : 0);
            }

            PooledBuffer<byte> returnVal = BufferPool<byte>.Rent((sizeof(ushort) + sizeof(uint)) * numChangedSettings);
            int idx = 0;
            if (serializeAllSettings || settings.HeaderTableSize != Http2Constants.DEFAULT_HEADER_TABLE_SIZE)
            {
                BinaryHelpers.UInt16ToByteArrayBigEndian((ushort)Http2SettingName.HeaderTableSize, returnVal.Buffer, idx);
                BinaryHelpers.UInt32ToByteArrayBigEndian((uint)settings.HeaderTableSize, returnVal.Buffer, idx + 2);
                idx += 6;
            }
            if (serializeAllSettings || !settings.EnablePush)
            {
                BinaryHelpers.UInt16ToByteArrayBigEndian((ushort)Http2SettingName.EnablePush, returnVal.Buffer, idx);
                BinaryHelpers.UInt32ToByteArrayBigEndian(settings.EnablePush ? 1U : 0, returnVal.Buffer, idx + 2);
                idx += 6;
            }
            if (serializeAllSettings || settings.MaxConcurrentStreams != Http2Constants.DEFAULT_MAX_CONCURRENT_STREAMS)
            {
                BinaryHelpers.UInt16ToByteArrayBigEndian((ushort)Http2SettingName.MaxConcurrentStreams, returnVal.Buffer, idx);
                BinaryHelpers.UInt32ToByteArrayBigEndian((uint)settings.MaxConcurrentStreams, returnVal.Buffer, idx + 2);
                idx += 6;
            }
            if (serializeAllSettings || settings.InitialWindowSize != Http2Constants.DEFAULT_INITIAL_WINDOW_SIZE)
            {
                BinaryHelpers.UInt16ToByteArrayBigEndian((ushort)Http2SettingName.InitialWindowSize, returnVal.Buffer, idx);
                BinaryHelpers.UInt32ToByteArrayBigEndian((uint)settings.InitialWindowSize, returnVal.Buffer, idx + 2);
                idx += 6;
            }
            if (serializeAllSettings || settings.MaxFrameSize != Http2Constants.DEFAULT_MAX_FRAME_SIZE)
            {
                BinaryHelpers.UInt16ToByteArrayBigEndian((ushort)Http2SettingName.MaxFrameSize, returnVal.Buffer, idx);
                BinaryHelpers.UInt32ToByteArrayBigEndian((uint)settings.MaxFrameSize, returnVal.Buffer, idx + 2);
                idx += 6;
            }
            if (serializeAllSettings || settings.MaxHeaderListSize != Http2Constants.DEFAULT_MAX_HEADER_LIST_SIZE)
            {
                BinaryHelpers.UInt16ToByteArrayBigEndian((ushort)Http2SettingName.MaxHeaderListSize, returnVal.Buffer, idx);
                BinaryHelpers.UInt32ToByteArrayBigEndian((uint)settings.MaxHeaderListSize, returnVal.Buffer, idx + 2);
                idx += 6;
            }
            if (serializeAllSettings || settings.EnableConnectProtocol != false)
            {
                BinaryHelpers.UInt16ToByteArrayBigEndian((ushort)Http2SettingName.EnableConnectProtocol, returnVal.Buffer, idx);
                BinaryHelpers.UInt32ToByteArrayBigEndian(settings.EnableConnectProtocol ? 1U : 0, returnVal.Buffer, idx + 2);
                idx += 6;
            }

            return returnVal;
        }

        public static string SerializeSettingsToBase64(Http2Settings settings)
        {
            PooledBuffer<byte> binary = SerializeSettings(settings, serializeAllSettings: true);
            return BinaryHelpers.EncodeUrlSafeBase64(binary.Buffer, 0, binary.Length);
        }

        public static bool TryParseSettingsFromBase64(string base64String, bool isServer, out Http2Settings settings)
        {
            try
            {
                using (PooledBuffer<byte> binary = BinaryHelpers.DecodeUrlSafeBase64(base64String))
                {
                    settings = ParseSettings(binary.Buffer, 0, binary.Length, isServer);
                    return true;
                }
            }
            catch (Exception)
            {
                settings = null;
                return false;
            }
        }
    }
}
