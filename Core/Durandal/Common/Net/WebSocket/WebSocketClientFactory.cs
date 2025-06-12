using Durandal.Common.Instrumentation;
using Durandal.Common.IO.Hashing;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Net.Http;
using Durandal.Common.Net.Http2;
using Durandal.Common.Security;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.WebSocket
{
    public class WebSocketClientFactory : IWebSocketClientFactory
    {
        private readonly WeakPointer<ISocketFactory> _socketFactory;
        private readonly WeakPointer<IHttp2SessionManager> _h2SessionManager;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly IRandom _random;
        
        public WebSocketClientFactory(
            WeakPointer<ISocketFactory> socketFactory,
            WeakPointer<IHttp2SessionManager> h2SessionManager,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            IRandom secureRandom)
        {
            _socketFactory = socketFactory.AssertNonNull(nameof(socketFactory));
            _h2SessionManager = h2SessionManager.AssertNonNull(nameof(h2SessionManager));
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            _random = secureRandom.AssertNonNull(nameof(secureRandom));

            InitialHttpVersion = HttpVersion.HTTP_1_1;
        }

        public HttpVersion InitialHttpVersion { get; set; }

        public async Task<IWebSocket> OpenWebSocketConnection(
            ILogger logger,
            TcpConnectionConfiguration remoteConfig,
            string uriPath,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            WebSocketConnectionParams additionalParams = null)
        {
            if (InitialHttpVersion == HttpVersion.HTTP_1_1)
            {
                return await ConnectWebsocketHttp1(logger, remoteConfig, uriPath, cancelToken, realTime, additionalParams).ConfigureAwait(false);
            }
            else if (InitialHttpVersion == HttpVersion.HTTP_2_0)
            {
                return await ConnectWebsocketHttp2(logger, remoteConfig, uriPath, cancelToken, realTime, additionalParams).ConfigureAwait(false);
            }

            throw new NotImplementedException("Invalid HTTP version for websocket client: " + InitialHttpVersion.ToString());
        }

        private async Task<IWebSocket> ConnectWebsocketHttp1(
            ILogger logger,
            TcpConnectionConfiguration remoteConfig,
            string uriPath,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            WebSocketConnectionParams additionalParams)
        {
            ISocket socket = await _socketFactory.Value.Connect(remoteConfig, logger, cancelToken, realTime).ConfigureAwait(false);
            return await ConnectWebsocketHttp1(socket, logger, remoteConfig, uriPath, cancelToken, realTime, additionalParams).ConfigureAwait(false);
        }

        private async Task<IWebSocket> ConnectWebsocketHttp1(
            ISocket socket,
            ILogger logger,
            TcpConnectionConfiguration remoteConfig,
            string uriPath,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            WebSocketConnectionParams additionalParams)
        {
            HttpRequest connectRequest = null;
            HttpResponse response = null;
            try
            {
                connectRequest = BuildWebSocketConnectRequest(uriPath, remoteConfig.HostHeaderValue, additionalParams);
               
                await HttpHelpers.WriteRequestToSocket(
                    connectRequest,
                    InitialHttpVersion,
                    socket,
                    cancelToken,
                    realTime,
                    logger,
                    () =>
                        string.Format("{0} {1}{2}",
                            connectRequest.RequestMethod,
                            socket.RemoteEndpointString,
                            connectRequest.RequestFile)).ConfigureAwait(false);

                response = await HttpHelpers.ReadResponseFromSocket(
                    socket,
                    InitialHttpVersion,
                    logger, cancelToken,
                    realTime,
                    useManualSocketContext: true).ConfigureAwait(false);

                // Is it a success response?
                if (response.ResponseCode == 101)
                {
                    // FinishAsync specifically doesn't close the socket because we specified that we want
                    // full manual control over the socket state. So this call shouldn't affect anything
                    // other than muting some error messages.
                    await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);

                    string subProtocol = response.ResponseHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_PROTOCOL];
                    string actualAcceptHeader = response.ResponseHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_ACCEPT];
                    if (string.IsNullOrEmpty(actualAcceptHeader))
                    {
                        throw new WebSocketException("No Sec-WebSocket-Accept header found in response");
                    }

                    if (subProtocol != null && subProtocol.Contains(","))
                    {
                        throw new WebSocketException("Invalid Sec-WebSocket-Protocol response " + subProtocol);
                    }

                    // Assert that the response contains the correct headers
                    byte[] hashInput = Encoding.UTF8.GetBytes(connectRequest.RequestHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_KEY] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
                    SHA1 hasher = new SHA1();
                    byte[] hash = hasher.ComputeHash(hashInput);
                    string expectedAcceptValue = Convert.ToBase64String(hash);
                    if (!string.Equals(expectedAcceptValue, actualAcceptHeader, StringComparison.Ordinal))
                    {
                        throw new WebSocketException("Incorrect Sec-WebSocket-Accept header");
                    }

                    IWebSocket returnVal = new WebSocketClient(new WeakPointer<ISocket>(socket), true, logger, realTime, false, _random, subProtocol);
                    socket = null;
                    return returnVal;
                }
                else
                {
                    // Read the response to figure out what error to show.
                    await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    throw new WebSocketException("Websocket failed to connect");
                }
            }
            finally
            {
                socket?.Dispose();
                connectRequest?.Dispose();
                response?.Dispose();
            }
        }

        private async Task<IWebSocket> ConnectWebsocketHttp2(
            ILogger logger,
            TcpConnectionConfiguration remoteConfig,
            string uriPath,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            WebSocketConnectionParams additionalParams)
        {
            Http2SessionInitiationResult initResult = await _h2SessionManager.Value.TryCreateH2Session(
                _socketFactory.Value,
                remoteConfig,
                new Http2SessionPreferences(),
                logger,
                _metrics,
                _metricDimensions,
                cancelToken,
                realTime).ConfigureAwait(false);

            if (initResult.Session != null)
            {
                // Operating over H2.
                // Does the session actually support websockets?
                HttpRequest connectRequest = BuildWebSocketConnectRequest(uriPath, remoteConfig.HostHeaderValue, additionalParams);
                Tuple<HttpResponse, ISocket> connectResult = await initResult.Session.OpenClientWebsocket(connectRequest, logger, cancelToken, realTime);

                string subProtocol = connectResult.Item1.ResponseHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_PROTOCOL];
                string actualAcceptHeader = connectResult.Item1.ResponseHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_ACCEPT];
                if (string.IsNullOrEmpty(actualAcceptHeader))
                {
                    throw new WebSocketException("No Sec-WebSocket-Accept header found in response");
                }

                if (subProtocol != null && subProtocol.Contains(","))
                {
                    throw new WebSocketException("Invalid Sec-WebSocket-Protocol response " + subProtocol);
                }

                // Assert that the response contains the correct headers
                byte[] hashInput = Encoding.UTF8.GetBytes(connectRequest.RequestHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_KEY] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
                SHA1 hasher = new SHA1();
                byte[] hash = hasher.ComputeHash(hashInput);
                string expectedAcceptValue = Convert.ToBase64String(hash);
                if (!string.Equals(expectedAcceptValue, actualAcceptHeader, StringComparison.Ordinal))
                {
                    throw new WebSocketException("Incorrect Sec-WebSocket-Accept header");
                }

                return new WebSocketClient(new WeakPointer<ISocket>(connectResult.Item2), true, logger, realTime, false, _random, subProtocol);
            }
            else if (initResult.Socket != null)
            {
                // Server does not support H2. Fallback to 1.1
                return await ConnectWebsocketHttp1(initResult.Socket, logger, remoteConfig, uriPath, cancelToken, realTime, additionalParams).ConfigureAwait(false);
            }
            else
            {
                // Could not connect to server.
                return null;
            }
        }

        private HttpRequest BuildWebSocketConnectRequest(string uriPath, string host, WebSocketConnectionParams additionalParams)
        {
            HttpRequest connectRequest = HttpRequest.CreateOutgoing(uriPath);
            byte[] connectKey = new byte[16];
            _random.NextBytes(connectKey);
            connectRequest.RequestHeaders[HttpConstants.HEADER_KEY_CONNECTION] = HttpConstants.HEADER_VALUE_CONNECTION_UPGRADE;
            connectRequest.RequestHeaders[HttpConstants.HEADER_KEY_UPGRADE] = HttpConstants.HEADER_VALUE_UPGRADE_WEBSOCKET;
            connectRequest.RequestHeaders[HttpConstants.HEADER_KEY_HOST] = host;
            connectRequest.RequestHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_KEY] = Convert.ToBase64String(connectKey);
            connectRequest.RequestHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_VERSION] = "13";

            if (additionalParams != null)
            {
                if (additionalParams.AvailableProtocols != null)
                {
                    connectRequest.RequestHeaders[HttpConstants.HEADER_KEY_SEC_WEBSOCKET_PROTOCOL] = string.Join(", ", additionalParams.AvailableProtocols);
                }

                if (additionalParams.AdditionalHeaders != null)
                {
                    foreach (var headerPair in additionalParams.AdditionalHeaders)
                    {
                        foreach (string headerValue in headerPair.Value)
                        {
                            connectRequest.RequestHeaders.Add(headerPair.Key, headerValue);
                        }
                    }
                }
            }

            return connectRequest;
        }
    }
}
