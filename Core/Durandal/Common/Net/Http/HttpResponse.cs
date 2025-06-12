namespace Durandal.Common.Net.Http
{
    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using Durandal.Common.IO;
    using Utils;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Globalization;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using System.Diagnostics;
    using Newtonsoft.Json;
    using Durandal.Common.IO.Json;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// Represents an abstract HTTP protocol response (independent of any specific protocol version, including HTTP/2).
    /// This class is used in two contexts - one is as a server creating a response to write outbound to the socket, and
    /// the other is as a client reading an incoming response from the socket. The difference between these contexts is determined
    /// by the <see cref="Direction"/> property. Based on this property, different methods and accessors are enabled or disabled.
    /// For example, it makes no sense to "set content" on an incoming response, so this is disallowed. Likewise for
    /// reading content on an outgoing response. This distinction is also enforced in the constructors
    /// <see cref="CreateIncoming(int, string, HttpHeaders, HttpContentStream, IHttpClientContext)"/> and <see cref="CreateOutgoing()"/>.
    /// </summary>
    public sealed class HttpResponse : IDisposable
    {
        /// <summary>
        /// The HTTP status code e.g. 200
        /// </summary>
        public int ResponseCode { get; set; }

        /// <summary>
        /// The short HTTP message string e.g. "Not Found". NOT the response body
        /// </summary>
        public string ResponseMessage { get; set; }

        /// <summary>
        /// All http headers found in response.
        /// Dictionary operations are case-insensitive.
        /// </summary>
        public IHttpHeaders ResponseHeaders { get; private set; }

        /// <summary>
        /// All http trailers found in response (assuming you are a client).
        /// This collection is not guaranteed to be non-null! If trailers are supported, and were actually sent by the
        /// server, AND the full response content body has finished being read, this field _may_ be set to a non-null value.
        /// At that point, you can access them in the same way that you would regular headers.
        /// </summary>
        public HttpHeaders ResponseTrailers => _responseStream.Trailers;

        /// <summary>
        /// Represents the direction that this request is moving relative to the current code.
        /// </summary>
        public NetworkDirection Direction { get; private set; }

        /// <summary>
        /// If this response's body content has a known fixed length; it will be available here.
        /// </summary>
        public long? KnownContentLength { get; private set; }

        /// <summary>
        /// The protocol version used for this response. This value is only set
        /// if you are a client that is receiving the response - meaning it
        /// has already been sent across the wire using a specific protocol.
        /// Otherwise it is null.
        /// </summary>
        public HttpVersion ProtocolVersion { get; private set; }

        /// <summary>
        /// The actual body of the response (whether it's a fixed block of data or a transfer stream) is stored here.
        /// Can never be null (the default value is an empty stream with no data).
        /// </summary>
        private HttpContentStream _responseStream;

        /// <summary>
        /// Associates this HTTP response with external resources in a larger context (for example, a network socket,
        /// ASP HttpRequestMessage, HTTP/2 connection context, etc.), this is the handle to that context. The user
        /// of HttpResponse will have to call FinishAsync() before disposal to ensure that the context can do what
        /// it needs to remain consistent (for example, keep lingering connections open and return them to a pool somewhere).
        /// </summary>
        private readonly IHttpClientContext _httpClientContext;

        private int _finished = 0;
        private int _disposed = 0;

        public static HttpResponse CreateOutgoing()
        {
            return new HttpResponse(NetworkDirection.Outgoing, new HttpHeaders());
        }

        public static HttpResponse CreateOutgoing(HttpHeaders headers)
        {
            return new HttpResponse(NetworkDirection.Outgoing, headers);
        }

        /// <summary>
        /// Creates a fully formed incoming HTTP response from well-defined inputs. Used primarily for internal parsers.
        /// </summary>
        /// <param name="responseCode"></param>
        /// <param name="responseMessage"></param>
        /// <param name="headers"></param>
        /// <param name="responseDataStream"></param>
        /// <param name="httpClientContext"></param>
        public static HttpResponse CreateIncoming(
            int responseCode,
            string responseMessage,
            HttpHeaders headers,
            HttpContentStream responseDataStream,
            IHttpClientContext httpClientContext)
        {
            return new HttpResponse(NetworkDirection.Incoming, responseCode, responseMessage, headers, responseDataStream, httpClientContext);
        }

        /// <summary>
        /// Constructor for outgoing responses (e.g. you are the server).
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="headers"></param>
        private HttpResponse(NetworkDirection direction, HttpHeaders headers)
        {
            ResponseHeaders = headers;
            ResponseCode = 0;
            ResponseMessage = string.Empty;
            _httpClientContext = null;
            _responseStream = EmptyHttpContentStream.Singleton;
            Direction = direction;
            ProtocolVersion = null;
            KnownContentLength = null;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        /// <summary>
        /// Constructor for incoming responses (e.g. you are the client)
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="responseCode"></param>
        /// <param name="responseMessage"></param>
        /// <param name="headers"></param>
        /// <param name="responseDataStream"></param>
        /// <param name="httpClientContext"></param>
        private HttpResponse(
            NetworkDirection direction,
            int responseCode,
            string responseMessage,
            HttpHeaders headers,
            HttpContentStream responseDataStream,
            IHttpClientContext httpClientContext)
        {
            ResponseHeaders = headers;
            ResponseCode = responseCode;
            ResponseMessage = responseMessage.AssertNonNull(nameof(responseMessage));
            _httpClientContext = httpClientContext.AssertNonNull(nameof(httpClientContext));
            _responseStream = responseDataStream.AssertNonNull(nameof(responseDataStream));
            Direction = direction;
            ProtocolVersion = _httpClientContext.ProtocolVersion.AssertNonNull("protocolVersion");
            KnownContentLength = null;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~HttpResponse()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Used internally by server implementations.
        /// </summary>
        /// <returns></returns>
        public HttpContentStream GetOutgoingContentStream()
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot fetch outgoing content stream of incoming response");
            }

            return _responseStream;
        }

        /// <summary>
        /// Sets the NetworkDirection on this response to be Proxied. This implies that you are a
        /// proxy server and you have a response from downstream that you want to route back to your caller
        /// directly. You are responsible for disposing of everything properly afterwards.
        /// </summary>
        public void MakeProxied()
        {
            Direction = NetworkDirection.Proxied;
        }

        /// <summary>
        /// Sets the payload data of this response to be a particular stream.
        /// This http response will take ownership of the stream.
        /// </summary>
        /// <param name="stream">The payload to set.</param>
        /// <param name="mimeType">The value to use for the Content-Type header</param>
        public void SetContent(Stream stream, string mimeType = null)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (stream == null)
            {
                throw new ArgumentNullException("Content is null");
            }

            SetContent(new NonRealTimeStreamWrapper(stream, true), mimeType);
            KnownContentLength = null;
        }

        /// <summary>
        /// Sets the payload data of this response to be a particular stream.
        /// This http response will take ownership of the stream.
        /// </summary>
        /// <param name="stream">The payload to set.</param>
        /// <param name="mimeType">The value to use for the Content-Type header</param>
        public void SetContent(NonRealTimeStream stream, string mimeType = null)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (stream == null)
            {
                throw new ArgumentNullException("Content stream is null");
            }

            // Dispose of old stream if we have set it multiple times
            _responseStream?.Dispose();

            _responseStream = new HttpContentStreamWrapper(stream, ownsStream: true);
            KnownContentLength = null;
            if (!string.IsNullOrEmpty(mimeType))
            {
                ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = mimeType;
            }
        }

        /// <summary>
        /// Sets the payload data of this response to be a particular stream.
        /// This http response will take ownership of the stream.
        /// This method header will typically be seen in proxy servers where the response from one request gets
        /// shifted over the response of another request.
        /// </summary>
        /// <param name="stream">The payload to set. MUST NOT BE AN HTTP REQUEST STREAM (like for an echo server). It won't work.</param>
        /// <param name="mimeType">The value to use for the Content-Type header</param>
        public void SetContent(HttpContentStream stream, string mimeType = null)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (stream == null)
            {
                throw new ArgumentNullException("Content stream is null");
            }

            // Dispose of old stream if we have set it multiple times
            _responseStream?.Dispose();
            _responseStream = stream;
            KnownContentLength = null;
            if (!string.IsNullOrEmpty(mimeType))
            {
                ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = mimeType;
            }
        }

        /// <summary>
        /// Used to create a wrapper around the outgoing stream on a response.
        /// Works kind of like the "filter stream" concept in ASP.
        /// The existing stream is not disposed because it is assumed to live on within the wrapper.
        /// </summary>
        /// <param name="wrapperFactory">A function which accepts the current outgoing stream and returns a newly generated wrapper stream</param>
        /// <param name="invalidatesContentLength">Whether to invalidate the Content-Length header, if present.
        /// This is intended for if your wrapper does something like compresses the content to be a different size.</param>
        public void WrapOutgoingContentStream(Func<HttpContentStream, HttpContentStream> wrapperFactory, bool invalidatesContentLength = false)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            wrapperFactory.AssertNonNull(nameof(wrapperFactory));
            _responseStream = wrapperFactory(_responseStream);

            if (invalidatesContentLength)
            {
                KnownContentLength = null;
            }
        }

        /// <summary>
        /// Writes a dictionary of values as an x-www-form-urlencoded payload, replacing any existing payload data
        /// </summary>
        /// <param name="postParameters">The dictionary of parameters to set. Do not URL encode!</param>
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

                    // OPT wasteful conversion. UrlEncodeToBytes would be slightly more efficient but still trigger allocations
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
        /// Sets the payload data of this response to be a particular UTF-8 string.
        /// </summary>
        /// <param name="content">The content to set.</param>
        public void SetContent(string content)
        {
            SetContent(content, HttpConstants.MIME_TYPE_UTF8_TEXT);
        }

        /// <summary>
        /// Sets the payload data of this response to be a particular string with the given content type
        /// </summary>
        /// <param name="content">The content to set.</param>
        /// <param name="mimeType">The value to set for the Content-Type header</param>
        public void SetContent(string content, string mimeType = null)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (content == null)
            {
                throw new ArgumentNullException("Content is null");
            }

            StringStream stream = new StringStream(content, StringUtils.UTF8_WITHOUT_BOM);
            SetContent(stream, mimeType);
            KnownContentLength = stream.Length;
        }

        /// <summary>
        /// Sets the payload data of this response to be a particular pooled string builder with the given content type.
        /// Ownership of the pooled object is tranferred to this object.
        /// </summary>
        /// <param name="content">The content to set.</param>
        /// <param name="mimeType">The value to set for the Content-Type header</param>
        public void SetContent(PooledStringBuilder content, string mimeType = null)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (content == null)
            {
                throw new ArgumentNullException("Content is null");
            }

            PooledStringBuilderStream stream = new PooledStringBuilderStream(content, StringUtils.UTF8_WITHOUT_BOM);
            SetContent(stream, mimeType);
            KnownContentLength = stream.Length;
        }

        /// <summary>
        /// Sets the payload data of this response to be a particular byte array.
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        /// <param name="mimeType">The value to set for the Content-Type header</param>
        public void SetContent(byte[] payload, string mimeType = null)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (payload == null)
            {
                throw new ArgumentNullException("Content is null");
            }

            _responseStream = new HttpContentStreamWrapper(new MemoryStream(payload, writable: false), ownsStream: true);
            KnownContentLength = payload.Length;
            if (!string.IsNullOrEmpty(mimeType))
            {
                ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = mimeType;
            }
        }

        /// <summary>
        /// Sets the payload data of this response to be a particular byte array segment.
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        /// <param name="mimeType">The value to set for the Content-Type header</param>
        public void SetContent(ArraySegment<byte> payload, string mimeType = null)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (payload == null)
            {
                throw new ArgumentNullException("Content is null");
            }

            _responseStream = new HttpContentStreamWrapper(new MemoryStream(payload.Array, payload.Offset, payload.Count, writable: false), ownsStream: true);
            KnownContentLength = payload.Count;
            if (!string.IsNullOrEmpty(mimeType))
            {
                ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = mimeType;
            }
        }

        /// <summary>
        /// Sets the payload data of this response to be a particular pooled byte buffer.
        /// </summary>
        /// <param name="payload">The payload to set.</param>
        /// <param name="mimeType">The value to set for the Content-Type header</param>
        public void SetContent(PooledBuffer<byte> payload, string mimeType = null)
        {
            if (Direction == NetworkDirection.Incoming)
            {
                throw new NotSupportedException("Cannot set the outgoing data on an incoming message");
            }

            if (payload == null || payload.Buffer == null)
            {
                throw new ArgumentNullException("Content is null");
            }

            _responseStream = new HttpContentStreamWrapper(new PooledBufferMemoryStream(payload), ownsStream: true);
            KnownContentLength = payload.Length;
            if (!string.IsNullOrEmpty(mimeType))
            {
                ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = mimeType;
            }
        }

        private static readonly JsonSerializer DEFAULT_JSON_SERIALIZER = JsonSerializer.CreateDefault(
            new JsonSerializerSettings()
            {
                Formatting = Formatting.None,
                NullValueHandling = NullValueHandling.Ignore
            });

        /// <summary>
        /// Sets the payload data of this response to be a particular object that will be serialized as Json, using default serializer settings
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

            _responseStream = new HttpContentStreamWrapper(new JsonSerializedObjectStream(payload, DEFAULT_JSON_SERIALIZER), ownsStream: true);
            ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = HttpConstants.MIME_TYPE_JSON;
            KnownContentLength = null;
        }

        [Obsolete("I think you are making a mistake by setting JSON content to a string value (I would expect an object instead). Please reevaluate")]
        public void SetContentJson(string payload)
        {
            SetContent(payload, HttpConstants.MIME_TYPE_JSON);
        }

        /// <summary>
        /// Sets the payload data of this response to be a particular object that will be serialized as Json
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

            _responseStream = new HttpContentStreamWrapper(new JsonSerializedObjectStream(payload, serializer), ownsStream: true);
            ResponseHeaders[HttpConstants.HEADER_KEY_CONTENT_TYPE] = HttpConstants.MIME_TYPE_JSON;
            KnownContentLength = null;
        }

        public void ClearContent()
        {
            _responseStream.Dispose();
            _responseStream = EmptyHttpContentStream.Singleton;
            ResponseHeaders.Remove(HttpConstants.HEADER_KEY_CONTENT_TYPE);
            KnownContentLength = null;
        }

        /// <summary>
        /// Decodes post form-data from the payload and parses it as a nice parameter collection
        /// </summary>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns></returns>
        public async Task<HttpFormParameters> ReadContentAsFormDataAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ArraySegment<byte> binary = await ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
            return HttpHelpers.GetFormDataFromPayload(ResponseHeaders, binary);
        }

        /// <summary>
        /// Returns the payload decoded as a UTF8 string. <b>Please avoid using this method if at
        /// all possible</b>, it is very inefficient.
        /// </summary>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The content as a string.</returns>
        public Task<string> ReadContentAsStringAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return ReadContentAsStringAsync(StringUtils.UTF8_WITHOUT_BOM, cancelToken, realTime);
        }

        /// <summary>
        /// Returns the payload as a string decoded with given encoding. <b>Please avoid using this method if at
        /// all possible</b>, it is very inefficient.
        /// </summary>
        /// <param name="encoding">The string encoding to use</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns></returns>
        public async Task<string> ReadContentAsStringAsync(Encoding encoding, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (Direction == NetworkDirection.Outgoing)
            {
                throw new NotSupportedException("Cannot read the incoming data on an outgoing message");
            }

            using (PooledStringBuilder pooledSb = await StringUtils.ConvertStreamIntoString(_responseStream, encoding, cancelToken, realTime))
            {
                _responseStream.Dispose();
                return pooledSb.Builder.ToString();
            }
        }

        public Task<T> ReadContentAsJsonObjectAsync<T>(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return ReadContentAsJsonObjectAsync<T>(DEFAULT_JSON_SERIALIZER, cancelToken, realTime);
        }

        public async Task<T> ReadContentAsJsonObjectAsync<T>(JsonSerializer serializer, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (Direction == NetworkDirection.Outgoing)
            {
                throw new NotSupportedException("Cannot read the incoming data on an outgoing message");
            }

            //using (StreamReader reader = new StreamReader(_responseStream, StringUtils.UTF8_WITHOUT_BOM))
            //using (JsonTextReader jsonReader = new JsonTextReader(reader))
            //{
            //    return Task.FromResult<T>(serializer.Deserialize<T>(jsonReader));
            //}

            using (RecyclableMemoryStream dataBuffer = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
            {
                // Working around Json.Net's apparent lack of proper async deserialization by prebuffering the entire response....
                // FIXME there has got be a better way!
                int readSize = 1;
                while (readSize > 0)
                {
                    readSize = await _responseStream.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length, cancelToken, realTime).ConfigureAwait(false);
                    if (readSize > 0)
                    {
                        dataBuffer.Write(scratch.Buffer, 0, readSize);
                    }
                }

                dataBuffer.Seek(0, SeekOrigin.Begin);

                using (StreamReader reader = new StreamReader(dataBuffer, StringUtils.UTF8_WITHOUT_BOM))
                using (JsonTextReader jsonReader = new JsonTextReader(reader))
                {
                    return serializer.Deserialize<T>(jsonReader);
                }
            }
        }

        public HttpContentStream ReadContentAsStream()
        {
            if (Direction == NetworkDirection.Outgoing)
            {
                throw new NotSupportedException("Cannot read the incoming data on an outgoing message");
            }

            // no need for async since we just return the handle of the stream
            return _responseStream;
        }

        /// <summary>
        /// Returns the payload as a single contiguous array.
        /// <b>Please avoid using this method for any large payloads as it is much less efficient than a stream.</b>
        /// </summary>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The response content as a byte array</returns>
        public async Task<ArraySegment<byte>> ReadContentAsByteArrayAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (Direction == NetworkDirection.Outgoing)
            {
                throw new NotSupportedException("Cannot read the incoming data on an outgoing message");
            }

            if (_responseStream is EmptyHttpContentStream)
            {
                return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
            }

            using (PooledBuffer<byte> pooledBuffer = BufferPool<byte>.Rent())
            {
                byte[] scratch = pooledBuffer.Buffer;
                using (RecyclableMemoryStream bucket = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                {
                    int readSize = await _responseStream.ReadAsync(scratch, 0, scratch.Length, cancelToken, realTime);
                    while (readSize > 0)
                    {
                        bucket.Write(scratch, 0, readSize);
                        readSize = await _responseStream.ReadAsync(scratch, 0, scratch.Length, cancelToken, realTime);
                    }

                    ArraySegment<byte> returnVal = new ArraySegment<byte>(bucket.ToArray());
                    return returnVal;
                }
            }
        }

        /// <summary>
        /// This method should be called by all users of the HttpResponse before disposing of it. Doing so
        /// will clean up managed or pooled resources such as sockets, listener contexts, etc.
        /// Consider this like an async disposal method.
        /// </summary>
        /// <returns>An async task</returns>
        public Task FinishAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (!AtomicOperations.ExecuteOnce(ref _finished) ||
                _httpClientContext == null)
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            return _httpClientContext.FinishAsync(this, cancelToken, realTime);
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
                try
                {
                    if (_httpClientContext != null)
                    {
                        if (AtomicOperations.ExecuteOnce(ref _finished))
                        {
#if NETFRAMEWORK
                            Console.WriteLine("HTTP response is being disposed without having FinishAsync() called. This is not optimal.");
#endif
                            Debug.WriteLine("HTTP response is being disposed without having FinishAsync() called. This is not optimal.");

                            // Synchronous block...
                            _httpClientContext.FinishAsync(this, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                        }
                    }

                    _responseStream.Dispose();
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error while disposing of HTTP response: " + e.Message);
                }
            }
        }

        public static HttpResponse ContinueResponse()
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 100;
            returnVal.ResponseMessage = "Continue";
            return returnVal;
        }

        public static HttpResponse OKResponse()
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 200;
            returnVal.ResponseMessage = "OK";
            return returnVal;
        }

        public static HttpResponse AcceptedResponse()
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 202;
            returnVal.ResponseMessage = "Accepted";
            return returnVal;
        }

        public static HttpResponse NoContentResponse()
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 204;
            returnVal.ResponseMessage = "No Content";
            return returnVal;
        }

        public static HttpResponse BadRequestResponse()
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 400;
            returnVal.ResponseMessage = "Bad Request";
            return returnVal;
        }

        public static HttpResponse BadRequestResponse(string message)
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 400;
            returnVal.ResponseMessage = "Bad Request";
            returnVal.SetContent(message);
            return returnVal;
        }

        public static HttpResponse BadRequestResponse(Exception error)
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 400;
            returnVal.ResponseMessage = "Bad Request";
            PooledStringBuilder errorBuilder = StringBuilderPool.Rent();
            errorBuilder.Builder.Append("The request was invalid. Error message: ");
            error.PrintToStringBuilder(errorBuilder.Builder);
            returnVal.SetContent(errorBuilder, HttpConstants.MIME_TYPE_UTF8_TEXT); // Ownership of the pooled stringbuilder is transferred
            return returnVal;
        }

        public static HttpResponse NotAuthorizedResponse()
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 401;
            returnVal.ResponseMessage = "Unauthorized";
            return returnVal;
        }

        public static HttpResponse NotFoundResponse()
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 404;
            returnVal.ResponseMessage = "Not Found";
            return returnVal;
        }

        public static HttpResponse TooManyRequestsResponse()
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 429;
            returnVal.ResponseMessage = "Too Many Requests";
            return returnVal;
        }

        public static HttpResponse ServerErrorResponse()
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 500;
            returnVal.ResponseMessage = "Server Error";
            returnVal.SetContent("A server error occurred.");
            return returnVal;
        }

        public static HttpResponse ServerErrorResponse(string errorMessage)
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 500;
            returnVal.ResponseMessage = "Server Error";
            PooledStringBuilder errorBuilder = StringBuilderPool.Rent();
            errorBuilder.Builder.Append("A server error occurred. Error message: ");
            errorBuilder.Builder.Append(errorMessage);
            returnVal.SetContent(errorBuilder, HttpConstants.MIME_TYPE_UTF8_TEXT); // Ownership of the pooled stringbuilder is transferred
            return returnVal;
        }

        public static HttpResponse ServerErrorResponse(Exception error)
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 500;
            returnVal.ResponseMessage = "Server Error";
            PooledStringBuilder errorBuilder = StringBuilderPool.Rent();
            errorBuilder.Builder.Append("A server error occurred. Error message: ");
            error.PrintToStringBuilder(errorBuilder.Builder);
            returnVal.SetContent(errorBuilder, HttpConstants.MIME_TYPE_UTF8_TEXT); // Ownership of the pooled stringbuilder is transferred
            return returnVal;
        }

        public static HttpResponse RedirectResponse(string redirectUrl)
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 303;
            returnVal.ResponseMessage = "See Other";
            returnVal.ResponseHeaders[HttpConstants.HEADER_KEY_LOCATION] = redirectUrl;
            return returnVal;
        }

        public static HttpResponse RedirectResponse(Uri redirectUrl)
        {
            return RedirectResponse(redirectUrl.AbsoluteUri);
        }

        public static HttpResponse NotModifiedResponse()
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 304;
            returnVal.ResponseMessage = "Not Modified";
            return returnVal;
        }

        /// <summary>
        /// Response code 419 is not in any formal spec, but it seems useful for some cases where an error
        /// happens specifically on a client and the server is not involved.
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public static HttpResponse ClientErrorResponse(string error = "A client error occurred")
        {
            return CreateIncoming(
                419,
                "Client Error",
                new HttpHeaders(),
                new HttpContentStreamWrapper(new StringStream(error ?? string.Empty, StringUtils.UTF8_WITHOUT_BOM), true),
                NullHttpClientContext.Singleton);
        }

        public static HttpResponse MethodNotAllowedResponse(string error = "Method not allowed")
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 405;
            returnVal.ResponseMessage = "Method Not Allowed";
            returnVal.SetContent(error ?? string.Empty);
            return returnVal;
        }

        public static HttpResponse SwitchingProtocolsResponse(string protocolName)
        {
            HttpResponse returnVal = CreateOutgoing();
            returnVal.ResponseCode = 101;
            returnVal.ResponseMessage = "Switching Protocols";
            returnVal.ResponseHeaders[HttpConstants.HEADER_KEY_CONNECTION] = "Upgrade";
            returnVal.ResponseHeaders[HttpConstants.HEADER_KEY_UPGRADE] = protocolName;
            return returnVal;
        }
    }
}
