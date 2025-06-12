namespace Durandal.Common.Net.Http
{
    using Durandal.Common.IO;
    using Durandal.Common.IO.Json;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an abstract HTTP protocol request (independent of any specific protocol version, including HTTP/2).
    /// This class is used in two contexts - one is as a server reading an incoming request from the socket, and
    /// the other is as a client sending an outgoing request to the socket. The difference between these contexts is determined
    /// by the <see cref="Direction"/> property. Based on this property, different methods and accessors are enabled or disabled.
    /// For example, it makes no sense to "set content" on an incoming request, so this is disallowed. Likewise for
    /// reading content on an outgoing request. This distinction is also enforced in the constructors
    /// <see cref="CreateIncoming(IHttpHeaders, string, string, string, IHttpFormParameters, string, HttpContentStream, HttpVersion)"/>
    /// and <see cref="CreateOutgoing(string, string)"/>.
    /// </summary>
    public class HttpRequest : IDisposable
    {
        private string _rawRequestFile = "/";
        private string _decodedRequestFile = "/";

        /// <summary>
        /// The HTTP method ("GET", "POST")
        /// </summary>
        public string RequestMethod { get; set; }

        /// <summary>
        /// The path to the file being requested ("/", "/home/my%20file.php").
        /// This is in RAW form (exactly as it appears on the wire, according to RFC spec). Spaces and special characters must be path-encoded first
        /// </summary>
        public string RequestFile
        {
            get
            {
                return _rawRequestFile;
            }
            set
            {
                _rawRequestFile = value;
                _decodedRequestFile = HttpHelpers.UrlPathDecode(value);
            }
        }

        /// <summary>
        /// The path to the file being requested ("/", "/home/my file.php").
        /// This is in NORMALIZED ("file path") form. Its path may contain spaces or other characters
        /// </summary>
        public string DecodedRequestFile
        {
            get
            {
                return _decodedRequestFile;
            }
            set
            {
                _decodedRequestFile = value;
                _rawRequestFile = HttpHelpers.UrlPathEncode(value);
            }
        }

        /// <summary>
        /// The set of HTTP headers to pass along. Some of these headers, such as Content-Length, will
        /// be set automatically. Dictionary lookups are case-insensitive.
        /// </summary>
        public IHttpHeaders RequestHeaders { get; private set; }

        /// <summary>
        /// The set of get parameters that are normally appended to the URL, after the file path and "?".
        /// DO NOT URL ENCODE. Encoding will be done automatically by the transport layer.
        /// Dictionary lookups are case-insensitive.
        /// </summary>
        public IHttpFormParameters GetParameters { get; private set; }

        /// <summary>
        /// The "fragment" part of the URL, which is whatever follows a "#" symbol
        /// </summary>
        public string UrlFragment { get; private set; }

        /// <summary>
        /// If you are a server that is accepting requests, this is the name of the host
        /// from which the request originated
        /// </summary>
        public string RemoteHost { get; set; }

        /// <summary>
        /// Represents the direction that this request is moving relative to the current code.
        /// </summary>
        public NetworkDirection Direction { get; private set; }

        /// <summary>
        /// If this request's body content has a known fixed length; it will be available here.
        /// </summary>
        public long? KnownContentLength { get; private set; }

        /// <summary>
        /// The protocol version used for this request. This value is only set
        /// if you are a server that is receiving the request - meaning it
        /// has already been sent across the wire using a specific protocol.
        /// Otherwise it is null.
        /// </summary>
        public HttpVersion ProtocolVersion { get; private set; }

        /// <summary>
        /// This holds the content of the outgoing or incoming request. This can never be null (if there is no content then this is just a zero-length stream)
        /// </summary>
        private HttpContentStream _requestStream;

        private int _disposed = 0;

        /// <summary>
        /// Creates an incoming HTTP request (implying that you are a server that is receiving a request from the wire)
        /// </summary>
        /// <param name="parsedHeaders">The headers that have been read from the wire (or an empty header collection if you
        /// plan to populate this later)</param>
        /// <param name="requestFile">The url encoded request base path</param>
        /// <param name="requestMethod">The request method</param>
        /// <param name="remoteHost">The remote host name</param>
        /// <param name="getParameters">The set of get parameters</param>
        /// <param name="urlFragment">The url fragment, if any</param>
        /// <param name="incomingRequestStream"></param>
        /// <param name="protocolVersion">The protocol version that was used to send this request over the wire.</param>
        /// <returns>The constructed HTTP request.</returns>
        public static HttpRequest CreateIncoming(
            IHttpHeaders parsedHeaders,
            string requestFile,
            string requestMethod,
            string remoteHost,
            IHttpFormParameters getParameters,
            string urlFragment,
            HttpContentStream incomingRequestStream,
            HttpVersion protocolVersion)
        {
            return new HttpRequest(
                NetworkDirection.Incoming,
                parsedHeaders,
                requestFile,
                requestMethod,
                remoteHost,
                getParameters,
                urlFragment,
                incomingRequestStream,
                protocolVersion);
        }

        /// <summary>
        /// Constructor for outgoing HTTP requests
        /// </summary>
        /// <param name="direction">The direction of this request (usually outgoing)</param>
        /// <param name="requestFile">The requested file (URL encoded form)</param>
        /// <param name="requestMethod">The request method, e.g. "GET"</param>
        /// <param name="getParameters">The dictionary of get parameters</param>
        /// <param name="urlFragment">The URL fragment, which is a specific part of the URL following a # symbol.</param>
        private HttpRequest(NetworkDirection direction, string requestFile, string requestMethod, IHttpFormParameters getParameters, string urlFragment)
        {
            RequestMethod = requestMethod;
            RequestHeaders = new HttpHeaders();
            _requestStream = EmptyHttpContentStream.Singleton;
            GetParameters = getParameters.AssertNonNull(nameof(getParameters));
            RemoteHost = "localhost";
            Direction = direction;
            KnownContentLength = null;
            ProtocolVersion = null;
            RequestFile = requestFile;
            UrlFragment = urlFragment;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        /// <summary>
        /// Constructor for incoming HTTP requests
        /// </summary>
        /// <param name="direction">The direction of this request (usually incoming)</param>
        /// <param name="parsedHeaders">The headers to set for this request</param>
        /// <param name="requestFile">The url encoded request base path</param>
        /// <param name="requestMethod">The request method</param>
        /// <param name="remoteHost">The remote host name or IP</param>
        /// <param name="getParameters">The set of get parameters</param>
        /// <param name="urlFragment">The url fragment, if any</param>
        /// <param name="incomingRequestStream">The stream to read the request body (usually an HTTP socket stream or equivalent)</param>
        /// <param name="protocolVersion">The protocol version that was used to send this request over the wire.</param>
        private HttpRequest(NetworkDirection direction,
            IHttpHeaders parsedHeaders,
            string requestFile,
            string requestMethod,
            string remoteHost,
            IHttpFormParameters getParameters,
            string urlFragment,
            HttpContentStream incomingRequestStream,
            HttpVersion protocolVersion)
        {
            RequestMethod = requestMethod;
            RequestHeaders = parsedHeaders.AssertNonNull(nameof(parsedHeaders));
            _requestStream = incomingRequestStream.AssertNonNull(nameof(incomingRequestStream));
            GetParameters = getParameters.AssertNonNull(nameof(getParameters));
            RemoteHost = remoteHost;
            Direction = direction;
            KnownContentLength = null;
            ProtocolVersion = protocolVersion.AssertNonNull(nameof(protocolVersion));
            RequestFile = requestFile;
            UrlFragment = urlFragment;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~HttpRequest()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Sets the NetworkDirection on this request to be Proxied. This implies that you are a
        /// proxy HTTP client and you have a request from a caller that you want to route to a downstream server
        /// directly. You are responsible for disposing of everything properly afterwards.
        /// </summary>
        public void MakeProxied()
        {
            Direction = NetworkDirection.Proxied;
        }

        /// <summary>
        /// Sets the payload data of this request to be a particular UTF-8 string with the default plaintext type
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        public void SetContent(string payload)
        {
            SetContent(payload, HttpConstants.MIME_TYPE_UTF8_TEXT);
        }

        /// <summary>
        /// Sets the payload data of this request to be a particular byte array.
        /// Ownership of the string builder is transferred to this object.
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        /// <param name="mimeType">The mimetype (Content-Type) of the data</param>
        public void SetContent(PooledStringBuilder payload, string mimeType)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (payload == null)
            {
                throw new ArgumentNullException("Payload data is null");
            }

            PooledStringBuilderStream stream = new PooledStringBuilderStream(payload, StringUtils.UTF8_WITHOUT_BOM);
            SetContent(stream, mimeType);
            KnownContentLength = stream.Length;
        }

        /// <summary>
        /// Sets the payload data of this request to be a particular byte array.
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        /// <param name="mimeType">The mimetype (Content-Type) of the data</param>
        public void SetContent(string payload, string mimeType)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (payload == null)
            {
                throw new ArgumentNullException("Payload data is null");
            }

            StringStream stream = new StringStream(payload, StringUtils.UTF8_WITHOUT_BOM);
            SetContent(stream, mimeType);
            KnownContentLength = stream.Length;
        }

        /// <summary>
        /// Sets the payload data of this request to be a particular byte array.
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        /// <param name="mimeType">The mimetype (Content-Type) of the data</param>
        public void SetContent(byte[] payload, string mimeType)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (payload == null)
            {
                throw new ArgumentNullException("Payload data is null");
            }

            _requestStream = new HttpContentStreamWrapper(new MemoryStream(payload, writable: false), ownsStream: true);
            if (mimeType != null)
            {
                RequestHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = mimeType;
            }

            KnownContentLength = payload.Length;
        }

        /// <summary>
        /// Sets the payload data of this request to be a particular byte array segment.
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        /// <param name="mimeType">The mimetype (Content-Type) of the data</param>
        public void SetContent(ArraySegment<byte> payload, string mimeType)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (payload == null)
            {
                throw new ArgumentNullException("Payload data is null");
            }

            _requestStream = new HttpContentStreamWrapper(new MemoryStream(payload.Array, payload.Offset, payload.Count, writable: false), ownsStream: true);
            if (mimeType != null)
            {
                RequestHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = mimeType;
            }

            KnownContentLength = payload.Count;
        }

        /// <summary>
        /// Sets the payload data of this request to be a particular pooled byte buffer.
        /// Ownership of the buffer is transferred to this object.
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        /// <param name="mimeType">The mimetype (Content-Type) of the data</param>
        public void SetContent(PooledBuffer<byte> payload, string mimeType)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (payload == null || payload.Buffer == null)
            {
                throw new ArgumentNullException("Payload data is null");
            }

            _requestStream = new HttpContentStreamWrapper(new PooledBufferMemoryStream(payload), ownsStream: true);
            if (mimeType != null)
            {
                RequestHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = mimeType;
            }

            KnownContentLength = payload.Length;
        }

        /// <summary>
        /// Sets the content of this HTTP request to be a HTTP formdata request using key-value pairs.
        /// </summary>
        /// <param name="postParameters">The dictionary of form data values to send</param>
        public void SetContent(IDictionary<string, string> postParameters)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (postParameters == null)
            {
                throw new ArgumentNullException("Post parameters are null");
            }

            PooledStringBuilder pooledSb = StringBuilderPool.Rent();

            try
            {
                StringBuilder builder = pooledSb.Builder;
                foreach (var kvp in postParameters)
                {
                    if (builder.Length != 0)
                    {
                        builder.Append("&");
                    }
                    builder.AppendFormat("{0}={1}", WebUtility.UrlEncode(kvp.Key), WebUtility.UrlEncode(kvp.Value));
                }

                SetContent(pooledSb, HttpConstants.MIME_TYPE_FORMDATA);
                pooledSb = null;
            }
            finally
            {
                pooledSb?.Dispose();
            }
        }

        /// <summary>
        /// Sets the content of this outgoing request to be a specified content stream with a mime-type header.
        /// </summary>
        /// <param name="outgoingStream">The stream that the framework will read from when sending outgoing content to the server</param>
        /// <param name="mimeType">The mimetype (Content-Type) of the data</param>
        public void SetContent(Stream outgoingStream, string mimeType)
        {
            SetContent(new NonRealTimeStreamWrapper(outgoingStream, ownsStream: true), mimeType);
        }

        /// <summary>
        /// Sets the content of this outgoing request to be a specified content stream with a mime-type header.
        /// </summary>
        /// <param name="outgoingStream">The stream that the framework will read from when sending outgoing content to the server</param>
        /// <param name="mimeType">The mimetype (Content-Type) of the data</param>
        public void SetContent(NonRealTimeStream outgoingStream, string mimeType)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (outgoingStream == null)
            {
                throw new ArgumentNullException("Stream content is null");
            }

            _requestStream = new HttpContentStreamWrapper(outgoingStream, ownsStream: true);
            KnownContentLength = null;
            if (mimeType != null)
            {
                RequestHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = mimeType;
            }
        }

        private static readonly JsonSerializer DEFAULT_JSON_SERIALIZER = JsonSerializer.CreateDefault(
            new JsonSerializerSettings()
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            });

        /// <summary>
        /// Sets the payload data of this request to be a particular object that will be serialized as Json, using default serializer settings
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        public void SetContentJson(object payload)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (payload == null)
            {
                throw new ArgumentNullException("Content is null");
            }

            _requestStream = new HttpContentStreamWrapper(new JsonSerializedObjectStream(payload, DEFAULT_JSON_SERIALIZER), ownsStream: true);
            RequestHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = HttpConstants.MIME_TYPE_JSON;
            KnownContentLength = null;
        }

        [Obsolete("I think you are making a mistake by setting JSON content to a string value (I would expect an object instead). Please reevaluate")]
        public void SetContentJson(string payload)
        {
            SetContent(payload, HttpConstants.MIME_TYPE_JSON);
        }

        /// <summary>
        /// Sets the payload data of this request to be a particular object that will be serialized as Json
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        /// <param name="serializer">The serializer to use</param>
        public void SetContentJson(object payload, JsonSerializer serializer)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (payload == null)
            {
                throw new ArgumentNullException("Content is null");
            }

            _requestStream = new HttpContentStreamWrapper(new JsonSerializedObjectStream(payload, serializer), ownsStream: true);
            RequestHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = HttpConstants.MIME_TYPE_JSON;
            KnownContentLength = null;
        }

        /// <summary>
        /// Reads the incoming request content as HTTP form data.
        /// </summary>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>A dictionary of form parameters from the POST body</returns>
        public async Task<HttpFormParameters> ReadContentAsFormDataAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (Direction == NetworkDirection.Outgoing)
            {
                throw new NotSupportedException("Cannot read the incoming data on an outgoing message");
            }

            // OPT probably a better way to do this by parsing in-place
            ArraySegment<byte> bytes = await ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
            return HttpHelpers.GetFormDataFromPayload(RequestHeaders, bytes);
        }

        /// <summary>
        /// Returns the incoming request content as a single contiguous array. <b>Please avoid using this method if at
        /// all possible</b>, it is very inefficient.
        /// </summary>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The entire request payload as a contiguous array.</returns>
        public async Task<ArraySegment<byte>> ReadContentAsByteArrayAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (Direction == NetworkDirection.Outgoing)
            {
                throw new NotSupportedException("Cannot read the incoming data on an outgoing message");
            }

            if (_requestStream is EmptyHttpContentStream)
            {
                return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
            }

            using (PooledBuffer<byte> pooledBuffer = BufferPool<byte>.Rent())
            {
                byte[] scratch = pooledBuffer.Buffer;
                using (RecyclableMemoryStream bucket = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                {
                    int readSize = await _requestStream.ReadAsync(scratch, 0, scratch.Length, cancelToken, realTime);
                    while (readSize > 0)
                    {
                        bucket.Write(scratch, 0, readSize);
                        readSize = await _requestStream.ReadAsync(scratch, 0, scratch.Length, cancelToken, realTime);
                    }

                    ArraySegment<byte> returnVal = new ArraySegment<byte>(bucket.ToArray());
                    return returnVal;
                }
            }
        }

        /// <summary>
        /// Returns the incoming request content decoded as a UTF8 string. <b>Please avoid using this method if at
        /// all possible</b>, it is very inefficient.
        /// </summary>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The request content as a string</returns>
        public Task<string> ReadContentAsStringAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return ReadContentAsStringAsync(StringUtils.UTF8_WITHOUT_BOM, cancelToken, realTime);
        }

        /// <summary>
        /// Returns the incoming request content decoded as a string of the specified encoding. <b>Please avoid using this method if at
        /// all possible</b>, it is very inefficient.
        /// </summary>
        /// <param name="encoding">The encoding to use for reading</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The request content as a string</returns>
        public async Task<string> ReadContentAsStringAsync(Encoding encoding, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (Direction == NetworkDirection.Outgoing)
            {
                throw new NotSupportedException("Cannot read the incoming data on an outgoing message");
            }

            using (PooledStringBuilder pooledSb = await StringUtils.ConvertStreamIntoString(
                _requestStream,
                encoding,
                cancelToken,
                realTime).ConfigureAwait(false))
            {
                return pooledSb.ToString();
            }
        }

        /// <summary>
        /// Gets the incoming content as a stream. This is assuming you are a server that is processing an incoming request.
        /// </summary>
        /// <returns></returns>
        public HttpContentStream GetIncomingContentStream()
        {
            if (Direction == NetworkDirection.Outgoing)
            {
                throw new NotSupportedException("Cannot read the incoming data on an outgoing message");
            }

            return _requestStream;
        }

        /// <summary>
        /// Used internally by client implementations. Gets the outgoing content as a stream.
        /// </summary>
        /// <returns></returns>
        public HttpContentStream GetOutgoingContentStream()
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot read the outgoing data on an incoming message");
            }

            return _requestStream;
        }

        /// <summary>
        /// Disposes of any content stream in this request and sets headers accordingly.
        /// </summary>
        public void ClearContent()
        {
            _requestStream.Dispose();
            _requestStream = EmptyHttpContentStream.Singleton;
            RequestHeaders.Remove(HttpConstants.HEADER_KEY_CONTENT_TYPE);
            KnownContentLength = null;
        }

        /// <summary>
        /// Builds the full (relative) URL string for this request, including get parameters, just as it would appear on the wire.
        /// </summary>
        /// <returns>The fully qualified URI, for example "/path/file.php?login=false&amp;test=true"</returns>
        public string BuildUri()
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                WriteUriTo(pooledSb.Builder);
                return pooledSb.Builder.ToString();
            }
        }

        /// <summary>
        /// Writes the fully qualified form of the URI to the given string builder.
        /// </summary>
        /// <param name="sb">The string builder to append to.</param>
        public void WriteUriTo(StringBuilder sb)
        {
            sb.Append(RequestFile);
            bool firstParam = !RequestFile.Contains("?");
            if (GetParameters.KeyCount > 0)
            {
                foreach (var parameter in GetParameters)
                {
                    foreach (var parameterValue in parameter.Value)
                    {
                        if (firstParam)
                        {
                            sb.Append("?");
                            firstParam = false;
                        }
                        else
                        {
                            sb.Append("&");
                        }

                        sb.Append(WebUtility.UrlEncode(parameter.Key));
                        if (!string.IsNullOrEmpty(parameterValue))
                        {
                            sb.Append("=");
                            sb.Append(WebUtility.UrlEncode(parameterValue));
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _requestStream?.Dispose();
            }
        }

        /// <summary>
        /// Builds an outgoing HTTP request with the specified target path (encoded) and HTTP method.
        /// </summary>
        /// <param name="urlString">The encoded URL path of the request</param>
        /// <param name="method">The HTTP verb to use for this request.</param>
        /// <returns>A newly generated HTTP request.</returns>
        public static HttpRequest CreateOutgoing(string urlString, string method = HttpConstants.HTTP_VERB_GET)
        {
            int startOfRequest = 0;
            if (urlString.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                startOfRequest = 7;
            }
            if (urlString.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                startOfRequest = 8;
            }

            // Find the relative portion of the URL beginning with the first / slash
            string relativeUrl;
            startOfRequest = urlString.IndexOf('/', startOfRequest);
            if (startOfRequest >= 0)
            {
                relativeUrl = urlString.Substring(startOfRequest);
            }
            else
            {
                // URL is a form like http://www.com with no trailing slash, so we will parse this as though it were "/"
                relativeUrl = "/";
            }

            string basePath;
            string fragment;
            HttpFormParameters queryParams;
            if (!HttpHelpers.TryParseRelativeUrl(
                relativeUrl,
                out basePath,
                out queryParams,
                out fragment,
                treatQueryParamNamesAsCaseSensitive: true))
            {
                throw new FormatException(string.Format("Failed to parse URL {0}", urlString));
            }

            return new HttpRequest(NetworkDirection.Outgoing, basePath, method, queryParams, fragment);
        }
    }
}
