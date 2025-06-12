using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// HTTP server context backed by an HttpListener server (and wrapping the context provided by that).
    /// </summary>
    public class ListenerHttpServerContext : IHttpServerContext, IDisposable
    {
        private readonly HttpListenerContext _listenerContext;
        private readonly DateTimeOffset _requestStartTime;
        private int _disposed = 0;

        public ListenerHttpServerContext(HttpListenerContext listenerContext, IRealTimeProvider realTime)
        {
            _listenerContext = listenerContext.AssertNonNull(nameof(listenerContext));
            _requestStartTime = realTime.AssertNonNull(nameof(realTime)).Time;
            HttpRequest = ConvertListenerRequestToDurandalRequest(listenerContext.Request);
            CurrentProtocolVersion = HttpVersion.FromVersion(listenerContext.Request.ProtocolVersion);
            PrimaryResponseStarted = false;
            PrimaryResponseFinished = false;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ListenerHttpServerContext()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc/>
        public HttpVersion CurrentProtocolVersion
        {
            get; private set;
        }

        /// <inheritdoc/>
        public bool SupportsWebSocket => true;

        /// <inheritdoc/>
        public bool SupportsServerPush => false;

        /// <inheritdoc/>
        public bool SupportsTrailers => false;

        /// <inheritdoc/>
        public bool PrimaryResponseStarted { get; private set; }

        /// <inheritdoc/>
        public bool PrimaryResponseFinished { get; private set; }

        /// <inheritdoc/>
        public HttpRequest HttpRequest
        {
            get;
            private set;
        }

        /// <inheritdoc/>
        public async Task<IWebSocket> AcceptWebsocketUpgrade(CancellationToken cancelToken, IRealTimeProvider realTime, string subProtocol = null)
        {
            // AcceptWebSocketAsync will generate the appropriate HTTP response message back to the client,
            // either 101 Switching Protocols or some other error response.
            PrimaryResponseStarted = true;
            HttpListenerWebSocketContext socketContext = await _listenerContext.AcceptWebSocketAsync(subProtocol).ConfigureAwait(false);
            PrimaryResponseFinished = true;
            return new SystemWebSocketWrapper(socketContext.WebSocket);
        }

        /// <inheritdoc/>
        public async Task WritePrimaryResponse(HttpResponse response, ILogger traceLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (ReferenceEquals(response.GetOutgoingContentStream(), HttpRequest.GetIncomingContentStream()))
            {
                throw new InvalidOperationException("You can't pipe an HTTP input directly back to HTTP output");
            }

            try
            {
                PrimaryResponseStarted = true;
                _listenerContext.Response.StatusCode = response.ResponseCode;
                _listenerContext.Response.StatusDescription = response.ResponseMessage;
                _listenerContext.Response.KeepAlive = CurrentProtocolVersion == HttpVersion.HTTP_1_1;
                foreach (var headerKvp in response.ResponseHeaders)
                {
                    foreach (string singleHeaderValue in headerKvp.Value)
                    {
                        if (headerKvp.Key.Equals(HttpConstants.HEADER_KEY_CONTENT_LENGTH, StringComparison.OrdinalIgnoreCase))
                        {
                            traceLogger.Log("Content-Length should not be set manually on an HTTP message; removing header", LogLevel.Wrn);
                        }
                        else if (headerKvp.Key.Equals(HttpConstants.HEADER_KEY_CONTENT_TYPE, StringComparison.OrdinalIgnoreCase))
                        {
                            _listenerContext.Response.ContentType = singleHeaderValue;
                        }
                        else if (headerKvp.Key.Equals(HttpConstants.HEADER_KEY_TRAILER, StringComparison.OrdinalIgnoreCase))
                        {
                            traceLogger.Log("Trailers are not supported in Listener HTTP server; removing header", LogLevel.Wrn);
                        }
                        else if (headerKvp.Key.Equals(HttpConstants.HEADER_KEY_CONNECTION, StringComparison.OrdinalIgnoreCase))
                        {
                            // There's some nuance here (HTTP/1.0 backwards compatibility)
                            // Http 1.0 assumes persistence is false unless Connection: keep-alive is set
                            // Http 1.1 assumes persistence is true unless Connection: close is set
                            if (singleHeaderValue.Equals(HttpConstants.HEADER_VALUE_CONNECTION_CLOSE, StringComparison.OrdinalIgnoreCase))
                            {
                                _listenerContext.Response.KeepAlive = false;
                            }
                            else if (singleHeaderValue.Equals(HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE, StringComparison.OrdinalIgnoreCase))
                            {
                                _listenerContext.Response.KeepAlive = true;
                            }
                        }
                        else
                        {
                            _listenerContext.Response.AddHeader(headerKvp.Key, singleHeaderValue);
                        }
                    }
                }

                // Generate instrumentation header
                TimeSpan totalRequestTime = realTime.Time - _requestStartTime;
                _listenerContext.Response.AddHeader(HttpConstants.HEADER_KEY_SERVER_WORK_TIME, totalRequestTime.PrintTimeSpan());

                using (HttpContentStream streamedResponse = response.GetOutgoingContentStream())
                {
                    if (streamedResponse != null && !(streamedResponse is EmptyHttpContentStream))
                    {
                        using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
                        {
                            if (response.KnownContentLength.HasValue)
                            {
                                _listenerContext.Response.ContentLength64 = response.KnownContentLength.Value;
                                _listenerContext.Response.SendChunked = false;

                                int readSize = 1;
                                while (readSize > 0)
                                {
                                    readSize = await streamedResponse.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length, cancelToken, realTime).ConfigureAwait(false);
                                    if (readSize > 0)
                                    {
                                        await _listenerContext.Response.OutputStream.WriteAsync(scratch.Buffer, 0, readSize, cancelToken).ConfigureAwait(false);
                                    }
                                }
                            }
                            else if (CurrentProtocolVersion == HttpVersion.HTTP_1_1)
                            {
                                // Copy from durandal response stream to listener response stream using chunked transfer
                                _listenerContext.Response.SendChunked = true;

                                int readSize = 1;
                                while (readSize > 0)
                                {
                                    readSize = await streamedResponse.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length, cancelToken, realTime).ConfigureAwait(false);
                                    if (readSize > 0)
                                    {
                                        await _listenerContext.Response.OutputStream.WriteAsync(scratch.Buffer, 0, readSize, cancelToken).ConfigureAwait(false);
                                    }
                                }
                            }
                            else
                            {
                                // We have to buffer the entire response manually because of HTTP 1.0 compatibility.
                                _listenerContext.Response.SendChunked = false;

                                using (RecyclableMemoryStream outputBuffer = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                                {
                                    int readSize = 1;
                                    while (readSize > 0)
                                    {
                                        readSize = await streamedResponse.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length, cancelToken, realTime).ConfigureAwait(false);
                                        if (readSize > 0)
                                        {
                                            outputBuffer.Write(scratch.Buffer, 0, readSize);
                                        }
                                    }

                                    _listenerContext.Response.ContentLength64 = outputBuffer.Length;
                                    outputBuffer.Position = 0;

                                    readSize = 1;
                                    while (readSize > 0)
                                    {
                                        readSize = outputBuffer.Read(scratch.Buffer, 0, scratch.Buffer.Length);
                                        if (readSize > 0)
                                        {
                                            await _listenerContext.Response.OutputStream.WriteAsync(scratch.Buffer, 0, readSize, cancelToken).ConfigureAwait(false);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                PrimaryResponseFinished = true;
            }
            finally
            {
                // If the provider of this response got it by making an outgoing request and piping the response to us,
                // then we need to close that response stream here
                if (response.Direction == NetworkDirection.Proxied)
                {
                    await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                }

                response?.Dispose();
            }
        }

        /// <inheritdoc/>
        public Task WritePrimaryResponse(
            HttpResponse response,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            IReadOnlyCollection<string> trailerNames,
            Func<string, Task<string>> trailerDelegate)
        {
            if (trailerNames != null && trailerNames.Count > 0)
            {
                traceLogger.Log("HTTP response trailers are not supported by listener servers. Trailers will not be sent.", LogLevel.Wrn);
            }

            return WritePrimaryResponse(response, traceLogger, cancelToken, realTime);
        }

        /// <inheritdoc/>
        public void PushPromise(
            string expectedRequestMethod,
            string expectedPath,
            HttpHeaders expectedRequestHeaders,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            throw new NotSupportedException("Server push is not implemented for HttpListener servers");
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
                HttpRequest?.Dispose();
            }
        }

        private static HttpRequest ConvertListenerRequestToDurandalRequest(HttpListenerRequest clientRequest)
        {
            IHttpHeaders convertedHeaders = new ListenerRequestHeaders(clientRequest.Headers);

            string requestFile;
            string fragment;
            HttpFormParameters parsedGetParams;
            if (!HttpHelpers.TryParseRelativeUrl(
                clientRequest.RawUrl,
                out requestFile,
                out parsedGetParams,
                out fragment))
            {
                throw new FormatException(string.Format("Cannot parse URL {0}", clientRequest.RawUrl));
            }

            HttpRequest returnVal = HttpRequest.CreateIncoming(
                convertedHeaders,
                requestFile,
                clientRequest.HttpMethod,
                clientRequest.RemoteEndPoint.ToString(),
                parsedGetParams,
                fragment,
                new HttpContentStreamWrapper(clientRequest.InputStream, ownsStream: true),
                HttpVersion.FromVersion(clientRequest.ProtocolVersion));
            returnVal.RemoteHost = clientRequest.RemoteEndPoint.ToString();
            return returnVal;
        }
    }
}
