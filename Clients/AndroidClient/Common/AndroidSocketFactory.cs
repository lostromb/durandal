using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.Net.Security;
using System.Net.Sockets;
using Java.Security.Cert;
using System.Security.Authentication;
using Durandal.Common.Logger;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Durandal.Common.Net;
using System.Threading;
using Durandal.Common.Time;

using DurandalNet = Durandal.Common.Net;
using JavaNet = Java.Net;

namespace Durandal.AndroidClient.Common
{
    public class AndroidSocketFactory : DurandalNet.ISocketFactory
    {
        private SslProtocols _defaultSslProtocol;
        private ILogger _logger;
        private bool _ignoreCertErrors;

        public AndroidSocketFactory(ILogger logger = null,
            SslProtocols defaultSslProtocol = SslProtocols.None,
            bool ignoreCertErrors = false)
        {
            _defaultSslProtocol = defaultSslProtocol;
            _ignoreCertErrors = ignoreCertErrors;
            _logger = logger ?? NullLogger.Singleton;
            this.NoDelay = false;
        }

        /// <summary>
        /// If true, disable nagling to send packets with lower latency at the expense of data overhead
        /// </summary>
        public bool NoDelay
        {
            get; set;
        }

        public async Task<DurandalNet.ISocket> Connect(string hostname, int port, bool secure, ILogger traceLogger = null, CancellationToken cancelToken = default, IRealTimeProvider realTime = null)
        {
            DurandalNet.TcpConnectionConfiguration connectionParams = new DurandalNet.TcpConnectionConfiguration()
            {
                DnsHostname = hostname,
                SslHostname = hostname,
                Port = port,
                UseTLS = secure,
                NoDelay = NoDelay,
                ReportHttp2Capability = secure
            };

            return await Connect(connectionParams);
        }

        public async Task<DurandalNet.ISocket> Connect(TcpConnectionConfiguration connectionConfig, ILogger traceLogger = null, CancellationToken cancelToken = default, IRealTimeProvider realTime = null)
        {
            if (connectionConfig.ClientCertificate != null)
            {
                throw new NotImplementedException("Client certificates are not implemented yet on Android");
            }

            int port = connectionConfig.Port.GetValueOrDefault(connectionConfig.UseTLS ? 443 : 80);
            JavaNet.Socket rawSocket = await Task<JavaNet.Socket>.Run(async () =>
            {
                //_logger.Log("Starting to create a Java socket");
                JavaNet.Socket javaSocket = new JavaNet.Socket();
                JavaNet.InetSocketAddress addr = new JavaNet.InetSocketAddress(connectionConfig.DnsHostname, port);
                javaSocket.TcpNoDelay = NoDelay;
                //_logger.Log("Connecting socket");
                await javaSocket.ConnectAsync(addr);
                //_logger.Log("Socket connected");
                return javaSocket;
            });

            //_logger.Log("Wrapping with AndroidSocket");
            AndroidSocket socket = new AndroidSocket(rawSocket);

            if (connectionConfig.UseTLS)
            {
                //_logger.Log("Creating multiplexed stream");
                Stream networkStream = socket.CreateMultiplexedStream();
                SslStream secureLayer;
                SslClientAuthenticationOptions clientAuthOptions = new SslClientAuthenticationOptions()
                {
                    TargetHost = connectionConfig.SslHostname,
                    EnabledSslProtocols = _defaultSslProtocol,
                    ApplicationProtocols = new List<SslApplicationProtocol>()
                    {
                        SslApplicationProtocol.Http11,
                        SslApplicationProtocol.Http2
                    }
                };

                if (_ignoreCertErrors)
                {
                    clientAuthOptions.RemoteCertificateValidationCallback = IgnoreErrorsCertValidationCallback;
                    clientAuthOptions.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
                }

                //_logger.Log("Creating SSL stream");
                secureLayer = new SslStream(networkStream, false);
                //_logger.Log("Authenticating SSL");
                await secureLayer.AuthenticateAsClientAsync(clientAuthOptions, cancelToken).ConfigureAwait(false); ;

                //_logger.Log("Creating final socket object");
                return new AndroidSslSocket(socket, secureLayer);
            }
            else
            {
                return socket;
            }
        }

        public void Dispose() { }

        private static bool IgnoreErrorsCertValidationCallback(
            object sender,
            System.Security.Cryptography.X509Certificates.X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}