using Durandal.Common.Tasks;

namespace Durandal.Common.Net.Http
{
    using System.Net;
    using System.Threading;
    using System;

    using Durandal.Common.Logger;
    using Durandal.Common.Utils;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Text.RegularExpressions;
    using Durandal.Common.Time;
    using Durandal.Common.IO;
    using System.Text;
    using Durandal.Common.Net.Http2;
    using Durandal.API;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Security;
    using Durandal.Common.MathExt;

    /// <summary>
    /// An class that enables the ability to serve HTTP requests abstractly.
    /// You cannot inherit from this class; because of inversion-of-control, you have to create an IHttpServerDelegate class
    /// and then pass if to this instance's RegisterSubclass function. This allows the superclass to be abstract and the subclass to be common,
    /// which allows for better portability.
    /// This class uses raw sockets, which means that you can create servers without administrative privileges.
    /// </summary>
    public sealed class SocketHttpServer : ISocketServerDelegate, IHttpServer
    {
        private static readonly Http2SessionPreferences HTTP2_SERVER_PREFERENCES = new Http2SessionPreferences()
        {
            DesiredGlobalConnectionFlowWindow = 12582912, // Magic number representing a "generous" bandwidth allocation of 12Mb. This is close to what Firefox uses in its h2 client.
            MaxIdleTime = TimeSpan.FromSeconds(30),
            MaxPromisedStreamsToStore = 0,
            MaxStreamId = null,
            OutgoingPingInterval = TimeSpan.FromSeconds(60),
            PromisedStreamTimeout = TimeSpan.Zero,
            SettingsTimeout = TimeSpan.FromSeconds(5),
        };

        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly ISocketServer _baseServer;
        private readonly IRandom _secureRandom;
        private readonly ILogger _logger;
        private IHttpServerDelegate _subclass;
        private Uri _localAccessUri;
        private int _disposed = 0;

        public SocketHttpServer(
            ISocketServer baseServer,
            ILogger logger,
            IRandom secureRandom,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions)
        {
            _baseServer = baseServer;
            _logger = logger;
            _localAccessUri = new Uri("http://localhost"); // temporary value until the server actually starts
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            _secureRandom = secureRandom.AssertNonNull(nameof(secureRandom));
            _baseServer.RegisterSubclass(this);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~SocketHttpServer()
        {
            Dispose(false);
        }
#endif

        public Uri LocalAccessUri
        {
            get
            {
                return _localAccessUri;
            }
        }

        /// <inheritdoc />
        public async Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_subclass == null)
            {
                throw new InvalidOperationException("Cannot start an SocketHttpServer without registering a subclass first");
            }

            if (!await _baseServer.StartServer(serverName, cancelToken, realTime))
            {
                return false;
            }

            // Now that the base server has actually bound to its ports, we can parse those to determine http endpoints
            _localAccessUri = HttpHelpers.FindBestLocalAccessUrl(_baseServer.Endpoints);
            return true;
        }

        public Task HandleSocketConnection(ISocket clientSocket, ServerBindingInfo bindPoint, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (clientSocket == null)
            {
                _logger.Log("Http server encountered a closed or erroneous connection (AcceptSocket is null)", LogLevel.Err);
                return DurandalTaskExtensions.NoOpTask;
            }

            if (clientSocket.Features.ContainsKey(SocketFeature.NegotiatedHttp2Support))
            {
                return HandleSocketConnectionHttp2(clientSocket, bindPoint, cancelToken, realTime);
            }
            else
            {
                return HandleSocketConnectionHttp1(clientSocket, bindPoint, cancelToken, realTime);
            }
        }

        public async Task HandleSocketConnectionHttp2(ISocket clientSocket, ServerBindingInfo bindPoint, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            try
            {
                using (Http2Session serverSession = new Http2Session(clientSocket, _logger.Clone("H2ServerSession"), HTTP2_SERVER_PREFERENCES, _metrics, _metricDimensions))
                {
                    string localAuthority = GetLocalAuthorityString(bindPoint);
                    await serverSession.BeginServerSession(
                        cancelToken,
                        realTime,
                        Http2Settings.ServerDefault(),
                        localAuthority,
                        bindPoint.UseTls ? HttpConstants.SCHEME_HTTPS : HttpConstants.SCHEME_HTTP,
                        remoteSettings: null).ConfigureAwait(false);

                    await RunH2ServerLoop(serverSession,
                        clientSocket,
                        cancelToken,
                        realTime).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                // can't just catch (SocketException) because that type is not defined at this level of code...
                if (SocketHelpers.DoesExceptionIndicateSocketClosed(e))
                {
                    // Simplify the exception message
                    _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.EndUserIdentifiableInformation, "Socket connection aborted by remote host {0}", clientSocket.RemoteEndpointString);
                }
                else
                {
                    _logger.Log("An unhandled exception arose in the web server", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
            }
            finally
            {
                //_logger.Log("Closing http server socket");
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.EndUserIdentifiableInformation, "Closing HTTP2 socket {0}", clientSocket.RemoteEndpointString);
                await clientSocket.Disconnect(cancelToken, realTime, NetworkDuplex.ReadWrite, allowLinger: false).ConfigureAwait(false);
            }
        }

        public async Task HandleSocketConnectionHttp1(ISocket clientSocket, ServerBindingInfo bindPoint, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            bool awaitingMoreConnections = true;

            try
            {
                while (awaitingMoreConnections)
                {
                    awaitingMoreConnections = false;

                    // Parse the request line to try and figure out what protocol is in use
                    // This next line is where we will park most of the time on long-lived connections, so ensure that timeouts are handled specifically here
                    HttpVersion incomingRequestVersion = await HttpHelpers.ParseHttpVersionFromRequest(clientSocket, _logger, cancelToken, realTime);

                    if (incomingRequestVersion == null)
                    {
                        // Usually means the socket closed
                    }
                    else if (incomingRequestVersion == HttpVersion.HTTP_1_0)
                    {
                        SocketHttpServerContext1_0 context = new SocketHttpServerContext1_0(new WeakPointer<ISocket>(clientSocket), _logger, realTime);
                        try
                        {
                            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Http_IncomingRequests10, _metricDimensions);
                            await context.ReadIncomingRequestHeaders(cancelToken, realTime).ConfigureAwait(false);

                            if (_logger.ValidLogLevels.HasFlag(LogLevel.Vrb))
                            {
                                _logger.LogFormat(
                                    LogLevel.Vrb,
                                    DataPrivacyClassification.EndUserIdentifiableInformation,
                                    "Got an {0} request {1} from {2}",
                                    context.CurrentProtocolVersion.ProtocolString,
                                    context.HttpRequest.DecodedRequestFile,
                                    clientSocket.RemoteEndpointString);
                            }

                            await _subclass.HandleConnection(context, cancelToken, realTime).ConfigureAwait(false);
                            await context.FinishReadingEntireRequest(cancelToken, realTime).ConfigureAwait(false);

                            // If the server delegate did not generate a response, return HTTP 500.
                            if (!context.PrimaryResponseStarted)
                            {
                                await context.WritePrimaryResponse(HttpResponse.ServerErrorResponse("The server implementation did not generate a response"), _logger, cancelToken, realTime).ConfigureAwait(false);
                            }

                            awaitingMoreConnections = context.DidRequestSpecifyKeepAlive;
                        }
                        finally
                        {
                            context.HttpRequest?.Dispose();
                        }
                    }
                    else if (incomingRequestVersion == HttpVersion.HTTP_1_1)
                    {
                        SocketHttpServerContext1_1 context = new SocketHttpServerContext1_1(new WeakPointer<ISocket>(clientSocket), _logger, _secureRandom, realTime);
                        try
                        {
                            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Http_IncomingRequests11, _metricDimensions);
                            await context.ReadIncomingRequestHeaders(cancelToken, realTime).ConfigureAwait(false);

                            bool failedWebsocketUpgrade = false;

                            // Is the request asking to upgrade to another protocol?
                            if ((context.HttpRequest?.RequestHeaders?.ContainsKey(HttpConstants.HEADER_KEY_UPGRADE)).GetValueOrDefault(false))
                            {
                                string upgrade = context.HttpRequest.RequestHeaders[HttpConstants.HEADER_KEY_UPGRADE];
                                if (string.Equals(HttpConstants.HEADER_VALUE_UPGRADE_H2C, upgrade))
                                {
                                    // Validate the attempt to do H2 upgrade
                                    _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata,
                                        "Incoming request is trying to upgrade protocol from http1.1 to http2", upgrade);

                                    // Validate Http2-Settings header field and Connection header contains "HTTP2-Settings" value
                                    string base64Settings;
                                    Http2Settings parsedSettings;
                                    if (!context.HttpRequest.RequestHeaders.ContainsValue(
                                            HttpConstants.HEADER_KEY_CONNECTION,
                                            HttpConstants.HEADER_VALUE_CONNECTION_UPGRADE,
                                            StringComparison.OrdinalIgnoreCase) || 
                                        !context.HttpRequest.RequestHeaders.ContainsValue(
                                            HttpConstants.HEADER_KEY_CONNECTION,
                                            HttpConstants.HEADER_VALUE_CONNECTION_HTTP2_SETTINGS,
                                            StringComparison.OrdinalIgnoreCase) ||
                                        !context.HttpRequest.RequestHeaders.TryGetValue(HttpConstants.HEADER_KEY_HTTP2_SETTINGS, out base64Settings) ||
                                        !Http2Helpers.TryParseSettingsFromBase64(base64Settings, isServer: true, settings: out parsedSettings))
                                    {
                                        _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "H2 upgrade request was malformed and will be ignored");
                                    }
                                    else
                                    {
                                        // Set context to null so it doesn't get caught up in the try-finally disposal later on,
                                        // and so we're not holding long-lived references to it in general
                                        SocketHttpServerContext1_1 contextCopy = context;
                                        context = null;
                                        string localAuthority = GetLocalAuthorityString(bindPoint);

                                        // Begin the HTTP2 session, which may run for a while.
                                        await UpgradeToHttp2SessionAndLoop(
                                            contextCopy,
                                            clientSocket,
                                            parsedSettings,
                                            _logger,
                                            localAuthority,
                                            cancelToken,
                                            realTime).ConfigureAwait(false);

                                        // We break the HTTP 1.1 socket recycling loop here because the Upgrade function begins its own loop
                                        // So we only reach this point after the entire h2 session has closed.
                                        return;
                                    }
                                }
                                else if (string.Equals(HttpConstants.HEADER_VALUE_UPGRADE_WEBSOCKET, upgrade))
                                {
                                    // Incoming websocket upgrade. Validate everything and tell the client if they made a mistake.
                                    _logger.Log("Incoming request is attempting websocket upgrade. Validating request...");

                                    string headerVal;
                                    string websocketKeyBase64;
                                    if (!string.Equals(context.HttpRequest.RequestMethod, HttpConstants.HTTP_VERB_GET, StringComparison.Ordinal))
                                    {
                                        failedWebsocketUpgrade = true;
                                        _logger.Log("Incoming websocket upgrade failed: Not a GET request.");
                                        await context.WritePrimaryResponse(HttpResponse.BadRequestResponse("Websocket upgrade must be a GET request."), _logger, cancelToken, realTime).ConfigureAwait(false);
                                    }
                                    else if (!context.HttpRequest.RequestHeaders.ContainsValue(
                                        HttpConstants.HEADER_KEY_CONNECTION, HttpConstants.HEADER_VALUE_CONNECTION_UPGRADE, StringComparison.OrdinalIgnoreCase))
                                    {
                                        failedWebsocketUpgrade = true;
                                        _logger.Log("Incoming websocket upgrade failed: Connection header must specify Upgrade.");
                                        await context.WritePrimaryResponse(HttpResponse.BadRequestResponse("Connection header must specify Upgrade"), _logger, cancelToken, realTime).ConfigureAwait(false);
                                    }
                                    else if (!context.HttpRequest.RequestHeaders.TryGetValue(HttpConstants.HEADER_KEY_SEC_WEBSOCKET_VERSION, out headerVal) ||
                                        !string.Equals("13", headerVal, StringComparison.OrdinalIgnoreCase))
                                    {
                                        failedWebsocketUpgrade = true;
                                        _logger.Log("Incoming websocket upgrade failed: Sec-Websocket-Version not present or unknown version.");
                                        await context.WritePrimaryResponse(HttpResponse.BadRequestResponse("Sec-Websocket-Version header must be \"13\""), _logger, cancelToken, realTime).ConfigureAwait(false);
                                    }
                                    else if (!context.HttpRequest.RequestHeaders.TryGetValue(HttpConstants.HEADER_KEY_SEC_WEBSOCKET_KEY, out websocketKeyBase64))
                                    {
                                        failedWebsocketUpgrade = true;
                                        _logger.Log("Incoming websocket upgrade failed: Sec-Websocket-Key not present.");
                                        await context.WritePrimaryResponse(HttpResponse.BadRequestResponse("Sec-Websocket-Key header must be present"), _logger, cancelToken, realTime).ConfigureAwait(false);
                                    }
                                }
                                else
                                {
                                    _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata,
                                        "Incoming request is trying to upgrade to unknown protocol {0}", upgrade);
                                }
                            }

                            if (_logger.ValidLogLevels.HasFlag(LogLevel.Vrb))
                            {
                                _logger.LogFormat(
                                    LogLevel.Vrb,
                                    DataPrivacyClassification.EndUserIdentifiableInformation,
                                    "Got an {0} request {1} from {2}",
                                    context.CurrentProtocolVersion.ProtocolString,
                                    context.HttpRequest.DecodedRequestFile,
                                    clientSocket.RemoteEndpointString);
                            }

                            if (!failedWebsocketUpgrade)
                            {
                                // If this is a regular HTTP request, this will just invoke the delegate as normal.
                                // If it's a websocket request, this method will represent the entire websocket session,
                                // and will return once it has closed. It will also disconnect the socket itself.
                                await _subclass.HandleConnection(context, cancelToken, realTime).ConfigureAwait(false);
                            }

                            if (context.AcceptedWebsocketConnection)
                            {
                                awaitingMoreConnections = false;
                            }
                            else
                            {
                                await context.FinishReadingEntireRequest(cancelToken, realTime).ConfigureAwait(false);

                                // If the server delegate did not generate a response, return HTTP 500.
                                if (!context.PrimaryResponseStarted)
                                {
                                    await context.WritePrimaryResponse(HttpResponse.ServerErrorResponse("The server implementation did not generate a response"), _logger, cancelToken, realTime).ConfigureAwait(false);
                                }

                                awaitingMoreConnections = !context.DidAnyoneSpecifyConnectionClose;
                            }
                        }
                        finally
                        {
                            context?.HttpRequest?.Dispose();
                        }
                    }
                    else
                    {
                        _logger.LogFormat(
                            LogLevel.Wrn,
                            DataPrivacyClassification.SystemMetadata, 
                            "Got an HTTP request with unsupported protocol version {0}",
                            incomingRequestVersion);
                    }
                }
            }
            catch (Exception e)
            {
                // can't just catch (SocketException) because that type is not defined at this level of code...
                if (SocketHelpers.DoesExceptionIndicateSocketClosed(e))
                {
                    // Simplify the exception message
                    _logger.LogFormat(
                        LogLevel.Wrn,
                        DataPrivacyClassification.EndUserIdentifiableInformation,
                        "Socket connection aborted by remote host {0}",
                        clientSocket.RemoteEndpointString);
                }
                else
                {
                    _logger.Log("An unhandled exception arose in the web server", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
            }
            finally
            {
                //_logger.Log("Closing http server socket");
                _logger.LogFormat(
                    LogLevel.Vrb,
                    DataPrivacyClassification.EndUserIdentifiableInformation, 
                    "Closing HTTP server socket {0}",
                    clientSocket.RemoteEndpointString);
                await clientSocket.Disconnect(cancelToken, realTime, NetworkDuplex.ReadWrite, allowLinger: false).ConfigureAwait(false);
            }
        }

        private string GetLocalAuthorityString(ServerBindingInfo bindPoint)
        {
            if (bindPoint.LocalIpPort.HasValue)
            {
                if (bindPoint.TlsCertificateIdentifier != null && !string.IsNullOrEmpty(bindPoint.TlsCertificateIdentifier.SubjectName))
                {
                    return string.Format("{0}:{1}", bindPoint.TlsCertificateIdentifier.SubjectName, bindPoint.LocalIpPort);
                }
                else if (!bindPoint.IsWildcardEndpoint)
                {
                    return string.Format("{0}:{1}", bindPoint.LocalIpEndpoint, bindPoint.LocalIpPort);
                }
                else
                {
                    return string.Format("localhost:{0}", bindPoint.LocalIpPort);
                }
            }
            else
            {

                if (bindPoint.TlsCertificateIdentifier != null && !string.IsNullOrEmpty(bindPoint.TlsCertificateIdentifier.SubjectName))
                {
                    return bindPoint.TlsCertificateIdentifier.SubjectName;
                }
                else if (!bindPoint.IsWildcardEndpoint)
                {
                    return bindPoint.LocalIpEndpoint;
                }
                else
                {
                    return "localhost";
                }
            }
        }

        private async Task RunH2ServerLoop(
            Http2Session serverSession,
            ISocket clientSocket,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            while (serverSession.IsActive)
            {
                IHttpServerContext context = await serverSession.HandleIncomingHttpRequest(_logger.Clone("H2Request"), cancelToken, realTime, _subclass).ConfigureAwait(false);
                if (context != null) // context can be null if a request began but then aborted immediately, so just ignore it.
                {
                    _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Http_IncomingRequests20, _metricDimensions);
                    if (_logger.ValidLogLevels.HasFlag(LogLevel.Vrb))
                    {
                        _logger.LogFormat(
                        LogLevel.Vrb,
                        DataPrivacyClassification.EndUserIdentifiableInformation,
                        "Got an {0} request {1} from {2}",
                            context.CurrentProtocolVersion.ProtocolString,
                            context.HttpRequest.DecodedRequestFile,
                            clientSocket.RemoteEndpointString);
                    }

                    // THREADING NOTE: Since multiple HTTP2 requests can happen simultaneously over the same socket,
                    // we can't just handle the connection synchronously here because that means we are effectively single-threaded.
                    // So we delegate the handling to the global thread pool here and then immediately wait for the next request to come in.
                    IRealTimeProvider threadLocalTime = realTime.Fork("Http2ServerHandler");
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _subclass.HandleConnection(context, cancelToken, threadLocalTime).ConfigureAwait(false);

                            // If the server delegate did not generate a response, return HTTP 500.
                            if (!context.PrimaryResponseStarted)
                            {
                                await context.WritePrimaryResponse(HttpResponse.ServerErrorResponse("The server implementation did not generate a response"), _logger, cancelToken, threadLocalTime).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            context.HttpRequest?.Dispose();
                            threadLocalTime.Merge();
                        }
                    }).Forget(_logger);
                }
            }
        }

        private async Task UpgradeToHttp2SessionAndLoop(
            SocketHttpServerContext1_1 originalContext,
            ISocket clientSocket,
            Http2Settings settingsFromRequestHeader,
            ILogger logger,
            string localAuthority,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            // Write the Switching-Protocols response to the wire (this response must precede H2 frames)
            HttpResponse switchingProtocolsResponse = HttpResponse.SwitchingProtocolsResponse("h2c");
            await HttpHelpers.WriteResponseToSocket(
                switchingProtocolsResponse,
                originalContext.CurrentProtocolVersion,
                clientSocket,
                cancelToken,
                realTime,
                logger,
                connectionDescriptionProducer: () =>
                    string.Format("{0} HTTP/1.1 -> 2.0 Upgrade",
                        clientSocket.RemoteEndpointString)).ConfigureAwait(false);

            // Create a new H2 session, begin writing a SETTINGS frame
            Http2Session newServerSession = new Http2Session(clientSocket, logger.Clone("H2ServerSession"), HTTP2_SERVER_PREFERENCES, _metrics, _metricDimensions);
            newServerSession.BeginUpgradedServerSessionPart1(
                cancelToken,
                realTime,
                Http2Settings.ServerDefault(),
                localAuthority,
                HttpConstants.SCHEME_HTTP, // Scheme must be HTTP because otherwise we would have negotiated via ALPN
                settingsFromRequestHeader);

            // Migrate from the 1.1 context to the 2.0 context, while preserving the incoming request stream
            SocketHttpServerContext2_0 newContext = new SocketHttpServerContext2_0(
                new WeakPointer<Http2Session>(newServerSession),
                originalContext.HttpRequest,
                logger,
                _subclass);

            // Pass the new 2.0 context to the subclass and run with it
            if (_logger.ValidLogLevels.HasFlag(LogLevel.Vrb))
            {
                _logger.LogFormat(LogLevel.Vrb,
                    DataPrivacyClassification.EndUserIdentifiableInformation,
                    "Got an {0} request {1} from {2}",
                    newContext.CurrentProtocolVersion.ProtocolString,
                    newContext.HttpRequest.DecodedRequestFile,
                    clientSocket.RemoteEndpointString);
            }

            await _subclass.HandleConnection(newContext, cancelToken, realTime).ConfigureAwait(false);
            await originalContext.FinishReadingEntireRequest(cancelToken, realTime).ConfigureAwait(false);

            // If the server delegate did not generate a response, return HTTP 500.
            if (!newContext.PrimaryResponseStarted)
            {
                await newContext.WritePrimaryResponse(HttpResponse.ServerErrorResponse("The server implementation did not generate a response"), _logger, cancelToken, realTime).ConfigureAwait(false);
            }

            // Now carry on the upgraded session for potentially long-lived future requests. This method won't return until the session is terminated.
            await RunH2ServerLoop(newServerSession, clientSocket, cancelToken, realTime).ConfigureAwait(false);
        }

        public Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _baseServer.StopServer(cancelToken, realTime);
        }

        public IEnumerable<ServerBindingInfo> Endpoints
        {
            get
            {
                return _baseServer.Endpoints;
            }
        }

        public bool Running
        {
            get
            {
                return _baseServer.Running;
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
                _baseServer.Dispose();
            }
        }

        public void RegisterSubclass(IHttpServerDelegate subclass)
        {
            _subclass = subclass;
        }
    }
}
