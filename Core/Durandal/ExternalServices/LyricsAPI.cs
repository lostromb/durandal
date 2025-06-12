using Durandal.Common.Compression.ZLib;
using Durandal.Common.IO;
using Durandal.Common.IO.Hashing;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.ExternalServices
{
    /// <summary>
    /// Song lyrics API based on an implementation used for the Winamp Lyrics Plugin, www.lyricsplugin.com, with its hash validation scheme reverse-engineered.
    /// </summary>
    public class LyricsAPI
    {
        private static readonly DateTimeOffset EPOCH_ORIGIN = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly Regex LYRIC_MATCHER_REGEX = new Regex("<div id=\\\"lyrics\\\">([\\w\\W]+?)</div>");
        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        
        public LyricsAPI(IHttpClientFactory httpFactory, ILogger logger)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _httpClient = httpFactory.AssertNonNull(nameof(httpFactory)).CreateHttpClient("www.lyricsplugin.com", 443, true, _logger.Clone("LyricsApiHttp"));
        }

        /// <summary>
        /// Attempts to fetch song lyrics for the given artist + track title.
        /// </summary>
        /// <param name="songArtist">The song artist</param>
        /// <param name="songTitle">The song title</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>Lyrics in HTML format, or null if nothing was found</returns>
        public async Task<string> FetchLyrics(string songArtist, string songTitle, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            string sid = Guid.NewGuid().ToString("N").ToLowerInvariant();
            long unixTimestamp = GetCurrentUnixTime();
            string hashChecksumA = BinaryHelpers.ToHexString(MD5.HashString(unixTimestamp.ToString())).ToLowerInvariant();
            string mysteryInput = "8516ac99dc60603295de7bdb6a153530"; // This appears to be a hardcoded secret? It's not derived from sid, bc, tc, or epoch time
            string hashChecksumB = BinaryHelpers.ToHexString(MD5.HashString(mysteryInput)).ToLowerInvariant();
            StringBuilder pidInputBuilder = new StringBuilder();
            pidInputBuilder.Append(hashChecksumA.Substring(0, 4));
            pidInputBuilder.Append("|");
            pidInputBuilder.Append(hashChecksumB.Substring(0, 16));
            pidInputBuilder.Append("|");
            pidInputBuilder.Append(hashChecksumA.Substring(8, 4));
            pidInputBuilder.Append("|");
            pidInputBuilder.Append(songArtist);
            pidInputBuilder.Append("|");
            pidInputBuilder.Append(hashChecksumA.Substring(16, 4));
            pidInputBuilder.Append("|");
            pidInputBuilder.Append(songTitle);
            pidInputBuilder.Append("|");
            pidInputBuilder.Append(hashChecksumA.Substring(20, 4));
            pidInputBuilder.Append("|");
            pidInputBuilder.Append(sid);
            pidInputBuilder.Append("|");
            pidInputBuilder.Append(hashChecksumA.Substring(12, 4));
            pidInputBuilder.Append("|");
            pidInputBuilder.Append(hashChecksumB.Substring(16, 16));
            pidInputBuilder.Append("|");
            pidInputBuilder.Append(hashChecksumA.Substring(4, 4));
            string pidInput = pidInputBuilder.ToString();
            string pid = BinaryHelpers.ToHexString(MD5.HashString(pidInput)).ToLowerInvariant();
            int bc = 1850749; // not sure how these are generated
            int tc = 16777215;

            HttpRequest lyricRequest = HttpRequest.CreateOutgoing("/plugin/0.4/winamp/plugin.php", "POST");
            lyricRequest.RequestHeaders["Accept"] = "image/gif, image/jpeg, image/pjpeg, application/x-ms-application, application/xaml+xml, application/x-ms-xbap, */*";
            lyricRequest.RequestHeaders["Accept-Language"] = "en-US,en;q=0.5";
            lyricRequest.RequestHeaders["User-Agent"] = "Lyrics Plugin/0.4 (Winamp build)";
            lyricRequest.RequestHeaders["Content-Type"] = "application/x-www-form-urlencoded";
            lyricRequest.RequestHeaders["Accept-Encoding"] = "gzip";
            lyricRequest.RequestHeaders["Pragma"] = "no-cache";

            IDictionary<string, string> postParameters = new Dictionary<string, string>();
            postParameters["a"] = songArtist;
            postParameters["t"] = songTitle;
            postParameters["i"] = unixTimestamp.ToString();
            postParameters["pid"] = pid;
            postParameters["sid"] = sid;
            postParameters["bc"] = bc.ToString();
            postParameters["tc"] = tc.ToString();
            lyricRequest.SetContent(postParameters);

            using (HttpResponse response = await _httpClient.SendRequestAsync(lyricRequest, cancelToken, realTime))
            {
                if (response == null)
                {
                    _logger.Log("Got null response from lyric service", LogLevel.Err);
                    return null;
                }
                else if (response.ResponseCode == 200)
                {
                    // Decompress gzip response
                    using (Stream compressedResponseBody = response.ReadContentAsStream())
                    using (MemoryStream decompressedBody = new MemoryStream())
                    {
                        using (GZipStream decompressor = new GZipStream(compressedResponseBody, CompressionMode.Decompress, leaveOpen: true))
                        using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
                        {
                            int bytesRead = await decompressor.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length);
                            while (bytesRead > 0)
                            {
                                await decompressedBody.WriteAsync(scratch.Buffer, 0, bytesRead).ConfigureAwait(false);
                                bytesRead = await decompressor.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length);
                            }
                        }

                        byte[] decodedBytes = decompressedBody.ToArray();
                        string body = Encoding.UTF8.GetString(decodedBytes, 0, decodedBytes.Length);
                        await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                        return ExtractLyricsFromHtml(body);
                    }
                }
                else
                {
                    _logger.Log("Got error response from lyric service: " + response.ResponseCode, LogLevel.Err);
                    string body = await response.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                    _logger.Log(body, LogLevel.Err);
                    await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    return null;
                }
            }
        }

        private string ExtractLyricsFromHtml(string html)
        {
            string regexLyrics = StringUtils.RegexRip(LYRIC_MATCHER_REGEX, html, 1, _logger);
            return regexLyrics;
        }

        private static long GetCurrentUnixTime()
        {
            return (DateTimeOffset.UtcNow - EPOCH_ORIGIN).Ticks / 10000000L;
        }
    }
}
