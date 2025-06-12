using Durandal.Common.Tasks;

namespace Durandal.Common.Net
{
    using Durandal.API;
    using Durandal.Common.Cache;
    using Durandal.Common.Collections;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Security;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The most common implementation of <see cref="ISocketServer"/>, using TCP sockets from the System.Net.Sockets namespace.
    /// You cannot inherit from this class; because of inversion-of-control, you have to create an ISocketServerDelegate class
    /// and then pass if to this instance's RegisterSubclass function. This allows the superclass to be abstract and the subclass to be common,
    /// which allows for better portability.
    /// </summary>
    public sealed class RawTcpSocketServer : ISocketServer
    {
        /// <summary>
        /// Maximum number of socket event args to cache
        /// </summary>
        private const int SOCKET_CACHE_SIZE = 1024;

        private const int SOCKET_BACKLOG_SIZE = 128;

        private readonly List<SocketEndpointListener> _endpointListenThreads;
        private readonly WeakPointer<IThreadPool> _threadPool;
        private readonly WeakPointer<IMetricCollector> _metrics; 
        private readonly ILogger _logger;
        private readonly LockFreeCache<SocketAsyncEventArgs> _socketPool;
        private readonly IRealTimeProvider _realTime;

        /// <summary>
        /// Theoretical endpoints given at construction time may contain wildcards
        /// </summary>
        private readonly IList<ServerBindingInfo> _rawEndpoints;

        /// <summary>
        /// The actually bound endpoints, no wildcards, available only after server has started
        /// </summary>
        private readonly IList<ServerBindingInfo> _boundEndpoints;

        /// <summary>
        /// CTS for shutting down the entire server
        /// </summary>
        private readonly CancellationTokenSource _serverShutdownSignal = new CancellationTokenSource();

        private volatile bool _isRunning;
        private ISocketServerDelegate _serverImpl;
        private int _disposed = 0;

        public RawTcpSocketServer(
            IEnumerable<ServerBindingInfo> endpoints,
            ILogger logger,
            IRealTimeProvider realTime,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet dimensions,
            WeakPointer<IThreadPool> requestThreadPool)
        {
            _logger = logger;
            _rawEndpoints = new List<ServerBindingInfo>(endpoints.AssertNonNull(nameof(endpoints)));
            _boundEndpoints = new List<ServerBindingInfo>();
            _isRunning = false;
            _threadPool = requestThreadPool.AssertNonNull(nameof(requestThreadPool));
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            _socketPool = new LockFreeCache<SocketAsyncEventArgs>(SOCKET_CACHE_SIZE);
            _endpointListenThreads = new List<SocketEndpointListener>();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RawTcpSocketServer()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public async Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_serverImpl == null)
            {
                throw new InvalidOperationException("Cannot start a socket server because it is still abstract. You must call RegisterSubclass to provide a server implementation before starting.");
            }

            _boundEndpoints.Clear();
            bool startedOk = true;
            foreach (ServerBindingInfo endpoint in _rawEndpoints)
            {
                // Parse the protocol and endpoint
                IPAddress bindingIP;
                if (string.IsNullOrEmpty(endpoint.LocalIpEndpoint) ||
                    string.Equals(ServerBindingInfo.WILDCARD_HOSTNAME, endpoint.LocalIpEndpoint, StringComparison.Ordinal) ||
                    string.Equals("127.0.0.1", endpoint.LocalIpEndpoint, StringComparison.Ordinal) ||
                    //string.Equals("[::1]", endpoint.LocalIpEndpoint, StringComparison.Ordinal) ||
                    string.Equals("localhost", endpoint.LocalIpEndpoint, StringComparison.OrdinalIgnoreCase))
                {
                    bindingIP = IPAddress.Any;
                }
                else if (!IPAddress.TryParse(endpoint.LocalIpEndpoint, out bindingIP))
                {
                    throw new Exception("Cannot parse the server binding \"" + endpoint.LocalIpEndpoint + "\" as an IP endpoint");
                }

                if (endpoint.UseTls && endpoint.TlsCertificateIdentifier == null)
                {
                    throw new InvalidOperationException("Attempted to bind a TLS endpoint but no TLS certificate is specified! Endpoint: " + endpoint.ToString());
                }

                // Check that the SSL certificate exists. Let the certificate cache handle actually storing it though.
                if (endpoint.UseTls)
                {
                    if (endpoint.TlsCertificateIdentifier == null)
                    {
                        throw new ArgumentNullException("TLS certificate identifier is null but UseTLS is true");
                    }

                    X509Certificate2 endpointCertificate = CertificateCache.Instance.GetCertificate(endpoint.TlsCertificateIdentifier, realTime);
                    if (endpointCertificate == null)
                    {
                        _logger.LogFormat(LogLevel.Err, DataPrivacyClassification.SystemMetadata,
                            "Failed to find SSL certificate with identifier {0} in all cert stores", endpoint.TlsCertificateIdentifier);
                    }
                    else
                    {
                        _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Binding socket server listener to {0} with certificate {1} {2}", endpoint, endpointCertificate.GetCertHashString(), endpointCertificate.Subject);
                    }
                }
                else
                {
                    _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                        "Binding socket server listener to {0}", endpoint);
                }

                SocketEndpointListener listener = new SocketEndpointListener(
                    _serverShutdownSignal.Token,
                    bindingIP,
                    endpoint.LocalIpPort,
                    _logger,
                    _socketPool,
                    NewConnection,
                    endpoint.TlsCertificateIdentifier,
                    endpoint);
                _endpointListenThreads.Add(listener);
                startedOk = (await listener.Start().ConfigureAwait(false)) && startedOk;
                if (!startedOk)
                {
                    // I really have to decide whether to keep this "return false on failure" or "raise exceptions to caller" design.
                    return false;
                }

                IPEndPoint actualEndpoint = listener.ActualEndpoint as IPEndPoint;
                if (actualEndpoint == null)
                {
                    throw new Exception("Could not determine socket server endpoint after binding. Endpoint definition: " + endpoint);
                }

                string actualIpHost;
                if (IPAddress.Any.Equals(actualEndpoint.Address))
                {
                    actualIpHost = "127.0.0.1";
                }
                else
                {
                    actualIpHost = actualEndpoint.Address.ToString();
                }

                if (IPAddress.Any.Equals(bindingIP) ||
                    !endpoint.LocalIpPort.HasValue)
                {
                    _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                            "Actual endpoint after wildcard binding was {0}:{1}", actualIpHost, actualEndpoint.Port);
                }

                _boundEndpoints.Add(new ServerBindingInfo(
                    actualIpHost,
                    actualEndpoint.Port,
                    endpoint.TlsCertificateIdentifier));
            }

            _isRunning = true;
            return startedOk;
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

            StopServer(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _serverShutdownSignal.Dispose();
            }
        }

        public async Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _isRunning = false;
            _serverShutdownSignal.Cancel();
            foreach (SocketEndpointListener listener in _endpointListenThreads)
            {
                await listener.WaitForStop().ConfigureAwait(false);
            }
        }
        
        public void RegisterSubclass(ISocketServerDelegate subclass)
        {
            _serverImpl = subclass;
        }

        public IEnumerable<ServerBindingInfo> Endpoints
        {
            get
            {
                if (_isRunning)
                {
                    return _boundEndpoints;
                }
                else
                {
                    return _rawEndpoints;
                }
            }
        }

        public bool Running
        {
            get
            {
                return _isRunning;
            }
        }

        /// <summary>
        /// Helper method to find an open port on the local machine
        /// </summary>
        /// <returns></returns>
        public static int GetAnyAvailablePort()
        {
            System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            int port = ((IPEndPoint)socket.LocalEndPoint).Port;
            socket.Close();
            return port;
        }

        /// <summary>
        /// Returns the list of all IP addresses bound to the local machine.
        /// </summary>
        /// <returns></returns>
        public static IList<IPAddress> GetLocalMachineAddresses()
        {
            List<IPAddress> returnVal = new List<IPAddress>();

            // Get host name for local machine
            string strHostName = Dns.GetHostName();
            if (string.IsNullOrEmpty(strHostName))
            {
                return returnVal;
            }

            // Then using host name, get the IP address list..
            IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);

            if (ipEntry == null)
            {
                return returnVal;
            }

            returnVal.FastAddRangeList(ipEntry.AddressList);
            return returnVal;
        }

        /// <summary>
        /// This class actually hosts a listener socket on a specific instance of host + port
        /// A virtual server can bind do multiple endpoints and thus can have multiple of these listeners
        /// </summary>
        private class SocketEndpointListener : IDisposable
        {
            private readonly CancellationToken local_cancelToken;
            private readonly ILogger local_logger;
            private readonly IPAddress local_bindingIP;
            private readonly int? local_bindingPort;
            private readonly LockFreeCache<SocketAsyncEventArgs> local_socketPool;
            private readonly EventHandler<SocketAsyncEventArgs> local_newConnection;
            private readonly CertificateIdentifier local_tlsCertificateId;
            private readonly ServerBindingInfo local_serverBinding;

            private readonly AutoResetEventAsync _newConnectionSignal = new AutoResetEventAsync(false);
            private readonly ManualResetEventAsync _stoppedSignal = new ManualResetEventAsync();
            private bool _startedOk = false;
            private Task _listenThread;
            private EndPoint _actualEndpoint = null;
            private int _disposed = 0;

            public SocketEndpointListener(
                CancellationToken cancelToken,
                IPAddress endpoint,
                int? port,
                ILogger logger,
                LockFreeCache<SocketAsyncEventArgs> socketPool,
                EventHandler<SocketAsyncEventArgs> newConnectionHandler,
                CertificateIdentifier tlsCertificateId,
                ServerBindingInfo serverBinding)
            {
                local_cancelToken = cancelToken;
                local_bindingIP = endpoint;
                local_bindingPort = port;
                local_logger = logger;
                local_socketPool = socketPool;
                local_newConnection = newConnectionHandler;
                local_tlsCertificateId = tlsCertificateId;
                local_serverBinding = serverBinding;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~SocketEndpointListener()
            {
                Dispose(false);
            }
#endif

            public Task<bool> Start()
            {
                _listenThread = Run();
                return Task.FromResult(_startedOk);
            }

            public EndPoint ActualEndpoint => _actualEndpoint;

            public Task WaitForStop()
            {
                using (CancellationTokenSource cancelToken = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000)))
                {
                    return _stoppedSignal.WaitAsync(cancelToken.Token);
                }
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
                    //_newConnectionSignal.Dispose();
                }
            }

            private async Task Run()
            {
                try
                {
                    using (System.Net.Sockets.Socket serverSocket = new System.Net.Sockets.Socket(
                        AddressFamily.InterNetwork,
                        SocketType.Stream,
                        ProtocolType.Tcp))
                    {
                        serverSocket.Bind(new IPEndPoint(local_bindingIP, local_bindingPort.GetValueOrDefault(0)));
                        _actualEndpoint = serverSocket.LocalEndPoint;
                        serverSocket.Listen(SOCKET_BACKLOG_SIZE);
                        _startedOk = true;

                        // This yield is here so a synchronous caller of Run() will go and do something else rather than blocking
                        // on the socket accept method which could take forever (see the Start() method)
                        await Task.Yield();

                        while (!local_cancelToken.IsCancellationRequested)
                        {
                            SocketAsyncEventArgs socketArgs = local_socketPool.TryDequeue();
                            if (socketArgs == null)
                            {
                                socketArgs = new SocketAsyncEventArgs();
                                socketArgs.Completed += local_newConnection;
                            }

                            WeakPointer<X509Certificate2> bindingCert;
                            if (local_tlsCertificateId == null)
                            {
                                bindingCert = new WeakPointer<X509Certificate2>();
                            }
                            else
                            {
                                bindingCert = new WeakPointer<X509Certificate2>(CertificateCache.Instance.GetCertificate(local_tlsCertificateId, DefaultRealTimeProvider.Singleton));
                            }
                            
                            socketArgs.AcceptSocket = null;
                            socketArgs.UserToken = new NewConnectionArgs()
                                {
                                    PooledSocket = socketArgs,
                                    NewConnectionSignal = _newConnectionSignal,
                                    TlsCertificate = bindingCert,
                                    LocalBinding = local_serverBinding,
                                };

                            // FIXME: In some configurations, AcceptAsync() does not return immediately,
                            // instead it waits 1 second before it dispatches the request to the delegate.
                            // I believe this is because it waiting for additional data to be written to its buffer but then is timing out.
                            bool willRaiseEvent = serverSocket.AcceptAsync(socketArgs);

                            if (!willRaiseEvent)
                            {
                                // if not async, we need to handle the request right here
                                // This will still dispatch the actual work to the thread pool, so
                                // we shouldn't worry about this taking a lot of time
                                local_newConnection(null, socketArgs);
                            }

                            await _newConnectionSignal.WaitAsync(local_cancelToken);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (SocketException e)
                {
                    local_logger.Log("Error: Socket exception occurred while starting HTTP server. The port may already be in use.", LogLevel.Err);
                    local_logger.Log(e, LogLevel.Err);
                    _startedOk = false;
                }
                catch (Exception e)
                {
                    local_logger.Log(e, LogLevel.Err);
                    _startedOk = false;
                }
                finally
                {
                    _stoppedSignal.Set();
                }
            }
        }

        private class NewConnectionArgs
        {
            public SocketAsyncEventArgs PooledSocket;
            public AutoResetEventAsync NewConnectionSignal;
            public WeakPointer<X509Certificate2> TlsCertificate;
            public ServerBindingInfo LocalBinding;
        }

        private void NewConnection(object sender, SocketAsyncEventArgs args)
        {
            try
            {
                NewConnectionArgs arguments = args.UserToken as NewConnectionArgs;
                arguments.NewConnectionSignal.Set();
                SocketAsyncEventArgs pooledSocket = arguments.PooledSocket;
                if (pooledSocket == null)
                {
                    _logger.Log("No pool handle. Something is wonky in http socket server", LogLevel.Err);
                    return;
                }

                if (args.SocketError == SocketError.Success)
                {
                    _threadPool.Value.EnqueueUserAsyncWorkItem(
                        new RequestThreadWrapper(
                            args.AcceptSocket,
                            _logger,
                            _serverImpl.HandleSocketConnection,
                            _socketPool,
                            pooledSocket,
                            arguments.TlsCertificate,
                            arguments.LocalBinding,
                            _serverShutdownSignal.Token,
                            _realTime)
                        .Run);
                }
                else
                {
                    _logger.Log("Unexpected error in accept socket: " + args.SocketError, LogLevel.Err);
                }
            }
            catch (Exception e)
            {
                _logger.Log("Unexpected error while accepting socket connection", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
        }

        private delegate Task ConnectionHandler(ISocket clientSocket, ServerBindingInfo bindPoint, CancellationToken cancelToken, IRealTimeProvider realTime);

        private class RequestThreadWrapper
        {
            private readonly System.Net.Sockets.Socket _rawSocket;
            private readonly IRealTimeProvider _threadLocalTime;
            private readonly ConnectionHandler _handler;
            private readonly ILogger _logger;
            private readonly LockFreeCache<SocketAsyncEventArgs> _socketPool;
            private readonly CancellationToken _cancellationSource;
            private readonly WeakPointer<X509Certificate2> _serverCertificate;
            private readonly ServerBindingInfo _localBinding;
            private SocketAsyncEventArgs _pooledSocket;

            public RequestThreadWrapper(
                System.Net.Sockets.Socket rawSocket,
                ILogger logger,
                ConnectionHandler handler,
                LockFreeCache<SocketAsyncEventArgs> socketPool,
                SocketAsyncEventArgs pooledSocket,
                WeakPointer<X509Certificate2> serverCertificate,
                ServerBindingInfo localBinding,
                CancellationToken cancellationSource,
                IRealTimeProvider realTime)
            {
                _rawSocket = rawSocket.AssertNonNull(nameof(rawSocket));
                _handler = handler.AssertNonNull(nameof(handler));
                _socketPool = socketPool.AssertNonNull(nameof(socketPool));
                _logger = logger.AssertNonNull(nameof(logger));
                _pooledSocket = pooledSocket.AssertNonNull(nameof(pooledSocket));
                _localBinding = localBinding.AssertNonNull(nameof(localBinding));
                _cancellationSource = cancellationSource;
                _serverCertificate = serverCertificate; // may be null if no SSL enabled
                _threadLocalTime = realTime.Fork("RawTcpSocketServerThread");
            }
            
            public async Task Run()
            {
                ISocket socketWrapper = null;
                try
                {
                    // We need to carefully manage ownership of objects here
                    // _rawSocket is part of a resource pool and should not be disposed here.
                    // So, we pass ownsSocket=false to all of these constructors and then use a try-finally to dispose of _rawSocket.
                    if (_serverCertificate.Value != null)
                    {
                        // Negotiate SSL connection here on the request thread
                        NetworkStream newStream = new NetworkStream(_rawSocket, ownsSocket: false);
#if NETFRAMEWORK
                        SslStream secureLayer = await NegotiateTLSLegacy(
                            newStream,
                            _serverCertificate.Value);
#else
                        SslStream secureLayer = await NegotiateTLS(
                            newStream,
                            _serverCertificate.Value,
                            _localBinding.SupportHttp2,
                            _cancellationSource);
#endif

                        socketWrapper = new SslStreamSocket(_rawSocket, secureLayer, ownsSocket: false);
                    }
                    else
                    {
                        socketWrapper = new RawTcpSocket(_rawSocket, ownsSocket: false);
                    }

                    await _handler(socketWrapper, _localBinding, _cancellationSource, _threadLocalTime).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    socketWrapper?.Dispose();
                    _rawSocket?.Dispose();
                    // Recycle the socket event args if possible to avoid reallocating a bunch of kernel stuff
                    _socketPool.TryEnqueue(_pooledSocket);
                    _threadLocalTime.Merge();
                }
            }



            /// <summary>
            /// Negotiates an SSL connection using the legacy .Net Framework invocation pattern,
            /// which doesn't support ALPN or system-default protocol suites.
            /// </summary>
            /// <param name="innerStream"></param>
            /// <param name="serverCert"></param>
            /// <returns></returns>
            private static async Task<SslStream> NegotiateTLSLegacy(
                NetworkStream innerStream,
                X509Certificate2 serverCert)
            {
                SslStream secureStream = new SslStream(innerStream, leaveInnerStreamOpen: false);
                await secureStream.AuthenticateAsServerAsync(serverCert, false, SslProtocols.Tls12 | SslProtocols.Tls13, checkCertificateRevocation: true).ConfigureAwait(false);
                return secureStream;
            }

#if NETCOREAPP
            /// <summary>
            /// Negotiates a TLS connection using modern SslClientAuthenticationOptions
            /// </summary>
            /// <param name="innerStream"></param>
            /// <param name="serverCert"></param>
            /// <param name="supportHttp2"></param>
            /// <param name="cancelToken"></param>
            /// <returns></returns>
            private static async Task<SslStream> NegotiateTLS(
                NetworkStream innerStream,
                X509Certificate2 serverCert,
                bool supportHttp2,
                CancellationToken cancelToken)
            {
                SslServerAuthenticationOptions authOptions = new SslServerAuthenticationOptions()
                {
                    ServerCertificate = serverCert,
                    EnabledSslProtocols = SslProtocols.None,
                    ApplicationProtocols = new List<SslApplicationProtocol>(),
                };

                if (supportHttp2)
                {
                    authOptions.ApplicationProtocols.Add(SslApplicationProtocol.Http2);
                }

                authOptions.ApplicationProtocols.Add(SslApplicationProtocol.Http11);

                SslStream secureStream = new SslStream(innerStream, false);
                await secureStream.AuthenticateAsServerAsync(authOptions, cancelToken).ConfigureAwait(false);
                return secureStream;
            }
#endif
        }
    }
}
