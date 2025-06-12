using Durandal.Common.Logger;
using Durandal.Common.Security;
using Durandal.Common.Cache;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Instrumentation;
using Durandal.Common.Utils;
using Durandal.API;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Net
{
    public class PooledTcpClientSocketFactory : ISocketFactory
    {
        private static readonly TimeSpan SOCKET_LINGER_TIME = TimeSpan.FromSeconds(10);
        private const int MAX_CONNECTIONS_PER_HOST = 4;
        
        // TODO have a watchdog task to peek() at the pool and proactively dispose of expired sockets, rather then expiring them on-demand
        private readonly IDictionary<TcpConnectionConfiguration, Queue<LingeringSocket>> _socketPool = new Dictionary<TcpConnectionConfiguration, Queue<LingeringSocket>>();
        
        private readonly SemaphoreSlim _lock;
        private readonly SslProtocols _defaultSslProtocol;
        private readonly bool _ignoreCertErrors;
        private readonly ILogger _logger;
        private readonly IMetricCollector _metrics;
        private readonly DimensionSet _metricDimensions;

        private int _disposed = 0;
        private int _activeConnectionCount = 0;
        private int _lingeringConnectionCount = 0;

        public PooledTcpClientSocketFactory(ILogger logger,
            IMetricCollector metrics,
            DimensionSet metricDimensions,
            SslProtocols defaultSslProtocol = SslProtocols.None,
            bool ignoreCertErrors = false)
        {
            _ignoreCertErrors = ignoreCertErrors;
            _defaultSslProtocol = defaultSslProtocol;
            _logger = logger ?? NullLogger.Singleton;
            _metrics = metrics ?? NullMetricCollector.Singleton;
            _metricDimensions = metricDimensions ?? DimensionSet.Empty;
            _lock = new SemaphoreSlim(1);

#if NETFRAMEWORK
            if (_defaultSslProtocol == SslProtocols.None)
            {
                // .net framework doesn't yet support "none" to mean "OS enforced", so select TLS 1.2 as the default in that case
                _defaultSslProtocol = SslProtocols.Tls12;
            }
#endif

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PooledTcpClientSocketFactory()
        {
            Dispose(false);
        }
#endif

        public Task<ISocket> Connect(
            string hostname,
            int port,
            bool secure,
            ILogger traceLogger = null, 
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            traceLogger = traceLogger ?? _logger;

            // Is there already a socket available?
            TcpConnectionConfiguration connectionParams = new TcpConnectionConfiguration()
            {
                DnsHostname = hostname,
                Port = port,
                UseTLS = secure
            };

            return Connect(connectionParams, traceLogger, cancelToken, realTime);
        }

        public async Task<ISocket> Connect(
            TcpConnectionConfiguration connectionParams,
            ILogger traceLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            traceLogger = traceLogger ?? _logger;
            traceLogger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                "Socket is requested. Currently we have {0} lingering connections, {1} active connections", _lingeringConnectionCount, _activeConnectionCount);

            List<LingeringSocket> socketsToReclaim = new List<LingeringSocket>();
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_socketPool.ContainsKey(connectionParams))
                {
                    while (_socketPool[connectionParams].Count > 0)
                    {
                        LingeringSocket candidateSocket = _socketPool[connectionParams].Dequeue();
                        Interlocked.Decrement(ref _lingeringConnectionCount);

                        // Ensure the socket is still valid
                        if (!candidateSocket.Socket.Healthy || candidateSocket.LastUsedTime < realTime.Time - SOCKET_LINGER_TIME)
                        {
                            socketsToReclaim.Add(candidateSocket);
                        }
                        else
                        {
                            traceLogger.Log("Reusing pooled socket", LogLevel.Vrb);
                            Interlocked.Increment(ref _activeConnectionCount);
                            _metrics.ReportInstant("Pooled Sockets Reused/sec", _metricDimensions);
                            return candidateSocket.Socket;
                        }
                    }
                }
            }
            finally
            {
                _lock.Release();

                foreach (LingeringSocket obsoleteSocket in socketsToReclaim)
                {
                    traceLogger.Log("Disposing of old pooled socket", LogLevel.Vrb);
                    obsoleteSocket.Socket.Dispose();
                }
            }

            traceLogger.Log("Creating new pooled socket connection", LogLevel.Vrb);
            Interlocked.Increment(ref _activeConnectionCount);
            _metrics.ReportInstant("Pooled Sockets Created/sec", _metricDimensions);
            return await CreateNewConnection(connectionParams, traceLogger, cancelToken, realTime).ConfigureAwait(false);
        }

        private async Task<ISocket> CreateNewConnection(TcpConnectionConfiguration opts, ILogger traceLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            opts.AssertNonNull(nameof(opts));
            if (!opts.Port.HasValue)
            {
                throw new ArgumentNullException("TCP port must be specified");
            }

            TcpClient socket = new TcpClient();
            if (opts.MinBufferSize.HasValue)
            {
                if (socket.ReceiveBufferSize < opts.MinBufferSize)
                {
                    socket.ReceiveBufferSize = opts.MinBufferSize.Value;
                }
                if (socket.SendBufferSize < opts.MinBufferSize)
                {
                    socket.SendBufferSize = opts.MinBufferSize.Value;
                }
            }

            if (opts.NoDelay.HasValue)
            {
                socket.NoDelay = opts.NoDelay.Value;
            }

            // Load client certificates first, if required
            X509CertificateCollection clientCerts = null;
            if (opts.ClientCertificate != null)
            {
                clientCerts = CertificateCache.Instance.GetCertificates(opts.ClientCertificate, realTime);

                if (clientCerts == null || clientCerts.Count == 0)
                {
                    traceLogger.Log("Client certificate with ID " + opts.ClientCertificate + " was not found on the local machine!", LogLevel.Err);
                }
            }

#if NETFRAMEWORK
            await socket.ConnectAsync(opts.DnsHostname, opts.Port.Value).ConfigureAwait(false);
#else
            await socket.ConnectAsync(opts.DnsHostname, opts.Port.Value, cancelToken).ConfigureAwait(false);
#endif
            if (opts.UseTLS)
            {
                string sslHostname = string.IsNullOrEmpty(opts.SslHostname) ? opts.DnsHostname : opts.SslHostname;

                NetworkStream stream = socket.GetStream();
#if NETFRAMEWORK
                SslStream secureLayer = await NegotiateTLSLegacy(
                    stream,
                    sslHostname,
                    clientCerts,
                    _defaultSslProtocol,
                    _ignoreCertErrors);
#else
                SslStream secureLayer = await NegotiateTLS(
                    stream,
                    sslHostname,
                    clientCerts,
                    _defaultSslProtocol,
                    _ignoreCertErrors,
                    opts.ReportHttp2Capability,
                    CancellationToken.None);
#endif

                return new PooledSslStreamSocket(socket, secureLayer, (s, d, l) => ReclaimSocket(s, opts, d, l, realTime));
            }
            else
            {
                return new PooledTcpClientSocket(socket, (s, d, l) => ReclaimSocket(s, opts, d, l, realTime));
            }
        }

        /// <summary>
        /// Negotiates an SSL connection using the legacy .Net Framework invocation pattern,
        /// which doesn't support ALPN or system-default protocol suites.
        /// </summary>
        /// <param name="innerStream"></param>
        /// <param name="sslHostname"></param>
        /// <param name="clientCerts"></param>
        /// <param name="sslProtocol"></param>
        /// <param name="ignoreCertErrors"></param>
        /// <returns></returns>
        private static async Task<SslStream> NegotiateTLSLegacy(
            NetworkStream innerStream,
            string sslHostname,
            X509CertificateCollection clientCerts,
            SslProtocols sslProtocol,
            bool ignoreCertErrors)
        {
            SslStream secureStream;
            if (ignoreCertErrors)
            {
                secureStream = new SslStream(innerStream, false, IgnoreErrorsCertValidationCallback);
                await secureStream.AuthenticateAsClientAsync(sslHostname, clientCerts, sslProtocol, false).ConfigureAwait(false);
            }
            else
            {
                secureStream = new SslStream(innerStream, false);
                await secureStream.AuthenticateAsClientAsync(sslHostname, clientCerts, sslProtocol, true).ConfigureAwait(false);
            }

            return secureStream;
        }

#if NETCOREAPP
        /// <summary>
        /// Negotiates a TLS connection using modern SslClientAuthenticationOptions
        /// </summary>
        /// <param name="innerStream"></param>
        /// <param name="sslHostname"></param>
        /// <param name="clientCerts"></param>
        /// <param name="sslProtocol"></param>
        /// <param name="ignoreCertErrors"></param>
        /// <param name="supportHttp2"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        private static async Task<SslStream> NegotiateTLS(
            NetworkStream innerStream,
            string sslHostname,
            X509CertificateCollection clientCerts,
            SslProtocols sslProtocol,
            bool ignoreCertErrors,
            bool supportHttp2,
            CancellationToken cancelToken)
        {
            SslClientAuthenticationOptions authOptions = new SslClientAuthenticationOptions()
            {
                ClientCertificates = clientCerts,
                EnabledSslProtocols = sslProtocol,
                TargetHost = sslHostname,
                ApplicationProtocols = new List<SslApplicationProtocol>(),
            };

            if (ignoreCertErrors)
            {
                authOptions.RemoteCertificateValidationCallback = IgnoreErrorsCertValidationCallback;
            }

            if (supportHttp2)
            {
                authOptions.ApplicationProtocols.Add(SslApplicationProtocol.Http2);
            }

            authOptions.ApplicationProtocols.Add(SslApplicationProtocol.Http11);

            SslStream secureStream = new SslStream(innerStream, false);
            await secureStream.AuthenticateAsClientAsync(authOptions, cancelToken).ConfigureAwait(false);
            return secureStream;
        }
#endif

        private static bool IgnoreErrorsCertValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private void ReclaimSocket(
            IPooledSocket socket,
            TcpConnectionConfiguration connectionConfig,
            NetworkDuplex closingDuplex,
            bool allowLinger,
            IRealTimeProvider realTime)
        {
            Interlocked.Decrement(ref _activeConnectionCount);
            if (!allowLinger)
            {
                // Socket has requested immediate disconnect. Dispose of it
                _logger.Log("Socket has forbidden lingering. Disconnecting...", LogLevel.Vrb);
                socket.Dispose();
                return;
            }

            if (closingDuplex != NetworkDuplex.ReadWrite)
            {
                _logger.Log("Lingering on half-duplex connections is not supported", LogLevel.Vrb);
                socket.Dispose();
                return;
            }

            // Do a health check on the socket before returning it to the active pool
            if (!socket.Healthy)
            {
                _logger.Log("Socket is unhealthy so I will not reclaim it", LogLevel.Vrb);
                socket.Dispose();
                return;
            }

            _logger.Log("Reclaiming TCP socket", LogLevel.Vrb);
            List<LingeringSocket> socketsToReclaim = new List<LingeringSocket>();

            _lock.Wait();
            try
            {
                if (!_socketPool.ContainsKey(connectionConfig))
                {
                    _socketPool[connectionConfig] = new Queue<LingeringSocket>();
                }

                socket.MakeReadyForReuse();

                _socketPool[connectionConfig].Enqueue(new LingeringSocket()
                {
                    LastUsedTime = realTime.Time,
                    Socket = socket
                });
                Interlocked.Increment(ref _lingeringConnectionCount);

                // Is the number of lingering connections to one single host above the limit? Then cull a few connections here
                while (_socketPool[connectionConfig].Count > MAX_CONNECTIONS_PER_HOST)
                {
                    LingeringSocket oldSocket = _socketPool[connectionConfig].Dequeue();
                    socketsToReclaim.Add(oldSocket);
                }
            }
            finally
            {
                _lock.Release();
            }

            foreach (LingeringSocket obsoleteSocket in socketsToReclaim)
            {
                obsoleteSocket.Socket.Dispose();
                Interlocked.Decrement(ref _lingeringConnectionCount);
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
                _lock.Wait(1000);
                try
                {
                    // Close all connections in the pool
                    // TODO what can be done about all sockets that are currently leased?
                    foreach (Queue<LingeringSocket> socketConnection in _socketPool.Values)
                    {
                        foreach (LingeringSocket socket in socketConnection)
                        {
                            socket.Socket.Dispose();
                        }
                    }
                }
                finally
                {
                    _lock.Release();
                }

                _lock.Dispose();
            }
        }

        private class LingeringSocket
        {
            public IPooledSocket Socket { get; set; }
            public DateTimeOffset LastUsedTime { get; set; }
        }
    }
}
