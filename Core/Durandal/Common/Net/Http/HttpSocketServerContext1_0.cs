using Durandal.Common.Cache;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Implementation of HTTP 1.0 protocol operating on a socket
    /// </summary>
    public class SocketHttpServerContext1_0 : IHttpServerContext
    {
        private readonly WeakPointer<ISocket> _socket;
        private readonly ILogger _logger;
        private readonly DateTimeOffset _requestStartTime;

        /// <inheritdoc/>
        public HttpVersion CurrentProtocolVersion => HttpVersion.HTTP_1_0;

        /// <inheritdoc/>
        public bool SupportsWebSocket => false;

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

        public bool DidRequestSpecifyKeepAlive
        {
            get;
            private set;
        }

        public SocketHttpServerContext1_0(WeakPointer<ISocket> socket, ILogger logger, IRealTimeProvider realTime)
        {
            _socket = socket.AssertNonNull(nameof(socket));
            _logger = logger.AssertNonNull(nameof(logger));
            _requestStartTime = realTime.AssertNonNull(nameof(realTime)).Time;
            DidRequestSpecifyKeepAlive = false;
            PrimaryResponseStarted = false;
            PrimaryResponseFinished = false;
        }

        /// <inheritdoc/>
        public Task<IWebSocket> AcceptWebsocketUpgrade(CancellationToken cancelToken, IRealTimeProvider realTime, string subProtocol = null)
        {
            throw new NotSupportedException("Websocket is not supported over HTTP 1.0 protocol");
        }

        /// <summary>
        /// Reads from the socket until we read all of the incoming headers and are ready to begin reading body content (if any).
        /// </summary>
        /// <returns></returns>
        public async Task ReadIncomingRequestHeaders(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpRequest = await HttpHelpers.ReadRequestFromSocket(
                _socket.Value,
                HttpVersion.HTTP_1_0,
                _logger,
                cancelToken,
                realTime).ConfigureAwait(false);

            // Check if the request contained an explicit Connection: keep-alive operative
            string headerValue;
            DidRequestSpecifyKeepAlive =
                HttpRequest.RequestHeaders.TryGetValue(HttpConstants.HEADER_KEY_CONNECTION, out headerValue) &&
                string.Equals(HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE, headerValue, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public async Task FinishReadingEntireRequest(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            NonRealTimeStream incomingRequestStream = HttpRequest.GetIncomingContentStream();
            if (incomingRequestStream == null)
            {
                throw new NullReferenceException("Somehow the incoming request HTTP request stream got set to null; this shouldn't be possible");
            }

            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent())
            {
                int readSize = 1;
                while (readSize != 0)
                {
                    readSize = await incomingRequestStream.ReadAsync(scratch.Buffer, 0, scratch.Buffer.Length, cancelToken, realTime).ConfigureAwait(false);
                }
            }
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
                // Set the connection header depending on the socket state
                // Use the HTTP/1.0 "unofficial" extension to turn on persistence by default
                if (!response.ResponseHeaders.ContainsKey(HttpConstants.HEADER_KEY_CONNECTION))
                {
                    response.ResponseHeaders[HttpConstants.HEADER_KEY_CONNECTION] = HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE;
                }

                // Generate instrumentation header
                TimeSpan totalRequestTime = realTime.Time - _requestStartTime;
                response.ResponseHeaders.Add(HttpConstants.HEADER_KEY_SERVER_WORK_TIME, totalRequestTime.PrintTimeSpan());

                PrimaryResponseStarted = true;
                // TODO If the caller verb was HEAD, skip sending the payload
                await HttpHelpers.WriteResponseToSocket(
                    response,
                    HttpVersion.HTTP_1_0,
                    _socket.Value,
                    cancelToken,
                    realTime,
                    _logger,
                    connectionDescriptionProducer: () =>
                        string.Format("{0} {1} {2}",
                            _socket.Value.RemoteEndpointString,
                            HttpRequest.RequestMethod,
                            HttpRequest.RequestFile)).ConfigureAwait(false);

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
                traceLogger.Log("HTTP response trailers are not supported by HTTP/1.0 servers. Trailers will not be sent.", LogLevel.Wrn);
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
            throw new NotSupportedException("Server push is not supported for HTTP/1.0");
        }
    }
}
