namespace Durandal.Common.Net.Http
{
    using System;

    using Durandal.Common.Logger;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Durandal.Common.Tasks;
    using System.IO;
    using System.Threading;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.Common.IO;
    using Durandal.Common.Net.Http2;
    using Durandal.Common.Instrumentation;
    using Durandal.API;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// A base class for a simple HTTP client
    /// </summary>
    public class SocketHttpClient : IHttpClient
    {
        protected readonly ILogger _logger;
        private readonly WeakPointer<ISocketFactory> _socketFactory;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly TcpConnectionConfiguration _tcpConfig;
        private readonly WeakPointer<IHttp2SessionManager> _h2SessionManager;
        private readonly Http2SessionPreferences _h2SessionPreferences;
        private int _readTimeout = 10000;

        /// <summary>
        /// Indicates that the server has previously ignored an Upgrade: h2c request and we shouldn't keep trying.
        /// </summary>
        private bool _remoteServerHasRefusedH2Upgrade = false;

        private int _disposed = 0;

        public SocketHttpClient(
            WeakPointer<ISocketFactory> socketFactory,
            Uri url,
            ILogger logger,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            WeakPointer<IHttp2SessionManager> h2SessionManager,
            Http2SessionPreferences h2SessionPreferences)
        {
            bool useTls = string.Equals("https", url.Scheme, StringComparison.OrdinalIgnoreCase);
            int actualPort = url.Port;
            if (url.Port == 0 || url.IsDefaultPort)
            {
                actualPort = useTls ? 443 : 80;
            }

            _tcpConfig = new TcpConnectionConfiguration()
            {
                DnsHostname = url.Host,
                Port = actualPort,
                UseTLS = useTls,
                ReportHttp2Capability = true,
            };

            _logger = logger;
            _socketFactory = socketFactory;
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            _h2SessionManager = h2SessionManager.AssertNonNull(nameof(h2SessionManager));
            _h2SessionPreferences = h2SessionPreferences.AssertNonNull(nameof(h2SessionPreferences));
            InitialProtocolVersion = HttpVersion.HTTP_2_0;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public SocketHttpClient(
            WeakPointer<ISocketFactory> socketFactory,
            TcpConnectionConfiguration connectionConfig,
            ILogger logger,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            WeakPointer<IHttp2SessionManager> h2SessionManager,
            Http2SessionPreferences h2SessionPreferences)
        {
            _tcpConfig = connectionConfig;
            _logger = logger;
            _socketFactory = socketFactory;
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            _h2SessionManager = h2SessionManager.AssertNonNull(nameof(h2SessionManager));
            _h2SessionPreferences = h2SessionPreferences.AssertNonNull(nameof(h2SessionPreferences));

            // Set default port value if not specified.
            if (!_tcpConfig.Port.HasValue)
            {
                _tcpConfig.Port = _tcpConfig.UseTLS ? 443 : 80;
            }

            InitialProtocolVersion = HttpVersion.HTTP_2_0;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~SocketHttpClient()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// The Uri form (scheme, host, port) of the address used to connect to the server
        /// </summary>
        public Uri ServerAddress
        {
            get
            {
                return new Uri(ServerAddressInternal);
            }
        }

        /// <summary>
        /// The internal server address as a string, in the form http://0.0.0.0:port
        /// </summary>
        private string ServerAddressInternal
        {
            get
            {
                // This should always be true because we set the default port number in the constructor.
                if (_tcpConfig.Port.HasValue)
                {
                    return string.Format("{0}://{1}:{2}",
                        _tcpConfig.UseTLS ? "https" : "http",
                        _tcpConfig.DnsHostname,
                        _tcpConfig.Port);
                }
                else
                {
                    return string.Format("{0}://{1}",
                        _tcpConfig.UseTLS ? "https" : "http",
                        _tcpConfig.DnsHostname);
                }
            }
        }

        public HttpVersion MaxSupportedProtocolVersion => HttpVersion.HTTP_2_0;

        private HttpVersion _initialProtocolVersion;

        public HttpVersion InitialProtocolVersion
        {
            get
            {
                return _initialProtocolVersion;
            }
            set
            {
                _tcpConfig.ReportHttp2Capability = value == HttpVersion.HTTP_2_0;
                _initialProtocolVersion = value;
            }
        }

        public Task<HttpResponse> SendRequestAsync(HttpRequest request)
        {
            return SendRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, null);
        }

        public Task<NetworkResponseInstrumented<HttpResponse>> SendInstrumentedRequestAsync(HttpRequest request)
        {
            return SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, null);
        }

        public async Task<HttpResponse> SendRequestAsync(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime = null, ILogger queryLogger = null)
        {
            return (await SendInstrumentedRequestAsync(request, cancelToken, realTime, queryLogger).ConfigureAwait(false)).UnboxAndDispose();
        }

        /// <summary>
        /// Make an instrumented HTTP request
        /// </summary>
        /// <param name="request">The request to send</param>
        /// <param name="cancelToken">Cancellation token for the operation</param>
        /// <param name="realTime">Definition of real time</param>
        /// <param name="queryLogger">A logger for the request</param>
        /// <returns>An HTTP response with instrumentation</returns>
        public async Task<NetworkResponseInstrumented<HttpResponse>> SendInstrumentedRequestAsync(
            HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime = null, ILogger queryLogger = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            if (queryLogger == null)
            {
                queryLogger = _logger;
            }
            else
            {
                queryLogger = queryLogger.Clone(_logger.ComponentName);
            }

            try
            {
                Http2SessionInitiationResult h2EstablishResult;

                if (InitialProtocolVersion >= HttpVersion.HTTP_2_0)
                {
                    // If the remote endpoint is https, attempt to make a new connection and negotiate the protocol at the same time
                    if (_tcpConfig.UseTLS)
                    {
                        h2EstablishResult = await _h2SessionManager.Value.TryCreateH2Session(
                            _socketFactory.Value,
                            _tcpConfig,
                            _h2SessionPreferences,
                            queryLogger,
                            _metrics,
                            _metricDimensions,
                            cancelToken,
                            realTime).ConfigureAwait(false);
                    }
                    else
                    {
                        // If the remote endpoint is http, just check if there is a previous session that we have Upgraded from previously
                        Http2Session existingSession = _h2SessionManager.Value.CheckForExistingH2Session(
                            _tcpConfig,
                            queryLogger,
                            cancelToken,
                            realTime);

                        if (existingSession != null)
                        {
                            h2EstablishResult = new Http2SessionInitiationResult(existingSession);
                        }
                        else
                        {
                            h2EstablishResult = new Http2SessionInitiationResult(await _socketFactory.Value.Connect(_tcpConfig, queryLogger, cancelToken, realTime).ConfigureAwait(false));
                        }
                    }
                }
                else
                {
                    // Don't try to initiate http2 if caller of this client has disabled it
                    h2EstablishResult = new Http2SessionInitiationResult(await _socketFactory.Value.Connect(_tcpConfig, queryLogger, cancelToken, realTime).ConfigureAwait(false));
                }

                if (h2EstablishResult.Session != null)
                {
                    // We're running on HTTP/2. Send the request using that context
                    Http2Session h2Session = h2EstablishResult.Session;
                    Stopwatch timer = Stopwatch.StartNew();
                    HttpResponse response = await h2Session.MakeHttpRequest(request, queryLogger, cancelToken, realTime).ConfigureAwait(false);
                    if (response == null)
                    {
                        // Handle race conditions where the session was just aborting while we started this request, so we need to recreate the session
                        h2EstablishResult = await _h2SessionManager.Value.TryCreateH2Session(
                            _socketFactory.Value,
                            _tcpConfig,
                            _h2SessionPreferences,
                            queryLogger,
                            _metrics,
                            _metricDimensions,
                            cancelToken,
                            realTime).ConfigureAwait(false);
                    }
                    
                    // Just use approximate metrics as a base since timing things is hard
                    double overallTime = timer.ElapsedMillisecondsPrecise();
                    double sendTime = overallTime / 4;
                    double remoteTime = overallTime / 2;
                    double recvTime = overallTime / 4;

                    // If the server sent an instrumentation header, we can actually get accurate numbers
                    string remoteServerTimeHeader;
                    TimeSpan remoteServerTime;
                    if (response.ResponseHeaders.TryGetValue(HttpConstants.HEADER_KEY_SERVER_WORK_TIME, out remoteServerTimeHeader) &&
                        TimeSpanExtensions.TryParseTimeSpan(remoteServerTimeHeader, out remoteServerTime))
                    {
                        remoteTime = remoteServerTime.TotalMilliseconds;
                        sendTime = (overallTime - remoteTime) / 2;
                        recvTime = sendTime;
                    }

                    int requestSize = 0;
                    return new NetworkResponseInstrumented<HttpResponse>(response, requestSize, 0, sendTime, remoteTime, recvTime);
                }
                else
                {
                    ISocket clientSocket = h2EstablishResult.Socket;
                    if (clientSocket == null)
                    {
                        queryLogger.Log("Could not create HTTP socket: Unspecified error", LogLevel.Err);
                        return new NetworkResponseInstrumented<HttpResponse>(null);
                    }

                    try
                    {
                        if (InitialProtocolVersion != HttpVersion.HTTP_1_0)
                        {
                            // Add the host header
                            if (!request.RequestHeaders.ContainsKey(HttpConstants.HEADER_KEY_HOST))
                            {
                                request.RequestHeaders.Add(HttpConstants.HEADER_KEY_HOST, _tcpConfig.HostHeaderValue);
                            }
                        }

                        // Override the connection header based on the capabilities of the socket
                        bool requestedH2Upgrade = false;
                        Http2Settings clientH2Settings = null;
                        if (!_tcpConfig.UseTLS && !_remoteServerHasRefusedH2Upgrade && InitialProtocolVersion >= HttpVersion.HTTP_2_0)
                        {
                            // Send the upgrade header to try and upgrade Http 1.1 to 2.0
                            queryLogger.Log("Attempting non-TLS HTTP2 negotiation using Upgrade header");
                            request.RequestHeaders[HttpConstants.HEADER_KEY_UPGRADE] = HttpConstants.HEADER_VALUE_UPGRADE_H2C;
                            request.RequestHeaders[HttpConstants.HEADER_KEY_CONNECTION] = "Upgrade, HTTP2-Settings";

                            // Make sure we pass the settings here that will be used for the session later on.
                            // Otherwise we could get deadlocked from e.g. inconsistent flow-control windows
                            clientH2Settings = Http2Settings.Default();
                            clientH2Settings.MaxFrameSize = BufferPool<byte>.DEFAULT_BUFFER_SIZE;
                            clientH2Settings.InitialWindowSize = _h2SessionPreferences.DesiredGlobalConnectionFlowWindow;

                            request.RequestHeaders[HttpConstants.HEADER_KEY_HTTP2_SETTINGS] = Http2Helpers.SerializeSettingsToBase64(clientH2Settings);
                            requestedH2Upgrade = true;
                        }
                        else if (InitialProtocolVersion == HttpVersion.HTTP_1_0 && clientSocket is IPooledSocket)
                        {
                            request.RequestHeaders[HttpConstants.HEADER_KEY_CONNECTION] = HttpConstants.HEADER_VALUE_CONNECTION_KEEP_ALIVE;
                        }
                        else if (InitialProtocolVersion == HttpVersion.HTTP_1_1 && !(clientSocket is IPooledSocket))
                        {
                            request.RequestHeaders[HttpConstants.HEADER_KEY_CONNECTION] = HttpConstants.HEADER_VALUE_CONNECTION_CLOSE;
                        }

                        Stopwatch timer = Stopwatch.StartNew();
                        HttpVersion actualProtocolVersion = InitialProtocolVersion;
                        if (actualProtocolVersion >= HttpVersion.HTTP_2_0)
                        {
                            // this is so we don't write "HTTP/2.0 200 OK" to the wire if that's not the actual protocol we negotiated
                            actualProtocolVersion = HttpVersion.HTTP_1_1;
                        }

                        if (actualProtocolVersion == HttpVersion.HTTP_1_1)
                        {
                            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Http_OutgoingRequests11, _metricDimensions);
                        }
                        else if (actualProtocolVersion == HttpVersion.HTTP_1_0)
                        {
                            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_Http_OutgoingRequests10, _metricDimensions);
                        }

                        // WRITE THE REQUEST
                        await HttpHelpers.WriteRequestToSocket(
                            request,
                            actualProtocolVersion,
                            clientSocket,
                            cancelToken,
                            realTime,
                            queryLogger,
                            () =>
                                string.Format("{0} {1}{2}",
                                    request.RequestMethod,
                                    ServerAddressInternal,
                                    request.RequestFile)).ConfigureAwait(false);

                        // READ THE RESPONSE
                        HttpResponse response = await HttpHelpers.ReadResponseFromSocket(
                            clientSocket,
                            actualProtocolVersion,
                            queryLogger, cancelToken,
                            realTime).ConfigureAwait(false);

                        // Just use approximate metrics as a base since timing things is hard
                        double overallTime = timer.ElapsedMillisecondsPrecise();
                        double sendTime = overallTime / 4;
                        double remoteTime = overallTime / 2;
                        double recvTime = overallTime / 4;

                        // If the server sent an instrumentation header, we can actually get accurate numbers
                        string remoteServerTimeHeader;
                        TimeSpan remoteServerTime;
                        if (response.ResponseHeaders.TryGetValue(HttpConstants.HEADER_KEY_SERVER_WORK_TIME, out remoteServerTimeHeader) &&
                            TimeSpanExtensions.TryParseTimeSpan(remoteServerTimeHeader, out remoteServerTime))
                        {
                            remoteTime = remoteServerTime.TotalMilliseconds;
                            sendTime = (overallTime - remoteTime) / 2;
                            recvTime = sendTime;
                        }

                        // Did we request an H2 upgrade and get a successful response?
                        if (requestedH2Upgrade)
                        {
                            if (response.ResponseCode == 101)
                            {
                                // Read the upgade headers
                                queryLogger.Log("Remote host accepts upgrade; proceeding with HTTP2 session");

                                // The code used to call FinishAsync, but that disconnects the socket entirely? That can't be right
                                //await response.FinishAsync(cancelToken, realTime).ConfigureAwait(false);

                                // We should now have an HTTP 101 (Switching Protocols) response which we read above.
                                // That response is finished and disposed. The actual content response comes via an H2 stream.
                                // So establish the H2 session and return that session's stream as the final respose.
                                HttpResponse actualContentResponse = await _h2SessionManager.Value.CreateH2ClientSessionFromUpgrade(
                                    clientSocket,
                                    _tcpConfig,
                                    _h2SessionPreferences,
                                    queryLogger,
                                    clientH2Settings,
                                    _metrics,
                                    _metricDimensions,
                                    cancelToken,
                                    realTime).ConfigureAwait(false);

                                // Signal the socket to not dispose because it is now owned by the HTTP/2.0 session
                                clientSocket = null;
                                return new NetworkResponseInstrumented<HttpResponse>(actualContentResponse, 0, 0, sendTime, remoteTime, recvTime);
                            }
                            else
                            {
                                _remoteServerHasRefusedH2Upgrade = true;
                            }
                        }

                        // Signal the socket to not dispose because it is now owned by the HTTP/1.1 response
                        clientSocket = null;
                        return new NetworkResponseInstrumented<HttpResponse>(response, 0, 0, sendTime, remoteTime, recvTime);
                    }
                    finally
                    {
                        // This is the fallthrough path to abort the connection in case something in the block above
                        // throws an exception. In general use the socket is null by this point.
                        clientSocket?.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                if (SocketHelpers.DoesExceptionIndicateSocketClosed(e))
                {
                    queryLogger.Log("Unhandled error while sending HTTP request: The connection was forcibly closed.", LogLevel.Err);
                }
                else if (SocketHelpers.DoesExceptionIndicateConnectionRefused(e))
                {
                    queryLogger.LogFormat(
                        LogLevel.Err,
                        DataPrivacyClassification.SystemMetadata,
                        "Unhandled error while sending HTTP request: {0}",
                        e.Message);
                }
                else
                {
                    queryLogger.Log("Unhandled error while sending HTTP request.", LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                }

                return new NetworkResponseInstrumented<HttpResponse>(null);
            }
        }

        public void SetReadTimeout(TimeSpan timeout)
        {
            _readTimeout = (int)timeout.TotalMilliseconds;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
            }
        }
    }
}
