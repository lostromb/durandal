using Durandal.Common.Logger;
using Durandal.Common.Security;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net
{
    /// <summary>
    /// The most common implementation of <see cref="ISocketFactory"/>, using TCP sockets from the System.Net.Sockets namespace.
    /// </summary>
    public class RawTcpSocketFactory : ISocketFactory
    {
        private readonly TimeSpan _connectTimeout;
        private readonly SslProtocols _defaultSslProtocol;
        private readonly bool _ignoreCertErrors;
        private readonly ILogger _logger;
        private int _disposed = 0;

        public RawTcpSocketFactory(
            ILogger logger = null,
            SslProtocols defaultSslProtocol = SslProtocols.None,
            bool ignoreCertErrors = false,
            TimeSpan? connectTimeout = null)
        {
            _defaultSslProtocol = defaultSslProtocol;
#if NETFRAMEWORK
            if (_defaultSslProtocol == SslProtocols.None)
            {
                // .net framework doesn't yet support "none" to mean "OS enforced", so select TLS 1.2 as the default in that case
                _defaultSslProtocol = SslProtocols.Tls12;
            }
#endif
            _connectTimeout = connectTimeout.GetValueOrDefault(TimeSpan.FromSeconds(5));
            _logger = logger ?? NullLogger.Singleton;
            _ignoreCertErrors = ignoreCertErrors;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RawTcpSocketFactory()
        {
            Dispose(false);
        }
#endif

        public async Task<ISocket> Connect(
            string hostname,
            int port,
            bool secure,
            ILogger traceLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            traceLogger = traceLogger ?? _logger;

            TcpConnectionConfiguration connectionParams = new TcpConnectionConfiguration()
            {
                DnsHostname = hostname,
                SslHostname = hostname,
                Port = port,
                UseTLS = secure,
                NoDelay = true
            };

            return await Connect(connectionParams, traceLogger, cancelToken, realTime).ConfigureAwait(false);
        }

        public async Task<ISocket> Connect(
            TcpConnectionConfiguration connectionConfig,
            ILogger traceLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            traceLogger = traceLogger ?? _logger;
            connectionConfig.AssertNonNull(nameof(connectionConfig));
            if (!connectionConfig.Port.HasValue)
            {
                throw new ArgumentNullException("TCP port must be specified");
            }

            cancelToken.ThrowIfCancellationRequested();

#if !NETFRAMEWORK
            // We have to do some extra stuff here to make sure we have a reasonable connection timeout
            using (NonRealTimeCancellationTokenSource timeoutCancelToken = new NonRealTimeCancellationTokenSource(realTime, _connectTimeout))
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancelToken.Token, cancelToken))
            {
#endif
                //  Use try-finally to make sure we conditionally dispose of this on any error paths.

/* Unmerged change from project 'Durandal.NetCore'
Before:
            Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
After:
                System.Net.Sockets.Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
*/
            System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(SocketType.Stream, ProtocolType.Tcp);
            try
            {
                if (connectionConfig.MinBufferSize.HasValue)
                {
                    if (socket.ReceiveBufferSize < connectionConfig.MinBufferSize)
                    {
                        socket.ReceiveBufferSize = connectionConfig.MinBufferSize.Value;
                    }
                    if (socket.SendBufferSize < connectionConfig.MinBufferSize)
                    {
                        socket.SendBufferSize = connectionConfig.MinBufferSize.Value;
                    }
                }

                if (connectionConfig.NoDelay.HasValue)
                {
                    socket.NoDelay = connectionConfig.NoDelay.Value;
                }

                // Load client certificates first, if required
                X509CertificateCollection clientCerts = null;
                if (connectionConfig.ClientCertificate != null)
                {
                    clientCerts = CertificateCache.Instance.GetCertificates(connectionConfig.ClientCertificate, realTime);

                    if (clientCerts == null || clientCerts.Count == 0)
                    {
                        traceLogger.Log("Client certificate with ID " + connectionConfig.ClientCertificate + " was not found on the local machine!", LogLevel.Err);
                    }
                }

#if NETFRAMEWORK
                // Workaround to simulate a connection timeout in an environment where it's not directly supported
                Task timeoutTask = realTime.WaitAsync(_connectTimeout, cancelToken);
                Task connectTask = socket.ConnectAsync(new DnsEndPoint(connectionConfig.DnsHostname, connectionConfig.Port.Value));
                Task finishedTask = await Task.WhenAny(timeoutTask, connectTask).ConfigureAwait(false);
                if (finishedTask == timeoutTask)
                {
                    throw new SocketException((int)SocketError.TimedOut);
                }

                await connectTask.ConfigureAwait(false);
#else
                await socket.ConnectAsync(new DnsEndPoint(connectionConfig.DnsHostname, connectionConfig.Port.Value), linkedCts.Token).ConfigureAwait(false);
#endif
                cancelToken.ThrowIfCancellationRequested();

                if (connectionConfig.UseTLS)
                {
                    string sslHostname = string.IsNullOrEmpty(connectionConfig.SslHostname) ? connectionConfig.DnsHostname : connectionConfig.SslHostname;
                    NetworkStream stream = new NetworkStream(socket, true);
#if NETFRAMEWORK
                    Task<SslStream> negotiateTlsTask = NegotiateTLSLegacy(
                        stream,
                        sslHostname,
                        clientCerts,
                        _defaultSslProtocol,
                        _ignoreCertErrors);

                    // reuse the same timeout task as the connection did above
                    finishedTask = await Task.WhenAny(timeoutTask, negotiateTlsTask).ConfigureAwait(false);
                    if (finishedTask == timeoutTask)
                    {
                        throw new SocketException((int)SocketError.TimedOut);
                    }

                    SslStream secureLayer = await negotiateTlsTask.ConfigureAwait(false);
#else
                    SslStream secureLayer = await NegotiateTLS(
                        stream,
                        sslHostname,
                        clientCerts,
                        _defaultSslProtocol,
                        _ignoreCertErrors,
                        connectionConfig.ReportHttp2Capability,
                        linkedCts.Token);
#endif
                    cancelToken.ThrowIfCancellationRequested();
                    SslStreamSocket returnVal = new SslStreamSocket(
                        socket,
                        secureLayer,
                        ownsSocket: true);
                    socket = null;
                    return returnVal;
                }
                else
                {
                    RawTcpSocket returnVal = new RawTcpSocket(socket, ownsSocket: true);
                    socket = null;
                    return returnVal;
                }
            }
            finally
            {
                socket?.Dispose();
            }
#if !NETFRAMEWORK
            } // using scope for the linked cancellation token source
#endif
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
