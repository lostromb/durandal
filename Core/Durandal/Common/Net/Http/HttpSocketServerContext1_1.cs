using Durandal.API;
using Durandal.Common.Cache;
using Durandal.Common.IO;
using Durandal.Common.IO.Hashing;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net.WebSocket;
using Durandal.Common.Security;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Implementation of HTTP 1.1 protocol operating on a socket
    /// </summary>
    public class SocketHttpServerContext1_1 : IHttpServerContext
    {
        private readonly IRandom _secureRandom;
        private readonly WeakPointer<ISocket> _socket;
        private readonly ILogger _logger;
        private readonly DateTimeOffset _requestStartTime;

        /// <inheritdoc/>
        public HttpVersion CurrentProtocolVersion => HttpVersion.HTTP_1_1;

        /// <inheritdoc/>
        public bool SupportsWebSocket => true;

        /// <inheritdoc/>
        public bool SupportsServerPush => false;

        /// <inheritdoc/>
        public bool SupportsTrailers { get; private set; }

        /// <inheritdoc/>
        public bool PrimaryResponseStarted { get; private set; }

        /// <inheritdoc/>
        public bool PrimaryResponseFinished { get; private set; }

        public bool DidAnyoneSpecifyConnectionClose { get; private set; }

        public bool AcceptedWebsocketConnection { get; private set; }

        /// <inheritdoc/>
        public HttpRequest HttpRequest
        {
            get;
            private set;
        }

        public SocketHttpServerContext1_1(WeakPointer<ISocket> socket, ILogger logger, IRandom secureRandom, IRealTimeProvider realTime)
        {
            _socket = socket.AssertNonNull(nameof(socket));
            _logger = logger.AssertNonNull(nameof(logger));
            _requestStartTime = realTime.AssertNonNull(nameof(realTime)).Time;
            _secureRandom = secureRandom.AssertNonNull(nameof(secureRandom));
            DidAnyoneSpecifyConnectionClose = false;
            PrimaryResponseStarted = false;
            PrimaryResponseFinished = false;
            AcceptedWebsocketConnection = false;
        }

        /// <inheritdoc/>
        public async Task<IWebSocket> AcceptWebsocketUpgrade(CancellationToken cancelToken, IRealTimeProvider realTime, string subProtocol = null)
        {
            // Validate that the caller did not try to accept an upgrade request that was not a websocket upgrade request
            string headerVal;
            if (HttpRequest.RequestMethod != "GET")
            {
                throw new WebSocketException("Cannot accept websocket upgrade: Incoming websocket upgrade must be a GET request");
            }
            else if (!HttpRequest.RequestHeaders.ContainsValue(HttpConstants.HEADER_KEY_CONNECTION, HttpConstants.HEADER_VALUE_CONNECTION_UPGRADE, StringComparison.OrdinalIgnoreCase) ||
                !HttpRequest.RequestHeaders.TryGetValue(HttpConstants.HEADER_KEY_UPGRADE, out headerVal) ||
                !string.Equals(headerVal, HttpConstants.HEADER_VALUE_UPGRADE_WEBSOCKET, StringComparison.OrdinalIgnoreCase))
            {
                throw new WebSocketException("Cannot accept websocket upgrade: Not a websocket upgrade request");
            }
            else if (!HttpRequest.RequestHeaders.ContainsKey(HttpConstants.HEADER_KEY_SEC_WEBSOCKET_KEY))
            {
                throw new WebSocketException("Cannot accept websocket upgrade: No Sec-Websocket-Key header");
            }


            // Send the 101 Switching Protocols response
            string websocketKeyBase64 = HttpRequest.RequestHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_KEY];
            byte[] hashInput = Encoding.UTF8.GetBytes(websocketKeyBase64 + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
            SHA1 hasher = new SHA1();
            byte[] hash = hasher.ComputeHash(hashInput);
            string acceptValue = Convert.ToBase64String(hash);

            HttpResponse switchingProtocolsResponse = HttpResponse.SwitchingProtocolsResponse(HttpConstants.HEADER_VALUE_UPGRADE_WEBSOCKET);
            switchingProtocolsResponse.ResponseHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_ACCEPT] = acceptValue;

            if (!string.IsNullOrWhiteSpace(subProtocol))
            {
                switchingProtocolsResponse.ResponseHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_PROTOCOL] = subProtocol;
            }

            _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Upgrading to websocket protocol; subprotocol is {0}", subProtocol);

            PrimaryResponseStarted = true;

            // Send 101 Switching Protocols and clear the incoming request buffer
            Task sendResponseTask = WritePrimaryResponse(switchingProtocolsResponse, _logger, cancelToken, realTime);
            Task clearRequestTask = FinishReadingEntireRequest(cancelToken, realTime);

            await sendResponseTask.ConfigureAwait(false);
            await clearRequestTask.ConfigureAwait(false);

            // Have to set this to ensure that the server closes the socket after we're finished with this context.
            PrimaryResponseFinished = true;
            DidAnyoneSpecifyConnectionClose = true;
            AcceptedWebsocketConnection = true;

            IWebSocket returnVal = new WebSocketClient(_socket, false, _logger, realTime, true, _secureRandom, subProtocol);
            return returnVal;
        }

        /// <summary>
        /// Reads from the socket until we read all of the incoming headers and are ready to begin reading body content (if any).
        /// </summary>
        /// <returns></returns>
        public async Task ReadIncomingRequestHeaders(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpRequest = await HttpHelpers.ReadRequestFromSocket(
                _socket.Value,
                HttpVersion.HTTP_1_1,
                _logger,
                cancelToken,
                realTime).ConfigureAwait(false);

            // Check if the request contained an explicit Connection: close operative
            string headerValue;
            DidAnyoneSpecifyConnectionClose =
                HttpRequest.RequestHeaders.TryGetValue(HttpConstants.HEADER_KEY_CONNECTION, out headerValue) &&
                string.Equals(HttpConstants.HEADER_VALUE_CONNECTION_CLOSE, headerValue, StringComparison.OrdinalIgnoreCase);

            // Also conditionally enable trailers on the server-side based on the presence of the "TE: trailers" header
            SupportsTrailers = HttpRequest.RequestHeaders.ContainsValue(
                HttpConstants.HEADER_KEY_TE,
                HttpConstants.HEADER_VALUE_TRAILERS,
                StringComparison.OrdinalIgnoreCase);
        }

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
        public Task WritePrimaryResponse(HttpResponse response, ILogger traceLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return WritePrimaryResponse(response, traceLogger, cancelToken, realTime, trailerNames: null, trailerDelegate: null);
        }

        /// <inheritdoc/>
        public async Task WritePrimaryResponse(
            HttpResponse response,
            ILogger traceLogger,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            IReadOnlyCollection<string> trailerNames,
            Func<string, Task<string>> trailerDelegate)
        {
            if (ReferenceEquals(response.GetOutgoingContentStream(), HttpRequest.GetIncomingContentStream()))
            {
                throw new InvalidOperationException("You can't pipe an HTTP input directly back to HTTP output");
            }

            try
            {
                // Determine if the response specified Connection: close.
                // Otherwise, for HTTP/1.1, assume persistent connections for each request.
                string headerValue;
                bool explicitConnectionClose = 
                    HttpRequest.RequestHeaders.TryGetValue(HttpConstants.HEADER_KEY_CONNECTION, out headerValue) &&
                    string.Equals(HttpConstants.HEADER_VALUE_CONNECTION_CLOSE, headerValue, StringComparison.OrdinalIgnoreCase);

                // Add response connection: close response header if request specified it but response did not
                if (DidAnyoneSpecifyConnectionClose && !explicitConnectionClose)
                {
                    response.ResponseHeaders[HttpConstants.HEADER_KEY_CONNECTION] = HttpConstants.HEADER_VALUE_CONNECTION_CLOSE;
                }

                DidAnyoneSpecifyConnectionClose = DidAnyoneSpecifyConnectionClose && explicitConnectionClose;

                // Generate instrumentation header
                TimeSpan totalRequestTime = realTime.Time - _requestStartTime;
                response.ResponseHeaders.Add(HttpConstants.HEADER_KEY_SERVER_WORK_TIME, totalRequestTime.PrintTimeSpan());

                PrimaryResponseStarted = true;
                // TODO If the caller verb was HEAD, skip sending the payload
                await HttpHelpers.WriteResponseToSocket(
                    response,
                    HttpVersion.HTTP_1_1,
                    _socket.Value,
                    cancelToken,
                    realTime,
                    _logger,
                    connectionDescriptionProducer: () =>
                        string.Format("{0} {1} {2}",
                            _socket.Value.RemoteEndpointString,
                            HttpRequest.RequestMethod,
                            HttpRequest.RequestFile),
                    trailerNames: trailerNames,
                    trailerDelegate: trailerDelegate).ConfigureAwait(false);

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
        public void PushPromise(
            string expectedRequestMethod,
            string expectedPath,
            HttpHeaders expectedRequestHeaders,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            throw new NotSupportedException("Server push is not implemented for HTTP/1.1");
        }
    }
}
