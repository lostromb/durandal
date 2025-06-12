using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Utils.Cache;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.Util
{
    /// <summary>
    /// Factory class for creating TCP clients in a way that gives us almost total control over
    /// connection and SSL parameters. This is useful for us when we need to validate an HTTP GET
    /// request using more criteria than just "did it succeed"
    /// </summary>
    public class ModifiedTcpClientSocketFactory : ISocketFactory
    {
        private SslProtocols _defaultSslProtocol;
        private ILogger _logger;
        private SslCertValidationProcedure _validationProcedure;
        private string _lastSslError = null;

        public ModifiedTcpClientSocketFactory(
            ILogger logger = null,
            SslProtocols defaultSslProtocol = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
            SslCertValidationProcedure certValidationProcedure = SslCertValidationProcedure.StandardPlusNearExpiry)
        {
            _defaultSslProtocol = defaultSslProtocol;
            _validationProcedure = certValidationProcedure;
            _logger = logger ?? new NullLogger();
            this.NoDelay = true;
        }

        /// <summary>
        /// If true, disable nagling to send packets with lower latency at the expense of data overhead
        /// </summary>
        public bool NoDelay
        {
            get; set;
        }

        /// <summary>
        /// Guarantee that the socket send/recv buffers will always be at least this large
        /// </summary>
        public int MinBufferSize
        {
            get; set;
        }

        public async Task<ISocket> Connect(string hostname, int port, bool secure, string sslHostname = null)
        {
            // Is there already a socket available?
            TcpConnectionConfiguration connectionParams = new TcpConnectionConfiguration()
            {
                DnsHostname = hostname,
                SslHostname = string.IsNullOrEmpty(sslHostname) ? hostname : sslHostname,
                Port = port,
                UseSSL = secure
            };

            return await ConnectInternal(connectionParams);
        }

        private async Task<ISocket> ConnectInternal(TcpConnectionConfiguration opts)
        {
            TcpClient socket = new TcpClient();
            if (socket.ReceiveBufferSize < MinBufferSize)
            {
                socket.ReceiveBufferSize = MinBufferSize;
            }
            if (socket.SendBufferSize < MinBufferSize)
            {
                socket.SendBufferSize = MinBufferSize;
            }

            socket.NoDelay = this.NoDelay;
            await socket.ConnectAsync(opts.DnsHostname, opts.Port);

            if (opts.UseSSL)
            {
                NetworkStream stream = socket.GetStream();
                SslStream secureLayer;
                try
                {
                    if (_validationProcedure == SslCertValidationProcedure.IgnoreErrors)
                    {
                        secureLayer = new SslStream(stream, false, IgnoreErrorsCertValidationCallback);
                        await secureLayer.AuthenticateAsClientAsync(opts.SslHostname, null, _defaultSslProtocol, false);
                    }
                    else if (_validationProcedure == SslCertValidationProcedure.StandardPlusNearExpiry)
                    {
                        secureLayer = new SslStream(stream, false, StandardPlusExpiryCertValidationCallback);
                        await secureLayer.AuthenticateAsClientAsync(opts.SslHostname, null, _defaultSslProtocol, true);
                    }
                    else
                    {
                        secureLayer = new SslStream(stream, false);
                        await secureLayer.AuthenticateAsClientAsync(opts.SslHostname, null, _defaultSslProtocol, true);
                    }

                    return new SslStreamSocket(socket.Client, secureLayer);
                }
                catch (AuthenticationException e)
                {
                    // Find the latest ssl policy error, if any
                    if (_lastSslError != null)
                    {
                        throw new AuthenticationException("Failed to establish a secure SSL/TLS connection with " + opts.DnsHostname + ". The error was: " + _lastSslError);
                    }
                    else
                    {
                        // If we don't know the error, just rethrow the exception
                        ExceptionDispatchInfo info = ExceptionDispatchInfo.Capture(e);
                        info.Throw();
                        return null; // dummy to satisfy compiler
                    }
                }
            }
            else
            {
                return new TcpClientSocket(socket);
            }
        }

        private bool IgnoreErrorsCertValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private bool StandardPlusExpiryCertValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            string expirationDate = certificate.GetExpirationDateString();
            string thumbprint = certificate.GetCertHashString();
            DateTimeOffset certExpireTime = DateTimeOffset.Parse(expirationDate, CultureInfo.InvariantCulture.DateTimeFormat, DateTimeStyles.AssumeUniversal);

            // Validate that cert is valid for at least the next week
            TimeSpan timeUntilExpire = certExpireTime - DateTimeOffset.UtcNow;
            if (timeUntilExpire < TimeSpan.Zero)
            {
                _lastSslError = string.Format("Certificate {0} ({1}) presented by server expired on {2}", certificate.Subject, thumbprint, expirationDate);
                return false;
            }
            else if (timeUntilExpire.TotalDays < 7)
            {
                _lastSslError = string.Format("Certificate {0} ({1}) presented by server is set to expire in {2:F1} days", certificate.Subject, thumbprint, timeUntilExpire.TotalDays);
                return false;
            }

            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            _lastSslError = string.Format("Certificate {0} ({1}) presented by server had errors: {2}", certificate.Subject, thumbprint, sslPolicyErrors.ToString());
            return false;
        }

        public void Dispose()
        {
        }
    }
}
