using Durandal.API;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio;
using System.IO;
using Durandal.Common.Cache;
using Durandal.Common.Tasks;
using System.Globalization;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Net.Http
{
    public static class HttpHelpers
    {
        // Object pools for capture pattern matchers, commonly reused to parse delimiters in HTTP messages
        internal static readonly LockFreeCache<CapturePatternMatcher> NEWLINE_MATCHERS = new LockFreeCache<CapturePatternMatcher>(16);
        internal static readonly LockFreeCache<CapturePatternMatcher> DOUBLE_NEWLINE_MATCHERS = new LockFreeCache<CapturePatternMatcher>(16);

        /// <summary>
        /// The text encoding to use for HTTP protocol strings such as status codes, headers, etc.
        /// The RFC spec dictates 7-bit ASCII, but in most cases we can safely fall back to UTF8.
        /// </summary>
        public static readonly Encoding HTTP_TEXT_ENCODING = StringUtils.ASCII_ENCODING;

        /// <summary>
        /// Common cache of strings used for headers, request paths, method strings like "GET", "POST", things that we assume will be mostly
        /// constant and which we want to avoid constant reallocations for. Be sure not to use this for sensitive data such as authentication
        /// parameters or url string parameters that may be tokens or keys.
        /// </summary>
        public static readonly IReadThroughCache<ByteArraySegment, string> HTTP_COMMON_STRING_CACHE = new MFUStringCache(HTTP_TEXT_ENCODING, 1024);

        /// <summary>
        /// Performs URL-decoding only for characters that are not a valid url path, i.e. spaces
        /// </summary>
        /// <param name="requestFile"></param>
        /// <returns></returns>
        public static string UrlPathDecode(string requestFile)
        {
            if (requestFile == null)
            {
                return null;
            }

            return WebUtility.UrlDecode(requestFile);
        }

        /// <summary>
        /// Performs URL-encoding only for characters that are not a valid url path, i.e. spaces
        /// </summary>
        /// <param name="requestFile"></param>
        /// <returns></returns>
        public static string UrlPathEncode(string requestFile)
        {
            if (requestFile == null)
            {
                return null;
            }

            return WebUtility.UrlEncode(requestFile).Replace("%2F", "/");
        }

        /// <summary>
        /// Attempts to parse a relative URL (beginning with "/")
        /// into base path, query params (get parameters / URL parameters), and fragment (if any).
        /// </summary>
        /// <param name="relativeUrl">The URL to be parsed</param>
        /// <param name="basePath">The parsed base path of the URL, which is every part of the path prior to query parameters (if present)</param>
        /// <param name="queryParams">The parsed dictionary of query params - guaranteed non-null</param>
        /// <param name="fragment">The parsed fragment from the URL - guaranteed non-null</param>
        /// <param name="treatQueryParamNamesAsCaseSensitive">If true, create a case sensitive dictionary for query parameter keys</param>
        /// <returns>True if parsing succeeded</returns>
        public static bool TryParseRelativeUrl(
            string relativeUrl,
            out string basePath,
            out HttpFormParameters queryParams,
            out string fragment,
            bool treatQueryParamNamesAsCaseSensitive = false)
        {
            if (string.IsNullOrEmpty(relativeUrl))
            {
                // For empty strings, just assume base path.
                // This could happen if we are trimming the relative URL from something like "https://www.com"
                // and the trailing slash is omitted.
                basePath = "/";
                queryParams = new HttpFormParameters(0);
                fragment = string.Empty;
            }
            else if (relativeUrl.StartsWith("/"))
            {
                int startOfQueryParams = relativeUrl.IndexOf('?');
                int fragmentSplitter = relativeUrl.LastIndexOf('#');
                int endOfQueryParams;
                if (fragmentSplitter > 0)
                {
                    fragment = relativeUrl.Substring(fragmentSplitter + 1);
                    endOfQueryParams = fragmentSplitter;
                }
                else
                {
                    fragment = string.Empty;
                    endOfQueryParams = relativeUrl.Length;
                }

                if (startOfQueryParams > 0)
                {
                    // Full URI with query params and potentially a fragment
                    // FIXME how do fragment and parameters interact? I'm assuming fragment comes last
                    // Count the number of '=' to estimate the query param count
                    int probableParamCount = 0;
                    int equalsSign = startOfQueryParams;
                    do
                    {
                        probableParamCount++;
                        equalsSign = relativeUrl.IndexOf('=', equalsSign + 1);
                    } while (equalsSign > 0);

                    basePath = relativeUrl.Substring(0, startOfQueryParams);
                    queryParams = new HttpFormParameters(probableParamCount, treatQueryParamNamesAsCaseSensitive);

                    // Parse query params
                    int startOfThisParam = startOfQueryParams + 1;
                    bool moreParams = false;
                    while (startOfThisParam < endOfQueryParams)
                    {
                        while (startOfThisParam < endOfQueryParams && relativeUrl[startOfThisParam] == '&')
                        {
                            startOfThisParam++; // This loop handles weird (degenerate?) cases where there can be multiple &&& in a row.
                        }

                        int endOfThisParam = relativeUrl.IndexOf('&', startOfThisParam, endOfQueryParams - startOfThisParam);
                        if (endOfThisParam <= 0)
                        {
                            endOfThisParam = endOfQueryParams;
                            moreParams = false;
                        }
                        else
                        {
                            moreParams = true;
                        }

                        equalsSign = relativeUrl.IndexOf('=', startOfThisParam + 1, endOfThisParam - startOfThisParam - 1);
                        if (equalsSign < 0)
                        {
                            // Technically, URI query parameters do not require value.
                            // In this case, only capture the key, and mark the value as empty string
                            string queryKey = relativeUrl.Substring(startOfThisParam, endOfThisParam - startOfThisParam);
                            if (string.IsNullOrEmpty(queryKey))
                            {
                                // Empty string as query param - invalid
                                return false;
                            }

                            queryKey = WebUtility.UrlDecode(queryKey);
                            queryParams.Add(queryKey, string.Empty);
                        }
                        else
                        {
                            string queryKey = relativeUrl.Substring(startOfThisParam, equalsSign - startOfThisParam);
                            if (string.IsNullOrEmpty(queryKey))
                            {
                                // Empty string as query param - invalid
                                return false;
                            }

                            queryKey = WebUtility.UrlDecode(queryKey);
                            string queryValue = relativeUrl.Substring(equalsSign + 1, endOfThisParam - equalsSign - 1);
                            // Empty string as query value is technically valid, as it's been seen in the wild
                            // examples: ?param1=&param2=value2
                            if (!string.IsNullOrEmpty(queryValue))
                            {
                                queryValue = WebUtility.UrlDecode(queryValue);
                            }

                            queryParams.Add(queryKey, queryValue);
                        }

                        startOfThisParam = endOfThisParam + 1;
                        if (moreParams && startOfThisParam >= endOfQueryParams)
                        {
                            // We are expecting more parameters, but there's no more input.
                            // This is a parse error because it means there's a trailing '&'
                            return false;
                        }
                    }
                }
                else if (fragmentSplitter > 0)
                {
                    // url with fragment but no params
                    queryParams = new HttpFormParameters(0);
                    basePath = relativeUrl.Substring(0, fragmentSplitter);
                }
                else
                {
                    queryParams = new HttpFormParameters(0);
                    basePath = relativeUrl;
                }
            }
            else
            {
                // It's a non-null value but it doesn't start with '/'.
                // This usually indicates a proxy-style request where the request URL will be like "www.server.com:80".
                // We don't handle this format at this level of parsing; it needs to be detected elsewhere
                basePath = relativeUrl;
                queryParams = new HttpFormParameters(0);
                fragment = string.Empty;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Given a socket to which an HTTP request or response is ready to read, this method will read all of the
        /// data in the request/response header block, up until the first byte of the payload (if any), and then
        /// return the parsed header block as a single (internally pooled) buffer.
        /// </summary>
        /// <param name="socket">The socket to read from, which we expect to contain HTTP header data</param>
        /// <param name="logger">A logger</param>
        /// <param name="cancelToken">A cancel token for the read</param>
        /// <param name="realTime">A definition of wallclock time</param>
        /// <returns>A tuple representing the continguous HTTP data block along with its length, or null if an error occurred.</returns>
        public static async Task<Tuple<PooledBuffer<byte>, int>> ReadHttpHeaderBlock(ISocket socket, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            socket = socket.AssertNonNull(nameof(socket));
            logger = logger ?? NullLogger.Singleton;
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            // Assume any request header longer than 256Kb is some kind of malicious DoS attack or something
            // We're also relying on the fact that this is the largest buffer size available to us in the buffer pool
            const int MAX_HEADER_BLOCK_SIZE = 262144;
            PooledBuffer<byte> returnVal = BufferPool<byte>.Rent(MAX_HEADER_BLOCK_SIZE);
            int totalBytesRead = 0;
            int delimiterIdx = 0;

            CapturePatternMatcher doubleNewlineMatcher = DOUBLE_NEWLINE_MATCHERS.TryDequeue();
            if (doubleNewlineMatcher == null)
            {
                doubleNewlineMatcher = new CapturePatternMatcher(HttpConstants.CAPTURE_PATTERN_RNRN);
            }
            else
            {
                doubleNewlineMatcher.Reset();
            }

            try
            {
                while (totalBytesRead < MAX_HEADER_BLOCK_SIZE)
                {
                    int maxReadSize = returnVal.Length - totalBytesRead;
#if DEBUG
                    maxReadSize = 64; // try and expose bugs that could happen when the request spans multiple buffer reads
#endif
                    int bytesRead = await socket.ReadAnyAsync(returnVal.Buffer, totalBytesRead, maxReadSize, cancelToken, realTime).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        totalBytesRead += bytesRead;
                        for (; delimiterIdx < totalBytesRead; delimiterIdx++)
                        {
                            if (doubleNewlineMatcher.Match(returnVal.Buffer[delimiterIdx]))
                            {
                                int bytesOfBodyWeReadInto = totalBytesRead - (delimiterIdx + 1);
                                if (bytesOfBodyWeReadInto > 0)
                                {
                                    socket.Unread(returnVal.Buffer, delimiterIdx + 1, bytesOfBodyWeReadInto);
                                }

                                return new Tuple<PooledBuffer<byte>, int>(returnVal, delimiterIdx + 1);
                            }
                        }
                    }
                    else
                    {
                        if (totalBytesRead == 0)
                        {
                            logger.Log("Remote HTTP client closed socket before sending any data", LogLevel.Err);
                        }
                        else
                        {
                            logger.Log("End of stream while parsing HTTP headers (incomplete request?)", LogLevel.Err);
                        }

                        returnVal.Dispose();
                        return null;
                    }
                }

                logger.Log("Reached maximum buffer size while reading HTTP headers; assuming they are malformed or malicious", LogLevel.Err);
                returnVal.Dispose();
                return null;
            }
            finally
            {
                DOUBLE_NEWLINE_MATCHERS.TryEnqueue(doubleNewlineMatcher);
            }
        }

        /// <summary>
        /// Does a poll on a socket and tentatively reads the first line, assuming it to be an HTTP request. If so,
        /// this method will return the parsed version of the incoming request without actaully consuming any data
        /// from the socket.
        /// </summary>
        /// <param name="socket">The socket to read from</param>
        /// <param name="logger">A logger</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The parsed HTTP version of the incoming request, or null if no valid HTTP request was found</returns>
        public static async Task<HttpVersion> ParseHttpVersionFromRequest(ISocket socket, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // This value dictates the max length of the file path this server can parse, so make it count
            const int MAX_REQUEST_PATH = 4096;
            const int SCRATCH_BUFFER_SIZE = MAX_REQUEST_PATH + 32;
            using (PooledBuffer<byte> scratchBuffer = BufferPool<byte>.Rent(SCRATCH_BUFFER_SIZE))
            {
                int bytesInScratchBuf = 0;
                int versionMatchIter = 0;
                CapturePatternMatcher httpVersionMatcher = new CapturePatternMatcher(HttpConstants.CAPTURE_PATTERN_HTTP_VERSION_PREFIX);
                while (bytesInScratchBuf < SCRATCH_BUFFER_SIZE)
                {
                    int bytesRead = await socket.ReadAnyAsync(scratchBuffer.Buffer, bytesInScratchBuf, SCRATCH_BUFFER_SIZE - bytesInScratchBuf, cancelToken, realTime).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        bytesInScratchBuf += bytesRead;
                        for (; versionMatchIter < bytesInScratchBuf - 4; versionMatchIter++)
                        {
                            if (httpVersionMatcher.Match(scratchBuffer.Buffer[versionMatchIter]))
                            {
                                int majorVersion = scratchBuffer.Buffer[versionMatchIter + 1] - '0';
                                int minorVersion = scratchBuffer.Buffer[versionMatchIter + 3] - '0';
                                socket.Unread(scratchBuffer.Buffer, 0, bytesInScratchBuf);
                                return HttpVersion.ParseHttpVersion(majorVersion, minorVersion);
                            }
                        }
                    }
                    else
                    {
                        // logger.Log("No bytes read while parsing HTTP version (Socket closed?)", LogLevel.Vrb);
                        break;
                    }
                }

                if (bytesInScratchBuf > 0)
                {
                    socket.Unread(scratchBuffer.Buffer, 0, bytesInScratchBuf);
                    logger.Log("Received socket data, but was unable to parse HTTP version from incoming request", LogLevel.Err);
                }

                return null;
            }
        }

        /// <summary>
        /// Given a buffer of data containing at least the first line of an HTTP request e.g. "GET / HTTP/1.1\r\n",
        /// parse out the verb, request path, protocol version, and index of the next line.
        /// </summary>
        /// <param name="headerDataBuffer">The buffer containing HTTP response data</param>
        /// <param name="headerBufferLength">The total number of bytes in the buffer</param>
        /// <param name="httpMethod">The parsed HTTP method / verb</param>
        /// <param name="encodedRequestPath">The parsed request path as it appears on the wire</param>
        /// <param name="actualProtocolVersion">The parsed HTTP protocol version</param>
        /// <param name="indexOfFirstHeaderLine">The index of the first byte of the second line</param>
        /// <param name="stringCache">An optional string cache for reducing allocations on common HTTP strings.</param>
        public static void ParseHttpRequestLine(
            byte[] headerDataBuffer,
            int headerBufferLength,
            out string httpMethod,
            out string encodedRequestPath,
            out HttpVersion actualProtocolVersion,
            out int indexOfFirstHeaderLine,
            IReadThroughCache<ByteArraySegment, string> stringCache = null)
        {
            CapturePatternMatcher matcher = NEWLINE_MATCHERS.TryDequeue();
            if (matcher == null)
            {
                matcher = new CapturePatternMatcher(HttpConstants.CAPTURE_PATTERN_RN);
            }
            else
            {
                matcher.Reset();
            }

            try
            {
                int newlineIdx = matcher.Find(headerDataBuffer, 0, headerBufferLength);

                if (newlineIdx < 0)
                {
                    throw new FormatException("Could not parse HTTP request line: No linebreaks found");
                }

                int oldSpaceIdx = 0;
                int spaceIdx = headerDataBuffer.IndexOf((byte)' ', 0, newlineIdx);
                if (spaceIdx < 0)
                {
                    throw new FormatException("Could not parse HTTP request line: No space after HTTP method");
                }

                httpMethod = (stringCache != null) ?
                    stringCache.GetCache(new ByteArraySegment(headerDataBuffer, oldSpaceIdx, spaceIdx)) :
                    HTTP_TEXT_ENCODING.GetString(headerDataBuffer, oldSpaceIdx, spaceIdx);

                oldSpaceIdx = spaceIdx + 1;
                spaceIdx = headerDataBuffer.IndexOf((byte)' ', oldSpaceIdx, newlineIdx - oldSpaceIdx);
                if (spaceIdx < 0)
                {
                    throw new FormatException("Could not parse HTTP request line: No space after request path");
                }

                // Use the string cache for URL paths only if they are short. Potentially there could also be
                // secret get parameters in the URL string as well, but we will assume anything less than 16 bytes long
                // is not a secret.
                ByteArraySegment requestPathSegment = new ByteArraySegment(headerDataBuffer, oldSpaceIdx, spaceIdx - oldSpaceIdx);
                bool useCacheForRequestPath = stringCache != null && requestPathSegment.Count < 16;
                encodedRequestPath = useCacheForRequestPath ?
                    stringCache.GetCache(requestPathSegment) :
                    HTTP_TEXT_ENCODING.GetString(requestPathSegment.Array, requestPathSegment.Offset, requestPathSegment.Count);

                // Parse HTTP version
                if (spaceIdx + 9 < newlineIdx)
                {
                    throw new FormatException("Could not parse HTTP request line: Malformed HTTP version");
                }

                if (!ArrayExtensions.ArrayEquals(
                    headerDataBuffer,
                    spaceIdx,
                    HttpConstants.CAPTURE_PATTERN_HTTP_VERSION_PREFIX,
                    0,
                    6))
                {
                    throw new FormatException("Could not parse HTTP request line: Missing HTTP/ version declaration");
                }

                if (headerDataBuffer[spaceIdx + 6] < 0x30 || headerDataBuffer[spaceIdx + 6] > 0x39 ||
                    headerDataBuffer[spaceIdx + 8] < 0x30 || headerDataBuffer[spaceIdx + 8] > 0x39)
                {
                    throw new FormatException("Could not parse HTTP request line: Unknown HTTP version digits");
                }

                // Pull out the raw ASCII digits from the request line and interpret them as digits
                actualProtocolVersion = HttpVersion.ParseHttpVersion(headerDataBuffer[spaceIdx + 6] - 0x30, headerDataBuffer[spaceIdx + 8] - 0x30);

                indexOfFirstHeaderLine = newlineIdx + 2;
            }
            finally
            {
                NEWLINE_MATCHERS.TryEnqueue(matcher);
            }
        }

        /// <summary>
        /// Given a buffer of data containing at least the first line of an HTTP response e.g. "HTTP/1.1 200 OK\r\n",
        /// parse out the protocol version, response code, response message, and index of the next line.
        /// </summary>
        /// <param name="headerDataBuffer">The buffer containing HTTP response data</param>
        /// <param name="headerBufferLength">The total number of bytes in the buffer</param>
        /// <param name="responseProtocol">The parsed HTTP protocol version</param>
        /// <param name="responseCode">The parsed numeric response code</param>
        /// <param name="responseMessage">The parsed HTTP response message</param>
        /// <param name="indexOfFirstHeaderLine">The index of the first byte of the second line</param>
        /// <param name="stringCache">An optional string cache for reducing allocations on common HTTP strings.</param>
        public static void ParseHttpResponseLine(
           byte[] headerDataBuffer,
           int headerBufferLength,
           out HttpVersion responseProtocol,
           out int responseCode,
           out string responseMessage,
           out int indexOfFirstHeaderLine,
           IReadThroughCache<ByteArraySegment, string> stringCache = null)
        {
            CapturePatternMatcher matcher = NEWLINE_MATCHERS.TryDequeue();
            if (matcher == null)
            {
                matcher = new CapturePatternMatcher(HttpConstants.CAPTURE_PATTERN_RN);
            }
            else
            {
                matcher.Reset();
            }

            try
            {
                int newlineIdx = matcher.Find(headerDataBuffer, 0, headerBufferLength);

                if (newlineIdx < 0)
                {
                    throw new FormatException("Could not parse HTTP response line: No linebreaks found");
                }

                int oldSpaceIdx = 0;
                int spaceIdx = headerDataBuffer.IndexOf((byte)' ', 0, newlineIdx);
                if (spaceIdx < 0)
                {
                    throw new FormatException("Could not parse HTTP response line: No space after HTTP method");
                }

                if (spaceIdx != 8)
                {
                    throw new FormatException("Could not parse HTTP response line: invalid HTTP protocol line");
                }

                responseProtocol = HttpVersion.ParseHttpVersion((char)headerDataBuffer[5], (char)headerDataBuffer[7]);

                oldSpaceIdx = spaceIdx + 1;
                spaceIdx = headerDataBuffer.IndexOf((byte)' ', oldSpaceIdx, newlineIdx - oldSpaceIdx);
                if (spaceIdx < 0)
                {
                    throw new FormatException("Could not parse HTTP response line: No space after response code");
                }

                string stringResponseCode = (stringCache == null) ?
                    HTTP_TEXT_ENCODING.GetString(headerDataBuffer, oldSpaceIdx, spaceIdx - oldSpaceIdx) :
                    stringCache.GetCache(new ByteArraySegment(headerDataBuffer, oldSpaceIdx, spaceIdx - oldSpaceIdx));

                if (!int.TryParse(stringResponseCode, out responseCode))
                {
                    throw new FormatException("Could not parse HTTP response code: " + stringResponseCode);
                }

                if (stringCache == null)
                {
                    responseMessage = HTTP_TEXT_ENCODING.GetString(headerDataBuffer, spaceIdx + 1, newlineIdx - spaceIdx - 1);
                }
                else
                {
                    responseMessage = stringCache.GetCache(new ByteArraySegment(headerDataBuffer, spaceIdx + 1, newlineIdx - spaceIdx - 1));
                }

                indexOfFirstHeaderLine = newlineIdx + 2;
            }
            finally
            {
                NEWLINE_MATCHERS.TryEnqueue(matcher);
            }
        }

        /// <summary>
        /// Given a block of static data containing HTTP headers, parse them into a structured HttpHeaders
        /// object. This method assumes that the entire header field fits into a single buffer of about 256Kb, which might possibly
        /// have security or performance implications.
        /// </summary>
        /// <param name="requestBuffer">The buffer containing HTTP socket data</param>
        /// <param name="requestBufferLength">The total number of bytes in the buffer</param>
        /// <param name="indexOfFirstHeaderLine">The index of the first byte in the buffer where HTTP headers begin</param>
        /// <param name="endOfHeadersIdx">The index of the byte immediately following the \r\n\r\n sequence which terminates the headers.</param>
        /// <param name="stringCache">An optional cache for optimizing string fetches and reducing allocations for common HTTP header keys and values</param>
        /// <returns>The parsed HTTP headers</returns>
        public static HttpHeaders ParseHttpHeaders(
            byte[] requestBuffer,
            int requestBufferLength,
            int indexOfFirstHeaderLine,
            out int endOfHeadersIdx,
            IReadThroughCache<ByteArraySegment, string> stringCache = null)
        {
            HttpHeaders returnVal = new HttpHeaders();
            CapturePatternMatcher matcher = NEWLINE_MATCHERS.TryDequeue();
            if (matcher == null)
            {
                matcher = new CapturePatternMatcher(HttpConstants.CAPTURE_PATTERN_RN);
            }

            try
            {
                int newlineIdx = indexOfFirstHeaderLine;
                int oldNewlineIdx = newlineIdx;
                endOfHeadersIdx = indexOfFirstHeaderLine + 4;
                while (newlineIdx >= 0 && newlineIdx < requestBufferLength - 3)
                {
                    matcher.Reset();
                    newlineIdx = matcher.Find(requestBuffer, oldNewlineIdx, requestBufferLength - oldNewlineIdx);
                    if (newlineIdx >= 0 && newlineIdx > oldNewlineIdx + 2)
                    {
                        endOfHeadersIdx = newlineIdx + 4;
                        // Got a header line. Find out its bounds
                        int headerKeySeparator = requestBuffer.IndexOf((byte)':', oldNewlineIdx, newlineIdx - oldNewlineIdx);

                        // Trim whitespace from beginning of header value
                        int headerValueBegin = headerKeySeparator + 1;
                        while ((requestBuffer[headerValueBegin] == (byte)' ' ||
                            requestBuffer[headerValueBegin] == (byte)'\t') && headerValueBegin < newlineIdx)
                        {
                            headerValueBegin++;
                        }

                        // Trim whitespace from the end of header value
                        int headerValueEnd = newlineIdx - 1;
                        while ((requestBuffer[headerValueEnd] == (byte)' ' ||
                            requestBuffer[headerValueEnd] == (byte)'\t') && headerValueEnd > headerValueBegin)
                        {
                            headerValueEnd--;
                        }

                        headerValueEnd += 1; // need to offset by 1 because of how boundaries work
                        if (headerKeySeparator > 0 && headerValueBegin <= newlineIdx)
                        {
                            ByteArraySegment spanForHeaderKey = new ByteArraySegment(requestBuffer, oldNewlineIdx, headerKeySeparator - oldNewlineIdx);
                            ByteArraySegment spanForHeaderValue = new ByteArraySegment(requestBuffer, headerValueBegin, headerValueEnd - headerValueBegin);
                            string headerKey;
                            if (stringCache != null)
                            {
                                headerKey = stringCache.GetCache(spanForHeaderKey);
                            }
                            else
                            {
                                headerKey = HTTP_TEXT_ENCODING.GetString(spanForHeaderKey.Array, spanForHeaderKey.Offset, spanForHeaderKey.Count);
                            }

                            if (spanForHeaderValue.Count == 0)
                            {
                                // Assuming I am reading RFC 9110 § 5.5 correctly, field content is allowed to be an empty string.
                                returnVal.Add(headerKey, string.Empty);
                            }
                            else
                            {
                                // Make sure we don't store things like auth tokens in the string cache
                                bool useCacheForHeaderValue = stringCache != null &&
                                    spanForHeaderValue.Count < 128 &&
                                    !string.Equals(HttpConstants.HEADER_KEY_AUTHORIZATION, headerKey);
                                string headerValue = useCacheForHeaderValue ?
                                    stringCache.GetCache(spanForHeaderValue) :
                                    HTTP_TEXT_ENCODING.GetString(spanForHeaderValue.Array, spanForHeaderValue.Offset, spanForHeaderValue.Count);
                                returnVal.Add(headerKey, headerValue);
                            }
                        }
                    }

                    oldNewlineIdx = newlineIdx + 2;
                }
            }
            finally
            {
                NEWLINE_MATCHERS.TryEnqueue(matcher);
            }

            return returnVal;
        }

        /// <summary>
        /// Parses an HTTP verb string as a <see cref="System.Net.Http.HttpMethod" /> enum value.
        /// </summary>
        /// <param name="verb">The HTTP verb string</param>
        /// <returns>The parsed enum, or throws an exception if the verb is not recognized</returns>
        public static HttpMethod ParseHttpVerb(string verb)
        {
            verb = verb.AssertNonNullOrEmpty(nameof(verb));
            if (string.Equals(HttpConstants.HTTP_VERB_GET, verb, StringComparison.OrdinalIgnoreCase))
            {
                return System.Net.Http.HttpMethod.Get;
            }
            else if (string.Equals(HttpConstants.HTTP_VERB_POST, verb, StringComparison.OrdinalIgnoreCase))
            {
                return System.Net.Http.HttpMethod.Post;
            }
            else if (string.Equals(HttpConstants.HTTP_VERB_DELETE, verb, StringComparison.OrdinalIgnoreCase))
            {
                return System.Net.Http.HttpMethod.Delete;
            }
            else if (string.Equals(HttpConstants.HTTP_VERB_HEAD, verb, StringComparison.OrdinalIgnoreCase))
            {
                return System.Net.Http.HttpMethod.Head;
            }
            else if (string.Equals(HttpConstants.HTTP_VERB_OPTIONS, verb, StringComparison.OrdinalIgnoreCase))
            {
                return System.Net.Http.HttpMethod.Options;
            }
            else if (string.Equals(HttpConstants.HTTP_VERB_PUT, verb, StringComparison.OrdinalIgnoreCase))
            {
                return System.Net.Http.HttpMethod.Put;
            }
            else if (string.Equals(HttpConstants.HTTP_VERB_TRACE, verb, StringComparison.OrdinalIgnoreCase))
            {
                return System.Net.Http.HttpMethod.Trace;
            }
#if NETCOREAPP
            else if (string.Equals(HttpConstants.HTTP_VERB_CONNECT, verb, StringComparison.OrdinalIgnoreCase))
            {
                return System.Net.Http.HttpMethod.Connect;
            }
#endif

            throw new ArgumentException("Unknown HTTP verb \"" + verb + "\"");
        }

        /// <summary>
        /// Interprets a block of raw binary data as an HTTP multipart form data payload, and attempts to parse it as a dictionary of key-value pairs.
        /// </summary>
        /// <param name="headers"></param>
        /// <param name="payloadData"></param>
        /// <returns></returns>
        public static HttpFormParameters GetFormDataFromPayload(IHttpHeaders headers, ArraySegment<byte> payloadData)
        {
            // OPT this method is terribly inefficient
            HttpFormParameters returnVal = new HttpFormParameters();
            string contentTypeHeader = headers[HttpConstants.HEADER_KEY_CONTENT_TYPE];
            if (contentTypeHeader == null ||
                !contentTypeHeader.StartsWith(HttpConstants.MIME_TYPE_FORMDATA, StringComparison.OrdinalIgnoreCase))
            {
                return returnVal;
            }

            if (payloadData == null ||
                payloadData.Count == 0)
            {
                return returnVal;
            }

            string bigString = StringUtils.UTF8_WITHOUT_BOM.GetString(payloadData.Array, payloadData.Offset, payloadData.Count);
            string[] parts = bigString.Split('&');
            foreach (string part in parts)
            {
                string[] keyValue = part.Split('=');
                if (keyValue.Length == 2)
                {
                    string key = WebUtility.UrlDecode(keyValue[0]);
                    string val = WebUtility.UrlDecode(keyValue[1]);
                    returnVal.Add(key, val);
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Attempts to read an incoming HTTP request from a socket connection.
        /// This does not read the entire content body, rather it reads the initial headers
        /// and then returns an object that has a handle into the socket for reading
        /// the rest of the data.
        /// </summary>
        /// <param name="socket">The socket to read from</param>
        /// <param name="expectedProtocolVersion">the HTTP protocol version of the endpoint receiving the request</param>
        /// <param name="logger">A tracing logger</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">An implementation of real time</param>
        /// <returns>The parsed HTTP request.</returns>
        /// <exception cref="FormatException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static async Task<HttpRequest> ReadRequestFromSocket(
            ISocket socket,
            HttpVersion expectedProtocolVersion,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            // Read the block of data that includes the request line and headers
            Tuple<PooledBuffer<byte>, int> requestDataBlock = await ReadHttpHeaderBlock(socket, logger, cancelToken, realTime).ConfigureAwait(false);

            if (requestDataBlock == null)
            {
                throw new IOException("Could not read HTTP request from socket");
            }

            using (requestDataBlock.Item1)
            {
                string httpMethod;
                string encodedRequestPath;
                int indexOfFirstHeaderLine;
                HttpVersion actualProtocolVersion;
                ParseHttpRequestLine(
                    requestDataBlock.Item1.Buffer,
                    requestDataBlock.Item2,
                    out httpMethod,
                    out encodedRequestPath,
                    out actualProtocolVersion,
                    out indexOfFirstHeaderLine,
                    HTTP_COMMON_STRING_CACHE);

                int endOfHeaders;
                HttpHeaders parsedHeaders = ParseHttpHeaders(
                    requestDataBlock.Item1.Buffer,
                    requestDataBlock.Item2,
                    indexOfFirstHeaderLine,
                    out endOfHeaders,
                    HTTP_COMMON_STRING_CACHE);

                // Determine if the request has a content length or chunked transfer encoding, that will dictate what kind of stream we create to read it.
                // It's technically legal (?) to have both, in which case we default to chunked transfer anyways
                HttpContentStream contentStream;
                string probeHeaderValue;
                long? declaredContentLength = null;
                if (parsedHeaders.TryGetValue(HttpConstants.HEADER_KEY_CONTENT_LENGTH, out probeHeaderValue))
                {
                    long parsedContentLength;
                    if (!long.TryParse(probeHeaderValue, out parsedContentLength))
                    {
                        throw new FormatException(string.Format("Could not parse Content-Length header from HTTP request: Value was \"{0}\"", probeHeaderValue));
                    }

                    declaredContentLength = parsedContentLength;
                }

                if (parsedHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING))
                {
                    if (parsedHeaders.ContainsValue(
                        HttpConstants.HEADER_KEY_TRANSFER_ENCODING,
                        HttpConstants.HEADER_VALUE_TRANSFER_ENCODING_CHUNKED,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (actualProtocolVersion == HttpVersion.HTTP_1_0 || 
                            expectedProtocolVersion == HttpVersion.HTTP_1_0)
                        {
                            throw new NotSupportedException("Incoming HTTP request uses chunked transfer encoding when server specifies it is only HTTP/1.0 compatible");
                        }

                        contentStream = new HttpChunkedContentStream(new WeakPointer<ISocket>(socket), logger.Clone("HttpContentStream"), declaredContentLength, false);
                    }
                    else
                    {
                        throw new NotSupportedException(string.Format("This server does not understand HTTP request transfer encoding \"{0}\"", probeHeaderValue));
                    }
                }
                else if (declaredContentLength.HasValue)
                {
                    contentStream = new HttpFixedContentStream(
                        new WeakPointer<ISocket>(socket),
                        declaredContentLength.Value);
                }
                else
                {
                    // Even if there is no content, we still create a content stream for regularity.
                    // We need to make sure this stream is fully read before finishing the response,
                    // otherwise the socket will be in an inconsistent state.
                    contentStream = new HttpFixedContentStream(
                        new WeakPointer<ISocket>(socket),
                        fixedContentLength: 0);
                }

                string parsedRequestFile;
                string parsedFragment;
                HttpFormParameters parsedGetParams;
                if (!encodedRequestPath.StartsWith("/"))
                {
                    // It's a proxy-style URL like "www.server.com:80"
                    // Allow parsing these on incoming socket requests, assuming we're going
                    // to do some custom socket proxy stuff later on that won't be supported on other backends.
                    parsedRequestFile = encodedRequestPath;
                    parsedFragment = string.Empty;
                    parsedGetParams = new HttpFormParameters(0);
                }
                else if (!TryParseRelativeUrl(
                    encodedRequestPath,
                    out parsedRequestFile,
                    out parsedGetParams,
                    out parsedFragment,
                    treatQueryParamNamesAsCaseSensitive: false))
                {
                    throw new FormatException(string.Format("Cannot parse URL {0}", encodedRequestPath));
                }

                HttpRequest returnVal = HttpRequest.CreateIncoming(
                    parsedHeaders,
                    parsedRequestFile,
                    httpMethod,
                    socket.RemoteEndpointString,
                    parsedGetParams,
                    parsedFragment,
                    contentStream,
                    actualProtocolVersion);

                // Did the client specify "Expect: 100-continue"?
                // If so, write that response to the wire right now
                if (parsedHeaders.TryGetValue(HttpConstants.HEADER_KEY_EXPECT, out probeHeaderValue) &&
                    string.Equals(probeHeaderValue, HttpConstants.HEADER_VALUE_EXPECT_100_CONTINUE, StringComparison.OrdinalIgnoreCase))
                {
                    if (expectedProtocolVersion == HttpVersion.HTTP_1_0)
                    {
                        throw new NotSupportedException("Incoming HTTP request sent Expect: 100-Continue when server specifies it is only HTTP/1.0 compatible");
                    }

                    await socket.WriteAsync(HttpConstants.HTTP_100_CONTINUE_RESPONSE, 0, HttpConstants.HTTP_100_CONTINUE_RESPONSE.Length, cancelToken, realTime).ConfigureAwait(false);
                    //await HttpHelpers.WriteResponseToSocket(
                    //    HttpResponse.ContinueResponse(),
                    //    CurrentProtocolVersion,
                    //    _socket.Value,
                    //    cancelToken,
                    //    realTime,
                    //    _logger,
                    //    connectionDescriptionProducer: () =>
                    //        string.Format("{0} {1} {2}",
                    //            _socket.Value.RemoteEndpointString,
                    //            HttpRequest.RequestMethod,
                    //            HttpRequest.RequestFile));
                }

                return returnVal;
            }
        }

        /// <summary>
        /// Reads an HTTP response from a socket, advancing the socket to the beginning of the
        /// response body if applicable, and returning an <see cref="HttpResponse"/> which has
        /// a socket client context associated with the socket.
        /// </summary>
        /// <param name="socket">The socket to read a response from.</param>
        /// <param name="expectedProtocolVersion">The expected HTTP protocol version of the response.</param>
        /// <param name="logger">A logger</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="useManualSocketContext">If true, associate the returned response with
        /// an <see cref="IHttpClientContext"/> which does not touch the socket state at all. Typically this is used
        /// when the caller expects a 101 Switching Protocols response and wants to reuse the socket for something else.</param>
        /// <returns></returns>
        public static async Task<HttpResponse> ReadResponseFromSocket(
            ISocket socket,
            HttpVersion expectedProtocolVersion,
            ILogger logger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            bool useManualSocketContext = false)
        {
            Tuple<PooledBuffer<byte>, int> requestDataBlock = await ReadHttpHeaderBlock(socket, logger, cancelToken, realTime).ConfigureAwait(false);
            
            if (requestDataBlock == null)
            {
                throw new IOException("Could not read HTTP response from socket");
            }

            using (requestDataBlock.Item1)
            {
                HttpVersion responseProtocol;
                int responseCode;
                string responseMessage;
                int indexOfFirstHeaderLine;
                ParseHttpResponseLine(
                    requestDataBlock.Item1.Buffer,
                    requestDataBlock.Item2,
                    out responseProtocol,
                    out responseCode,
                    out responseMessage,
                    out indexOfFirstHeaderLine,
                    HTTP_COMMON_STRING_CACHE);

                int endOfHeaders;
                HttpHeaders parsedHeaders = ParseHttpHeaders(
                    requestDataBlock.Item1.Buffer,
                    requestDataBlock.Item2,
                    indexOfFirstHeaderLine,
                    out endOfHeaders,
                    HTTP_COMMON_STRING_CACHE);

                // Determine if the request has a content length or chunked transfer encoding, that will dictate what kind of stream we create to read it
                HttpContentStream contentStream;
                string probeHeaderValue;
                long? declaredContentLength = null;
                if (parsedHeaders.TryGetValue(HttpConstants.HEADER_KEY_CONTENT_LENGTH, out probeHeaderValue))
                {
                    long parsedContentLength;
                    if (!long.TryParse(probeHeaderValue, out parsedContentLength))
                    {
                        throw new FormatException(string.Format("Could not parse Content-Length header from HTTP request: Value was \"{0}\"", probeHeaderValue));
                    }

                    declaredContentLength = parsedContentLength;
                }

                bool expectTrailers = parsedHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRAILER);

                if (parsedHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING))
                {
                    if (parsedHeaders.ContainsValue(
                        HttpConstants.HEADER_KEY_TRANSFER_ENCODING,
                        HttpConstants.HEADER_VALUE_TRANSFER_ENCODING_CHUNKED,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (expectedProtocolVersion == HttpVersion.HTTP_1_0)
                        {
                            throw new NotSupportedException("Incoming HTTP response uses chunked transfer encoding when client specifies it is only HTTP/1.0 compatible");
                        }

                        contentStream = new HttpChunkedContentStream(new WeakPointer<ISocket>(socket), logger.Clone("HttpContentStream"), declaredContentLength, expectTrailers);
                    }
                    else
                    {
                        throw new NotSupportedException(string.Format("This server does not understand HTTP request transfer encoding \"{0}\"", probeHeaderValue));
                    }
                }
                else if (declaredContentLength.HasValue)
                {
                    contentStream = new HttpFixedContentStream(
                        new WeakPointer<ISocket>(socket),
                        declaredContentLength.Value);
                }
                else
                {
                    contentStream = new HttpFixedContentStream(
                        new WeakPointer<ISocket>(socket),
                        0);
                }

                // Http/1.0 only allows linger if connection: keep-alive is explicit.
                // Http/1.1 is the opposite, assuming linger unless connection: close is explicit.
                bool linger = responseProtocol == HttpVersion.HTTP_1_1;
                if (parsedHeaders != null &&
                    parsedHeaders.TryGetValue(HttpConstants.HEADER_KEY_CONNECTION, out probeHeaderValue))
                {
                    if (responseProtocol == HttpVersion.HTTP_1_1 &&
                        string.Equals(HttpConstants.HEADER_VALUE_CONNECTION_CLOSE, probeHeaderValue, StringComparison.OrdinalIgnoreCase))
                    {
                        linger = false;
                    }
                    else if (responseProtocol == HttpVersion.HTTP_1_0 &&
                        string.Equals(HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE, probeHeaderValue, StringComparison.OrdinalIgnoreCase))
                    {
                        linger = true;
                    }
                }

                IHttpClientContext socketContext;
                if (useManualSocketContext)
                {
                    // Caller wants full manual control over the context. Typically this means they are doing an Upgrade
                    // request and want to reuse the socket for some other protocol (e.g. WebSockets)
                    socketContext = new ManualSocketHttpClientContext(responseProtocol);
                }
                else
                {
                    socketContext = new SocketHttpClientContext(new WeakPointer<ISocket>(socket), linger, logger, responseProtocol);
                }

                return HttpResponse.CreateIncoming(responseCode, responseMessage, parsedHeaders, contentStream, socketContext);
            }
        }

        /// <summary>
        /// Writes an HTTP response to a socket, fully reading and disposing of its content stream in the process.
        /// </summary>
        /// <param name="response">The response to write</param>
        /// <param name="protocolVersion">The HTTP protocol version in use</param>
        /// <param name="socket">The socket to write to</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="logger">A logger</param>
        /// <param name="connectionDescriptionProducer">A function that produces a string which describes this connection (for logging)</param>
        /// <returns>An async task</returns>
        public static Task WriteResponseToSocket(
            HttpResponse response,
            HttpVersion protocolVersion,
            ISocket socket,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            ILogger logger,
            Func<string> connectionDescriptionProducer)
        {
            return WriteResponseToSocket(
                response,
                protocolVersion,
                socket,
                cancelToken,
                realTime,
                logger,
                connectionDescriptionProducer,
                trailerNames: null,
                trailerDelegate: null);
        }

        /// <summary>
        /// Writes an HTTP response to a socket, fully reading and disposing of its content stream in the process.
        /// </summary>
        /// <param name="response">The response to write</param>
        /// <param name="protocolVersion">The HTTP protocol version in use</param>
        /// <param name="socket">The socket to write to</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="logger">A logger</param>
        /// <param name="connectionDescriptionProducer">A function that produces a string which describes this connection (for logging)</param>
        /// <param name="trailerNames">A set of trailer names to declare, may be null</param>
        /// <param name="trailerDelegate">A function delegate where the input is the trailer name and the output is an async task which produces the trailer value.</param>
        /// <returns>An async task</returns>
        public static async Task WriteResponseToSocket(
            HttpResponse response,
            HttpVersion protocolVersion,
            ISocket socket,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            ILogger logger,
            Func<string> connectionDescriptionProducer,
            IReadOnlyCollection<string> trailerNames,
            Func<string, Task<string>> trailerDelegate)
        {
            if (response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRAILER))
            {
                throw new ArgumentException("You may not set the \"Trailer\" HTTP header manually");
            }

            if (trailerNames != null && trailerNames.Count > 0 && trailerDelegate == null)
            {
                throw new ArgumentNullException("A trailer delegate is required when declaring trailers");
            }

            using (PooledBuffer<byte> scratchBuf = BufferPool<byte>.Rent(65536))
            {
                bool allowChunkedTransfer = protocolVersion == HttpVersion.HTTP_1_1;

                // See if we are in HTTP/1.1 but the response specified some kind of transfer encoding other than chunked
                if (allowChunkedTransfer && response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING))
                {
                    if (!response.ResponseHeaders.ContainsValue(
                        HttpConstants.HEADER_KEY_TRANSFER_ENCODING,
                        HttpConstants.HEADER_VALUE_TRANSFER_ENCODING_CHUNKED,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        logger.Log("Disabling HTTP chunked transfer as hinted by response");
                        allowChunkedTransfer = false;
                    }
                }

                // If this is HTTP 1.0, preread the entire response content so we can generate a content-length header
                NonRealTimeStream responseStream = response.GetOutgoingContentStream();
                RecyclableMemoryStream fixedContent;
                if (response.ResponseCode >= 200)
                {
                    fixedContent = await ConvertHttpStreamToFixedContentIfNecessary(responseStream, response.KnownContentLength, allowChunkedTransfer, response.ResponseHeaders, logger, scratchBuf.Buffer, cancelToken, realTime).ConfigureAwait(false);

                    // Is there any response content at all? If not, make sure there is a Content-Length: 0 header
                    if (responseStream is EmptyHttpContentStream ||
                        (!response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_CONTENT_LENGTH) &&
                        !response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING)))
                    {
                        response.ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_LENGTH] = "0";
                    }
                }
                else
                {
                    if (response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_CONTENT_LENGTH))
                    {
                        logger.Log("Response content was specified for a 1xx HTTP response; this is not allowed", LogLevel.Wrn);
                    }

                    fixedContent = null;
                    response.ResponseHeaders.Remove(HttpConstants.HEADER_KEY_CONTENT_LENGTH);
                    response.ResponseHeaders.Remove(HttpConstants.HEADER_KEY_TRANSFER_ENCODING);
                }

                // Trailers are only allowed to be sent on chunked-transfer messages, so we check that fixedContent is null here
                bool sendTrailers = trailerNames != null && trailerNames.Count > 0 && allowChunkedTransfer && fixedContent == null;

                try
                {
                    // Write each string piecewise to prevent us fram having to reallocate new string buffers
                    // Write the response line e.g. "HTTP/1.1 200 OK"
                    int bytesOfDataInScratchBuffer = 0;
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(protocolVersion.ProtocolString, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(" ", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(response.ResponseCode.ToString(CultureInfo.InvariantCulture), scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(" ", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(response.ResponseMessage, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer("\r\n", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);

                    // Write lines for each header, making sure we encode strings directly to the buffer instead of reallocating anything.
                    foreach (KeyValuePair<string, IReadOnlyCollection<string>> headerKvp in response.ResponseHeaders)
                    {
                        if (string.Equals(headerKvp.Key, HttpConstants.HEADER_KEY_TRAILER, StringComparison.OrdinalIgnoreCase))
                        {
                            // we handle the Trailer header specifically later on
                            continue;
                        }

                        // Write each header e.g. "Content-Type: application/json\r\n"
                        // Concatenate multiple header values together using ", " delimiter
                        bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(headerKvp.Key, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                        bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(": ", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                        bool first = true;

                        foreach (string singleHeaderValue in headerKvp.Value)
                        {
                            if (!first)
                            {
                                bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(", ", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                            }

                            bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(singleHeaderValue, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                            first = false;
                        }

                        bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer("\r\n", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    }

                    // Declare trailers if applicable
                    if (sendTrailers)
                    {
                        bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(HttpConstants.HEADER_KEY_TRAILER, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                        bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(": ", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);

                        bool first = true;
                        foreach (string trailerName in trailerNames)
                        {
                            // Validate trailer name
                            // there's probably more than this... based on https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Trailer
                            if (!IsValidTrailerName(trailerName))
                            {
                                throw new ArgumentException("An HTTP trailer may not be used to transmit message-framing, routing, authentication, request modifier, or content header names");
                            }

                            if (!first)
                            {
                                bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(", ", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                            }

                            bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(trailerName, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                            first = false;
                        }

                        bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer("\r\n", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    }

                    // And flush the entire buffer for the response headers
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer("\r\n", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    if (bytesOfDataInScratchBuffer > 0)
                    {
                        //logger.Log(HTTP_TEXT_ENCODING.GetString(scratchBuf.Buffer, 0, bytesOfDataInScratchBuffer).Replace("\r", "\\r").Replace("\n", "\\n"));
                        await socket.WriteAsync(scratchBuf.Buffer, 0, bytesOfDataInScratchBuffer, cancelToken, realTime).ConfigureAwait(false);
                        bytesOfDataInScratchBuffer = 0;
                    }

                    // Then write the content stream (if any)
                    // We have to tread a bit carefully in case we converted streaming content to fixed content and
                    // thus the response.KnownContentLength is different from the actual Content-Length header we are sending.
                    // So we have to double check here.
                    long? declaredContentLength = null;
                    string actualContentLengthHeader;
                    if (response.ResponseHeaders.TryGetValue(HttpConstants.HEADER_KEY_CONTENT_LENGTH, out actualContentLengthHeader))
                    {
                        declaredContentLength = long.Parse(actualContentLengthHeader);
                    }

                    if (response.ResponseCode >= 200 &&
                        (declaredContentLength.GetValueOrDefault(0) > 0 ||
                        response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING)))
                    {
                        await WriteHttpContentToSocket(
                            fixedContent,
                            responseStream,
                            !response.KnownContentLength.HasValue,
                            scratchBuf.Buffer,
                            socket,
                            cancelToken,
                            realTime,
                            sendTrailers).ConfigureAwait(false);
                    }

                    // Write trailers after content if present
                    // See formatting example at https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Trailer
                    if (sendTrailers)
                    {
                        foreach (string trailerName in trailerNames)
                        {
                            string trailerValue = await trailerDelegate(trailerName).ConfigureAwait(false);

                            if (trailerValue == null)
                            {
                                trailerValue = string.Empty;
                            }

                            bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(trailerName, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                            bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(": ", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                            bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(trailerValue, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                            bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer("\r\n", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                        }

                        bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer("\r\n", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);

                        // Flush scratch buffer again
                        if (bytesOfDataInScratchBuffer > 0)
                        {
                            //logger.Log(HTTP_TEXT_ENCODING.GetString(scratchBuf.Buffer, 0, bytesOfDataInScratchBuffer).Replace("\r", "\\r").Replace("\n", "\\n"));
                            await socket.WriteAsync(scratchBuf.Buffer, 0, bytesOfDataInScratchBuffer, cancelToken, realTime).ConfigureAwait(false);
                            bytesOfDataInScratchBuffer = 0;
                        }
                    }

                    await socket.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.Log("Exception in writing http response to " + connectionDescriptionProducer(), LogLevel.Err);
                    logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    fixedContent?.Dispose();
                    responseStream?.Dispose();
                }
            }
        }

        /// <summary>
        /// Checks whether a given trailer field name is valid.
        /// An HTTP trailer may not be used to transmit message-framing, routing, authentication, request modifier, or content header names.
        /// </summary>
        /// <param name="trailerName">The trailer name to check, e.g. "Expires"</param>
        /// <returns>Whether the given name is a valid trailer.</returns>
        public static bool IsValidTrailerName(string trailerName)
        {
            if (string.Equals(trailerName, HttpConstants.HEADER_KEY_TRANSFER_ENCODING) ||
                string.Equals(trailerName, HttpConstants.HEADER_KEY_CONTENT_LENGTH) ||
                string.Equals(trailerName, HttpConstants.HEADER_KEY_CONTENT_TYPE) ||
                string.Equals(trailerName, HttpConstants.HEADER_KEY_TRAILER) ||
                string.Equals(trailerName, HttpConstants.HEADER_KEY_CONTENT_ENCODING) ||
                string.Equals(trailerName, HttpConstants.HEADER_KEY_CONTENT_RANGE) ||
                string.Equals(trailerName, HttpConstants.HEADER_KEY_HOST) ||
                string.Equals(trailerName, HttpConstants.HEADER_KEY_CACHE_CONTROL) ||
                string.Equals(trailerName, HttpConstants.HEADER_KEY_MAX_FORWARDS) ||
                string.Equals(trailerName, HttpConstants.HEADER_KEY_TE) ||
                string.Equals(trailerName, HttpConstants.HEADER_KEY_AUTHORIZATION) ||
                string.Equals(trailerName, HttpConstants.HEADER_KEY_SET_COOKIE))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Writes an HTTP request to a socket.
        /// </summary>
        /// <param name="request">The request to write</param>
        /// <param name="protocolVersion">The HTTP protocol version in use</param>
        /// <param name="socket">The socket to write to</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="logger">A logger</param>
        /// <param name="connectionDescriptionProducer">A function that produces a string which describes this connection (for logging)</param>
        /// <returns>An async task</returns>
        public static async Task WriteRequestToSocket(
            HttpRequest request,
            HttpVersion protocolVersion,
            ISocket socket,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            ILogger logger,
            Func<string> connectionDescriptionProducer)
        {
            using (PooledBuffer<byte> scratchBuf = BufferPool<byte>.Rent(65536))
            {
                bool allowChunkedTransfer = protocolVersion == HttpVersion.HTTP_1_1;
                if (request.RequestHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING))
                {
                    bool responseSpecifiesChunkedTransfer = request.RequestHeaders.ContainsValue(
                        HttpConstants.HEADER_KEY_TRANSFER_ENCODING,
                        HttpConstants.HEADER_VALUE_TRANSFER_ENCODING_CHUNKED,
                        StringComparison.OrdinalIgnoreCase);
                    if (responseSpecifiesChunkedTransfer != allowChunkedTransfer)
                    {
                        logger.Log("Using HTTP chunked transfer = " + responseSpecifiesChunkedTransfer + " as hinted by response");
                    }

                    allowChunkedTransfer = responseSpecifiesChunkedTransfer;
                }

                NonRealTimeStream requestStream = null;
                RecyclableMemoryStream fixedContent = null;

                // Disallow outgoing content on anything but POST and PUT requests
                if (string.Equals(HttpConstants.HTTP_VERB_POST, request.RequestMethod, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(HttpConstants.HTTP_VERB_PUT, request.RequestMethod, StringComparison.OrdinalIgnoreCase))
                {
                    // If this is HTTP 1.0, preread the entire response content so we can generate a content-length header
                    requestStream = request.GetOutgoingContentStream();
                    fixedContent = await ConvertHttpStreamToFixedContentIfNecessary(requestStream, request.KnownContentLength, allowChunkedTransfer, request.RequestHeaders, logger, scratchBuf.Buffer, cancelToken, realTime).ConfigureAwait(false);
                }

                try
                {
                    // Write the request line e.g. "GET / HTTP/1.1\r\n"
                    int bytesOfDataInScratchBuffer = 0;
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(request.RequestMethod, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(" ", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(request.BuildUri(), scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(" ", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(protocolVersion.ProtocolString, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer("\r\n", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);

                    // Write lines for each header, making sure we encode strings directly to the buffer instead of reallocating anything.
                    foreach (KeyValuePair<string, IReadOnlyCollection<string>> headerKvp in request.RequestHeaders)
                    {
                        // Write each header e.g. "Content-Type: application/json\r\n"
                        foreach (string singleHeaderValue in headerKvp.Value)
                        {
                            bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(headerKvp.Key, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                            bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(": ", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                            bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer(singleHeaderValue, scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                            bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer("\r\n", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);
                        }
                    }

                    // And flush the entire buffer for the request header
                    bytesOfDataInScratchBuffer = await WriteStringToSocketUsingScratchBuffer("\r\n", scratchBuf.Buffer, bytesOfDataInScratchBuffer, HTTP_TEXT_ENCODING, socket, cancelToken, realTime).ConfigureAwait(false);

                    if (bytesOfDataInScratchBuffer > 0)
                    {
                        await socket.WriteAsync(scratchBuf.Buffer, 0, bytesOfDataInScratchBuffer, cancelToken, realTime).ConfigureAwait(false);
                        bytesOfDataInScratchBuffer = 0;
                    }

                    // Then write the content stream (if any)
                    if (request.KnownContentLength.GetValueOrDefault(0) > 0 || request.RequestHeaders.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING))
                    {
                        await WriteHttpContentToSocket(
                            fixedContent,
                            requestStream,
                            !request.KnownContentLength.HasValue,
                            scratchBuf.Buffer,
                            socket,
                            cancelToken,
                            realTime,
                            expectTrailers: false).ConfigureAwait(false);
                    }

                    await socket.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    if (SocketHelpers.DoesExceptionIndicateSocketClosed(e))
                    {
                        logger.LogFormat(
                            LogLevel.Err,
                            DataPrivacyClassification.SystemMetadata,
                            "Exception in writing http request to {0}. The connection was forcibly closed.",
                            connectionDescriptionProducer());
                    }
                    else
                    {
                        logger.LogFormat(
                            LogLevel.Err,
                            DataPrivacyClassification.SystemMetadata,
                            "Exception in writing http request to {0}.",
                            connectionDescriptionProducer());
                        logger.Log(e, LogLevel.Err);
                    }
                }
                finally
                {
                    fixedContent?.Dispose();
                    requestStream?.Dispose();
                }
            }
        }

        /// <summary>
        /// If the HTTP protocol doesn't support chunked transfer encoding, copies the entire payload stream into a variable size buffer
        /// so it can be sent with fixed content length instead. This method will modify the headers of the request/response to correspond
        /// with the changes.
        /// </summary>
        /// <param name="contentStream">The original HTTP content stream. May be null</param>
        /// <param name="knownContentLength">Some HTTP requests / responses know their exact content length in advance, if so it will be passed here</param>
        /// <param name="allowChunkedTransfer">True if chunked-transfer encoding is allowed for this request or response</param>
        /// <param name="headers">The headers for the current HTTP message. These will be modified in place to match the processing done in this method.</param>
        /// <param name="logger">A logger</param>
        /// <param name="scratchBuf">A scratch buffer of any length</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>A pooled buffer containing the entire fixed content payload, or null if we are allowed to just stream the content.</returns>
        private static async ValueTask<RecyclableMemoryStream> ConvertHttpStreamToFixedContentIfNecessary(
            NonRealTimeStream contentStream,
            long? knownContentLength,
            bool allowChunkedTransfer,
            IHttpHeaders headers,
            ILogger logger,
            byte[] scratchBuf,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            RecyclableMemoryStream returnVal = null;
            if (contentStream != null)
            {
                if (knownContentLength.HasValue)
                {
                    // Content length was set manually, so honor it
                    if (headers.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING))
                    {
                        headers.Remove(HttpConstants.HEADER_KEY_TRANSFER_ENCODING);
                        logger.Log("Removing Transfer-Encoding header as we know the exact content length", LogLevel.Wrn);
                    }

                    headers.Set(HttpConstants.HEADER_KEY_CONTENT_LENGTH, knownContentLength.Value.ToString(CultureInfo.InvariantCulture));
                }
                else if (allowChunkedTransfer)
                {
                    if (contentStream is EmptyHttpContentStream)
                    {
                        // No content at all.
                        headers.Remove(HttpConstants.HEADER_KEY_TRANSFER_ENCODING);
                        headers.Remove(HttpConstants.HEADER_KEY_CONTENT_LENGTH);
                    }
                    else
                    {
                        headers[HttpConstants.HEADER_KEY_TRANSFER_ENCODING] = HttpConstants.HEADER_VALUE_TRANSFER_ENCODING_CHUNKED;
                        if (headers.ContainsKey(HttpConstants.HEADER_KEY_CONTENT_LENGTH))
                        {
                            // headers.Remove(HttpConstants.HEADER_KEY_CONTENT_LENGTH);
                            logger.Log("Content-Length is set on an HTTP message which also specifies chunked transfer encoding. Header will be ignored.", LogLevel.Wrn);
                        }
                    }
                }
                else
                {
                    if (headers.ContainsKey(HttpConstants.HEADER_KEY_CONTENT_LENGTH))
                    {
                        // If we hit this case it means someone set the Content-Length header without actually properly setting the content length.
                        headers.Remove(HttpConstants.HEADER_KEY_CONTENT_LENGTH);
                        logger.Log("Content-Length should not be set manually on an HTTP message; removing header", LogLevel.Wrn);
                    }

                    logger.Log("Converting streaming content to fixed-size buffer because HTTP chunked transfer encoding is not available", LogLevel.Wrn);
                    returnVal = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default);

                    // Copy the entire request stream to this buffer
                    int scratchReadSize = 1;
                    while (scratchReadSize > 0)
                    {
                        scratchReadSize = await contentStream.ReadAsync(scratchBuf, 0, scratchBuf.Length, cancelToken, realTime).ConfigureAwait(false);
                        if (scratchReadSize > 0)
                        {
                            returnVal.Write(scratchBuf, 0, scratchReadSize);
                        }
                    }

                    returnVal.Seek(0, SeekOrigin.Begin);

                    // Generate the content-length header
                    headers[HttpConstants.HEADER_KEY_CONTENT_LENGTH] = returnVal.Length.ToString(CultureInfo.InvariantCulture);

                    if (headers.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING))
                    {
                        headers.Remove(HttpConstants.HEADER_KEY_TRANSFER_ENCODING);
                        logger.Log("Transfer encoding is not supported; removing", LogLevel.Wrn);
                    }
                }
            }
            else
            {
                // No content, remove transfer encoding headers
                if (headers.ContainsKey(HttpConstants.HEADER_KEY_TRANSFER_ENCODING))
                {
                    headers.Remove(HttpConstants.HEADER_KEY_TRANSFER_ENCODING);
                    logger.Log("Transfer-Encoding is set on HTTP message with no content; removing header", LogLevel.Wrn);
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Writes an entire HTTP payload to the socket, either as fixed-length content or a chunked transfer stream.
        /// </summary>
        /// <param name="fixedContent">If non-null, just write all of the data in this buffer without any extras.</param>
        /// <param name="dataStream">If fixed content is null, transfer this stream instead using HTTP chunked-transfer encoding.</param>
        /// <param name="useChunkedTransfer">If true, use HTTP/1.1 chunked-transfer encoding</param>
        /// <param name="scratchBuf">A scratch buffer of any size</param>
        /// <param name="socket">The socket to write to</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="expectTrailers">Whether we expect to send trailers after this content (affects the terminator pattern at the end)</param>
        /// <returns>An async task</returns>
        private static async Task WriteHttpContentToSocket(
            RecyclableMemoryStream fixedContent,
            NonRealTimeStream dataStream,
            bool useChunkedTransfer, 
            byte[] scratchBuf,
            ISocket socket,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            bool expectTrailers)
        {
            if (fixedContent != null)
            {
                if (fixedContent.Length > 0)
                {
                    // No chunked transfer, just write it as one block
                    int scratchReadSize = 1;
                    while (scratchReadSize > 0)
                    {
                        scratchReadSize = fixedContent.Read(scratchBuf, 0, scratchBuf.Length);
                        if (scratchReadSize > 0)
                        {
                            await socket.WriteAsync(scratchBuf, 0, scratchReadSize, cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
            else if (dataStream != null)
            {
                if (useChunkedTransfer)
                {
                    // Send a streaming (chunked-transfer) payload
                    // TODO If the client accepts gzip encoding, we could do compression right here
                    int readSize = 1;
                    int maxChunkSize = Math.Min(16384, scratchBuf.Length - 16);
                    while (readSize > 0)
                    {
                        // bytes 0-15 of the scratch buffer are reserved for the hexadecimal length prefix and newlines
                        // chunk data starts at index 16
                        readSize = await dataStream.ReadAsync(scratchBuf, 16, maxChunkSize, cancelToken, realTime).ConfigureAwait(false);
                        //logger.Log("Chunked-transfer: read " + readSize + " bytes", LogLevel.Vrb);

                        //logger.Log("Chunked-transfer: read " + readSize + " bytes", LogLevel.Vrb);
                        if (readSize > 0)
                        {
                            int chunkHeaderLength = GenerateChunkHeaderBytes(readSize, false, scratchBuf);
                            await socket.WriteAsync(scratchBuf, 0, chunkHeaderLength, cancelToken, realTime).ConfigureAwait(false);
                            await socket.WriteAsync(scratchBuf, 16, readSize, cancelToken, realTime).ConfigureAwait(false);
                            await socket.WriteAsync(HttpConstants.CAPTURE_PATTERN_RN, 0, 2, cancelToken, realTime).ConfigureAwait(false);
                            //await socket.FlushAsync(cancelToken).ConfigureAwait(false);
                            //logger.Log("Chunked-transfer: wrote " + readSize + " bytes", LogLevel.Vrb);
                        }
                    }

                    // Send the terminator. Schwarzenegger all up in these sockets
                    if (expectTrailers)
                    {
                        // Very subtle semantic thing: If the server is sending trailers, they go in the place where the
                        // data would normally go in a zero-length chunk.
                        // That is to say, instead of being 0\r\n\r\nHeader, it's 0\r\nHeader. There's one less linebreak.
                        // So we have to send a slightly different terminator in this case.
                        scratchBuf[0] = (byte)0x30; // 0
                        scratchBuf[1] = (byte)0x0D; // \r
                        scratchBuf[2] = (byte)0x0A; // \n
                        await socket.WriteAsync(scratchBuf, 0, 3, cancelToken, realTime).ConfigureAwait(false);
                    }
                    else
                    {
                        int terminatorLength = GenerateChunkHeaderBytes(0, true, scratchBuf);
                        await socket.WriteAsync(scratchBuf, 0, terminatorLength, cancelToken, realTime).ConfigureAwait(false);
                        //logger.Log("Chunked-transfer: closed output", LogLevel.Vrb);
                    }
                }
                else
                {
                    int scratchReadSize = 1;
                    while (scratchReadSize > 0)
                    {
                        scratchReadSize = await dataStream.ReadAsync(scratchBuf, 0, scratchBuf.Length, cancelToken, realTime).ConfigureAwait(false);
                        if (scratchReadSize > 0)
                        {
                            await socket.WriteAsync(scratchBuf, 0, scratchReadSize, cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Encodes a string to a scratch buffer (of any size), and if the scratch buffer is filled more than 75%,
        /// commit that entire buffer to an outbound socket and continue processing. This method will use piecewise
        /// writes if the input string is large enough to overflow the target buffer, so it should be safe for
        /// any combination of string length + scratch buffer length. The return value is the number of bytes remaining
        /// in the scratch buffer after processing finishes.
        /// </summary>
        /// <param name="str">The string to send</param>
        /// <param name="scratch">A scratch buffer of any length (but greater than like 16 at least please)</param>
        /// <param name="bytesInScratchBuffer">The number of bytes currently stored in the scratch buffer</param>
        /// <param name="stringEncoding">The encoding to use for the string</param>
        /// <param name="outSocket">The socket to write the encoded string to.</param>
        /// <param name="cancelToken">A cancel token when writing to the socket.</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The number of bytes remaining in the scratch buffer which haven't been written to the socket</returns>
        private static async ValueTask<int> WriteStringToSocketUsingScratchBuffer(
            string str,
            byte[] scratch,
            int bytesInScratchBuffer,
            Encoding stringEncoding,
            ISocket outSocket,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            int charsConsumed = 0;
            int maxChars = str.Length;
            int flushThreshold = scratch.Length / 4 * 3;
            while (charsConsumed < maxChars)
            {
                int maxCharsPerBlock = (scratch.Length / 2) - bytesInScratchBuffer; // Assume no single char can exceed 2 bytes. Since this is HTTP header data this should all be ASCII anyways.
                int thisBlockSizeChars = Math.Min(maxCharsPerBlock, maxChars - charsConsumed);
                int encodedByteSize = stringEncoding.GetBytes(str, charsConsumed, thisBlockSizeChars, scratch, bytesInScratchBuffer);
                charsConsumed += thisBlockSizeChars;
                bytesInScratchBuffer += encodedByteSize;
                if (bytesInScratchBuffer > flushThreshold)
                {
                    await outSocket.WriteAsync(scratch, 0, bytesInScratchBuffer, cancelToken, realTime).ConfigureAwait(false);
                    bytesInScratchBuffer = 0;
                }
            }

            return bytesInScratchBuffer;
        }

        /// <summary>
        /// Given a set of endpoint bindings for a server, determine the one that is most suitable
        /// to use for loopback requests, and return it as a Uri.
        /// </summary>
        /// <param name="serverBindings">A list of server bindings.</param>
        /// <returns>A suitable loopback Uri, usually something like "http://localhost:43321"</returns>
        public static Uri FindBestLocalAccessUrl(IEnumerable<ServerBindingInfo> serverBindings)
        {
            Uri bestSecureUri = null;
            Uri bestNonSecureUri = null;

            foreach (ServerBindingInfo endpoint in serverBindings)
            {
                if (endpoint.UseTls)
                {
                    string hostName;
                    if (!string.IsNullOrEmpty(endpoint.TlsCertificateIdentifier.SubjectName))
                    {
                        hostName = endpoint.TlsCertificateIdentifier.SubjectName;
                    }
                    else
                    {
                        hostName = "localhost";
                    }

                    bestSecureUri = new Uri(string.Format("https://{0}:{1}/", hostName, endpoint.LocalIpPort.GetValueOrDefault(443)));
                }
                else
                {
                    bestNonSecureUri = new Uri(string.Format("http://localhost:{0}/", endpoint.LocalIpPort.GetValueOrDefault(80)));
                }
            }

            return bestNonSecureUri ?? bestSecureUri ?? new Uri("http://localhost/");
        }

        public static string ResolveMimeType(string path)
        {
            string responseType = "application/octet-stream";
            if (path.Contains("."))
            {
                string fileExtension = path.Substring(path.LastIndexOf('.')).ToLowerInvariant();
                if (fileExtension.Equals(".html") || fileExtension.Equals(".htm"))
                {
                    responseType = "text/html";
                }
                else if (fileExtension.Equals(".png"))
                {
                    responseType = "image/png";
                }
                else if (fileExtension.Equals(".gif"))
                {
                    responseType = "image/gif";
                }
                else if (fileExtension.Equals(".bmp"))
                {
                    responseType = "image/bmp";
                }
                else if (fileExtension.Equals(".jpg") || fileExtension.Equals(".jpeg"))
                {
                    responseType = "image/jpeg";
                }
                else if (fileExtension.Equals(".css"))
                {
                    responseType = "text/css";
                }
                else if (fileExtension.Equals(".js"))
                {
                    responseType = "text/javascript";
                }
                else if (fileExtension.Equals(".txt") || fileExtension.Equals(".template") || fileExtension.Equals(".ini"))
                {
                    responseType = "text/plain";
                }
                else if (fileExtension.Equals(".xml"))
                {
                    responseType = "text/xml";
                }
                else if (fileExtension.Equals(".exe") || fileExtension.Equals(".dll"))
                {
                    responseType = "application/octet-stream";
                }
                else if (fileExtension.Equals(".ico"))
                {
                    responseType = "image/x-icon";
                }
            }
            else
            {
                // The path is a default root like "/". Assume html
                responseType = "text/html";
            }

            return responseType;
        }

        /// <summary>
        /// Translates an audio codec name into a HTTP content-type header matching that codec
        /// </summary>
        /// <param name="codec"></param>
        /// <param name="codecParams"></param>
        /// <returns></returns>
        public static string GetHeaderValueForAudioFormat(string codec, string codecParams)
        {
            if (string.IsNullOrEmpty(codec) || string.Equals(codec, "pcm", StringComparison.OrdinalIgnoreCase))
            {
                AudioSampleFormat fmt;
                if (CommonCodecParamHelper.TryParseCodecParams(codecParams, out fmt))
                {
                    return string.Format("audio/wav; codec=\"audio/pcm\"; samplerate={0}", fmt.SampleRateHz);
                }
                else
                {
                    return "audio/wav; codec=\"audio/pcm\"";
                }
            }
            else if (string.Equals(codec, "speex", StringComparison.OrdinalIgnoreCase))
            {
                return "audio/speex";
            }
            else if (string.Equals(codec, "flac", StringComparison.OrdinalIgnoreCase))
            {
                return "audio/flac";
            }
            else if (string.Equals(codec, "mp3", StringComparison.OrdinalIgnoreCase))
            {
                return "audio/mpeg";
            }
            else if (string.Equals(codec, "opus", StringComparison.OrdinalIgnoreCase))
            {
                return "audio/ogg; codecs=opus";
            }
            else if (string.Equals(codec, "ogg", StringComparison.OrdinalIgnoreCase))
            {
                return "audio/ogg";
            }
            else if (string.Equals(codec, "ilbc", StringComparison.OrdinalIgnoreCase))
            {
                return "audio/iLBC";
            }
            else if (string.Equals(codec, "sqrt", StringComparison.OrdinalIgnoreCase))
            {
                AudioSampleFormat fmt;
                if (CommonCodecParamHelper.TryParseCodecParams(codecParams, out fmt))
                {
                    return string.Format("audio/sqrt\"; samplerate={0}", fmt.SampleRateHz);
                }
                else
                {
                    return "audio/sqrt\"";
                }
            }
            else if (string.Equals(codec, "alaw", StringComparison.OrdinalIgnoreCase))
            {
                AudioSampleFormat fmt;
                if (CommonCodecParamHelper.TryParseCodecParams(codecParams, out fmt))
                {
                    return string.Format("audio/alaw\"; samplerate={0}", fmt.SampleRateHz);
                }
                else
                {
                    return "audio/alaw\"";
                }
            }
            else if (string.Equals(codec, "ulaw", StringComparison.OrdinalIgnoreCase))
            {
                AudioSampleFormat fmt;
                if (CommonCodecParamHelper.TryParseCodecParams(codecParams, out fmt))
                {
                    return string.Format("audio/ulaw\"; samplerate={0}", fmt.SampleRateHz);
                }
                else
                {
                    return "audio/ulaw\"";
                }
            }
            else if (string.Equals(codec, "g722", StringComparison.OrdinalIgnoreCase))
            {
                AudioSampleFormat fmt;
                if (CommonCodecParamHelper.TryParseCodecParams(codecParams, out fmt))
                {
                    return string.Format("audio/g722\"; samplerate={0}", fmt.SampleRateHz);
                }
                else
                {
                    return "audio/g722\"";
                }
            }
            else
            {
                return "application/octet-stream";
            }
        }

        public static string GetStatusStringForStatusCode(int statusCode)
        {
            switch (statusCode)
            {
                case 200:
                    return "OK";
                case 400:
                    return "Bad Request";
                case 404:
                    return "Not Found";
                case 419:
                    return "Client Error";
                case 500:
                    return "Server Error";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Generates a data chunk length header such as "48F\r\n" in conformity with RFC 2616-3.6.1
        /// https://tools.ietf.org/html/rfc2616#section-3.6.1
        /// </summary>
        /// <param name="writeSize">The size of the chunk we are generating the header for</param>
        /// <param name="isLastChunk">If true, this is the last 0-length chunk to terminate the transmission</param>
        /// <param name="output">The byte array to write the output to. Must be at least 16 bytes.</param>
        /// <returns>The number of bytes written to the output, in other words the total length of the header.</returns>
        public static int GenerateChunkHeaderBytes(int writeSize, bool isLastChunk, byte[] output)
        {
            if (isLastChunk)
            {
                output[0] = (byte)0x30; // 0
                output[1] = (byte)0x0D; // \r
                output[2] = (byte)0x0A; // \n
                output[3] = (byte)0x0D; // \r
                output[4] = (byte)0x0A; // \n
                return 5;
            }

#if DEBUG
            if (writeSize < 0)
            {
                throw new ArgumentOutOfRangeException("HTTP chunk size cannot be negative");
            }
#endif

            int writeIdx = 0;
            uint formatter = 0xF0000000U;
            int shift = 28;
            uint unsignedWriteSize = (uint)writeSize;
            while (formatter > 0)
            {
                uint remainder = (unsignedWriteSize & formatter) >> shift;
                //Debug.Assert(remainder < 16);
                if (remainder >= 10)
                {
                    output[writeIdx++] = (byte)(0x37 /* 'A' - 10 */ + remainder); // For uppercase use 0x57
                }
                else if (remainder > 0 || (remainder == 0 && writeIdx > 0))
                {
                    output[writeIdx++] = (byte)(0x30 /* '0' */ + remainder);
                }

                shift -= 4;
                formatter = formatter >> 4;
            }

            output[writeIdx++] = (byte)0x0D; // \r
            output[writeIdx++] = (byte)0x0A; // \n
            return writeIdx;
        }

        /// <summary>
        /// Interprets a sequence of bytes as a hexadecimal string and returned the parsed hex value.
        /// </summary>
        /// <param name="scratchBuf">A byte buffer</param>
        /// <param name="stringStartIdx">The index of the first hex char</param>
        /// <param name="stringLength">The length of the hex string</param>
        /// <returns>The parsed value</returns>
        public static int ParseChunkLength(byte[] scratchBuf, int stringStartIdx, int stringLength)
        {
            int returnVal = 0;
            int radix = 0;
            for (int hexChar = stringStartIdx + stringLength - 1; hexChar >= stringStartIdx; hexChar--)
            {
                byte c = scratchBuf[hexChar];
                if (c < 58)
                {
                    // Numeric char
                    returnVal |= (byte)(c - 0x30) << radix; // '0'
                }
                else if (c < 71)
                {
                    // Uppercase A-F char
                    returnVal |= (byte)(c - 0x37) << radix; // 'A' - 10
                }
                else
                {
                    // Lowercase a-f char
                    returnVal |= (byte)(c - 0x57) << radix; // 'a' - 10
                }

                radix += 4;
            }

            return returnVal;
        }

        /// <summary>
        /// Searches for a specific byte in a byte array, and returns the index of the first match (if found),
        /// or -1 if no match.
        /// </summary>
        /// <param name="array">The array to search within.</param>
        /// <param name="value">The value to search for</param>
        /// <param name="startIdx">The index of the first slot to look in.</param>
        /// <param name="maxCount">The maximum number of bytes to search.</param>
        /// <returns>The absolute index of the first match, or -1 if no match found.</returns>
        private static int IndexOf(this byte[] array, byte value, int startIdx, int maxCount)
        {
#if DEBUG
            if (startIdx < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startIdx));
            }
            if (maxCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            }
            if (startIdx + maxCount > array.Length)
            {
                throw new ArgumentOutOfRangeException("Search will exceed the bounds of the input array");
            }
#endif

            for (int endIdx = startIdx + maxCount; startIdx < endIdx; startIdx++)
            {
                if (array[startIdx] == value)
                {
                    return startIdx;
                }
            }

            return -1;
        }
    }
}
