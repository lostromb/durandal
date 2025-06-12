using Durandal.Common.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net
{
    public class TcpConnectionConfiguration : IEquatable<TcpConnectionConfiguration>
    {
        /// <summary>
        /// The DNS hostname to connect to, e.g. "widgets.com"
        /// </summary>
        public string DnsHostname { get; set; }

        /// <summary>
        /// The SSL hostname to connect to, if different from DNS hostname.
        /// This is only useful in very specific cases, such as when you want to directly connect to an endpoint behind a traffic manager while still impersonating the public-facing hostname.
        /// </summary>
        public string SslHostname { get; set; }

        /// <summary>
        /// The remote port to connect to
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// If true, use SSL for this connection
        /// </summary>
        public bool UseTLS { get; set; }

        /// <summary>
        /// If true, disable nagling to send packets with lower latency at the expense of data overhead
        /// </summary>
        public bool? NoDelay { get; set; }

        /// <summary>
        /// Guarantee that the socket send/recv buffers will always be at least this large
        /// </summary>
        public int? MinBufferSize { get; set; }

        /// <summary>
        /// If UseSSL is true and this is non-null, attempt to load the certificate with the specified thumbprint and use it for client authentication during the SSL handshake.
        /// </summary>
        public CertificateIdentifier ClientCertificate { get; set; }

        /// <summary>
        /// If true, negotiate the "h2" capability using the ALPN feature of the TLS handshake, if possible. This allows
        /// the remote endpoint to report whether it supports HTTP2 before we make a request.
        /// </summary>
        public bool ReportHttp2Capability { get; set; }

        /// <summary>
        /// Returns the value that represents the externally-facing domain name that this connection is trying to reach.
        /// There's a rare case where we can connect to a server directly by IP (such as using a backend
        /// route) but still want to use the external domain name for SSL. In this case, prefer the
        /// SSL hostname for the HTTP Host: header as well.
        /// </summary>
        public string HostHeaderValue
        {
            get
            {
                return string.IsNullOrEmpty(SslHostname) ? DnsHostname : SslHostname;
            }
        }

        public TcpConnectionConfiguration() { }

        public TcpConnectionConfiguration(string hostname, int port, bool useSsl = false)
        {
            DnsHostname = hostname;
            Port = port;
            UseTLS = useSsl;
            ReportHttp2Capability = true;
        }

        public override bool Equals(object obj)
        {
            TcpConnectionConfiguration other = obj as TcpConnectionConfiguration;

            if (obj == null)
            {
                return false;
            }

            return Equals(other);
        }

        public bool Equals(TcpConnectionConfiguration other)
        {
            return string.Equals(DnsHostname, other.DnsHostname) &&
                string.Equals(SslHostname, other.SslHostname, StringComparison.Ordinal) &&
                Port == other.Port &&
                UseTLS == other.UseTLS &&
                NoDelay == other.NoDelay &&
                MinBufferSize == other.MinBufferSize &&
                ReportHttp2Capability == other.ReportHttp2Capability &&
                Equals(ClientCertificate, other.ClientCertificate);
        }

        public override int GetHashCode()
        {
            return (DnsHostname == null ? 0 : (DnsHostname.GetHashCode() * 0x07)) ^
                (SslHostname == null ? 0 : (SslHostname.GetHashCode() * 0x1E)) ^
                (Port.GetHashCode() << 4) ^
                (UseTLS.GetHashCode() << 8) ^
                (ReportHttp2Capability.GetHashCode() << 12) ^
                (!NoDelay.HasValue ? 0 : NoDelay.Value.GetHashCode()) ^
                (!MinBufferSize.HasValue ? 0 : MinBufferSize.Value.GetHashCode()) ^
                (ClientCertificate == null ? 0 : ClientCertificate.GetHashCode());
        }
    }
}
