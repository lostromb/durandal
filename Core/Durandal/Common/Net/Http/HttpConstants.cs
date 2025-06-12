using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http
{
    public static class HttpConstants
    {
        /// <summary>
        /// The byte pattern of a carriage return followed by a newline: 0x0D0A
        /// </summary>
        public static readonly byte[] CAPTURE_PATTERN_RN = new byte[] { (byte)'\r', (byte)'\n' };

        /// <summary>
        /// The byte pattern of a carriage return followed by a newline, repeated twice: 0x0D0A0D0A
        /// </summary>
        public static readonly byte[] CAPTURE_PATTERN_RNRN = new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };

        /// <summary>
        /// The byte pattern for the http version code preceded by a space, " HTTP/"
        /// </summary>
        public static readonly byte[] CAPTURE_PATTERN_HTTP_VERSION_PREFIX = new byte[] { (byte)' ', (byte)'H', (byte)'T', (byte)'T', (byte)'P', (byte)'/' };

        public static readonly string MIME_TYPE_FORMDATA = "application/x-www-form-urlencoded";
        public static readonly string MIME_TYPE_OCTET_STREAM = "application/octet-stream";
        public static readonly string MIME_TYPE_ASCII_TEXT = "text/plain";
        public static readonly string MIME_TYPE_UTF8_TEXT = "text/plain; charset=utf-8";
        public static readonly string MIME_TYPE_JSON = "application/json";

        public static readonly string HEADER_KEY_CONTENT_TYPE = "Content-Type";
        public static readonly string HEADER_KEY_CONTENT_LENGTH = "Content-Length";
        public static readonly string HEADER_KEY_TRANSFER_ENCODING = "Transfer-Encoding";
        public static readonly string HEADER_KEY_CONNECTION = "Connection";
        public static readonly string HEADER_KEY_HOST = "Host";
        public static readonly string HEADER_KEY_LOCATION = "Location";
        public static readonly string HEADER_KEY_EXPECT = "Expect";
        public static readonly string HEADER_KEY_AUTHORIZATION = "Authorization";
        public static readonly string HEADER_KEY_TRAILER = "Trailer";
        public static readonly string HEADER_KEY_TE = "TE";
        public static readonly string HEADER_KEY_ACCEPT_ENCODING = "Accept-Encoding";
        public static readonly string HEADER_KEY_CONTENT_ENCODING = "Content-Encoding";
        public static readonly string HEADER_KEY_CONTENT_RANGE = "Content-Range";
        public static readonly string HEADER_KEY_CACHE_CONTROL = "Cache-Control";
        public static readonly string HEADER_KEY_MAX_FORWARDS = "Max-Forwards";
        public static readonly string HEADER_KEY_SET_COOKIE = "Set-Cookie";
        public static readonly string HEADER_KEY_UPGRADE = "Upgrade";
        public static readonly string HEADER_KEY_SEC_WEBSOCKET_VERSION = "Sec-WebSocket-Version";
        public static readonly string HEADER_KEY_SEC_WEBSOCKET_KEY = "Sec-WebSocket-Key";
        public static readonly string HEADER_KEY_SEC_WEBSOCKET_ACCEPT = "Sec-WebSocket-Accept";
        public static readonly string HEADER_KEY_SEC_WEBSOCKET_PROTOCOL = "Sec-WebSocket-Protocol";
        public static readonly string HEADER_KEY_HTTP2_SETTINGS = "HTTP2-Settings";

        /// <summary>
        /// Custom header used for instrumentation. The header value is a TimeSpan of the time between
        /// the server starting to receive the request and starting to send the response.
        /// TODO This could probably be moved to the more web-standard Server-Timing header
        /// https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Server-Timing
        /// </summary>
        public static readonly string HEADER_KEY_SERVER_WORK_TIME = "X-ServerWorkTime";

        public static readonly string HEADER_VALUE_TRANSFER_ENCODING_CHUNKED = "chunked";
        public static readonly string HEADER_VALUE_CONNECTION_CLOSE = "close";
        public static readonly string HEADER_VALUE_CONNECTION_UPGRADE = "Upgrade";
        public static readonly string HEADER_VALUE_CONNECTION_KEEP_ALIVE = "keep-alive";
        public static readonly string HEADER_VALUE_CONNECTION_HTTP2_SETTINGS = "HTTP2-Settings";
        public static readonly string HEADER_VALUE_EXPECT_100_CONTINUE = "100-continue";
        public static readonly string HEADER_VALUE_TRAILERS = "trailers";
        public static readonly string HEADER_VALUE_UPGRADE_WEBSOCKET = "websocket";
        public static readonly string HEADER_VALUE_UPGRADE_H2C = "h2c";

        public static readonly string SCHEME_HTTP = "http";
        public static readonly string SCHEME_HTTPS = "https";
        public static readonly string SCHEME_WS = "ws";
        public static readonly string SCHEME_WSS = "wss";

        public const int HTTP_DEFAULT_PORT = 80;
        public const int HTTPS_DEFAULT_PORT = 443;

        /// <summary>
        /// A canned HTTP response representing a valid HTTP/1.1 100 Continue message.
        /// </summary>
        public static readonly byte[] HTTP_100_CONTINUE_RESPONSE = Encoding.UTF8.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");

        public const string HTTP_VERB_GET = "GET";
        public const string HTTP_VERB_POST = "POST";
        public const string HTTP_VERB_PUT = "PUT";
        public const string HTTP_VERB_DELETE = "DELETE";
        public const string HTTP_VERB_HEAD = "HEAD";
        public const string HTTP_VERB_OPTIONS = "OPTIONS";
        public const string HTTP_VERB_TRACE = "TRACE";
        public const string HTTP_VERB_CONNECT = "CONNECT";
    }
}
