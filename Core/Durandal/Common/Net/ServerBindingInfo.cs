using Durandal.Common.Security;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Durandal.Common.Utils;
using Durandal.Common.Net.Http;

namespace Durandal.Common.Net
{
    /// <summary>
    /// Represents an instruction for a local (IP/socket-based) server to
    /// bind to a specified IP / port / hostname on the current machine.
    /// A single server may bind to multiple endpoints or multiple network
    /// interfaces, with mixed configurations for each, so this keeps
    /// all of that configuration in one place.
    /// This class mostly operates at the TCP layer but sometimes makes
    /// assumptions based on HTTP (mostly in regards to whether TLS is
    /// enabled for an endpoint).
    /// </summary>
    public class ServerBindingInfo
    {
        private static readonly Regex URI_PARSE_REGEX = new Regex("(.+?)://(.+?):([0-9]+)/?");
        public static string WILDCARD_HOSTNAME = "*";

        /// <summary>
        /// A string representing the local IP address to bind to.
        /// Can either be an IPv4, IPv6 string, or "*"
        /// to represent "bind to whatever's available"
        /// </summary>
        public string LocalIpEndpoint { get; private set; }

        /// <summary>
        /// The local IP port to bind to. If null, bind
        /// to any available port.
        /// </summary>
        public int? LocalIpPort { get; private set; }

        /// <summary>
        /// If <see cref="UseTls"/> is enabled, this is the identifier
        /// of the certificate to use for server verification.
        /// </summary>
        public CertificateIdentifier TlsCertificateIdentifier { get; private set; }

        /// <summary>
        /// If true, use server certificates and establish secure TLS connections
        /// with clients.
        /// </summary>
        public bool UseTls => TlsCertificateIdentifier != null;

        /// <summary>
        /// Whether this server endpoint should negotiate support for the HTTP/2 protocol during TLS handshake.
        /// </summary>
        public bool SupportHttp2 { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this binding is for the wildcard endpoint "*", meaning
        /// any available network interface
        /// </summary>
        public bool IsWildcardEndpoint => string.Equals(WILDCARD_HOSTNAME, LocalIpEndpoint, StringComparison.Ordinal);

        /// <summary>
        /// Creates a new server binding descriptor.
        /// </summary>
        /// <param name="endpoint">The local IP endpoint to use, or "*" to use any available address.</param>
        /// <param name="port">The desired IP port to bind to, or null to use any available port</param>
        /// <param name="certificateId">The identifier for the certificate that this server will use.
        /// Setting this value will set <see cref="UseTls"/> to be true.</param>
        /// <param name="supportHttp2">Whether this endpoint should negotiate support for HTTP/2 protocol.</param>
        public ServerBindingInfo(string endpoint, int? port, CertificateIdentifier certificateId = null, bool supportHttp2 = true)
        {
            LocalIpEndpoint = endpoint.AssertNonNullOrEmpty(nameof(endpoint));
            if (port.HasValue)
            {
                if (port.Value <= 0 || port.Value > 65535)
                {
                    throw new ArgumentOutOfRangeException("Port number is out of range");
                }
            }

            LocalIpPort = port;
            TlsCertificateIdentifier = certificateId;
            SupportHttp2 = supportHttp2;
        }

        public override string ToString()
        {
            string endpointString = WILDCARD_HOSTNAME;
            if (!string.IsNullOrEmpty(LocalIpEndpoint))
            {
                endpointString = LocalIpEndpoint;
            }

            string portString = "0";
            if (LocalIpPort.HasValue)
            {
                portString = LocalIpPort.Value.ToString();
            }

            string scheme = UseTls ? "tls" : "tcp";
            return string.Format("{0}://{1}:{2}", scheme, endpointString, portString);
        }

        /// <summary>
        /// Returns a server binding descriptor that represents any available
        /// interface and any available port
        /// </summary>
        /// <returns>A wildcard server binding descriptor</returns>
        public static ServerBindingInfo Wildcard()
        {
            return new ServerBindingInfo(WILDCARD_HOSTNAME, port: null);
        }

        /// <summary>
        /// Returns a server binding descriptor that represents any available
        /// interface with the given port
        /// </summary>
        /// <param name="port">The desired port number</param>
        /// <returns>A wildcard server binding descriptor</returns>
        public static ServerBindingInfo WildcardHost(int port)
        {
            return new ServerBindingInfo(WILDCARD_HOSTNAME, port);
        }

        /// <summary>
        /// Attempts to build a local server binding from an HTTP url string.<br/>
        /// Valid formats:<br/>
        /// http://localhost:80,<br/>
        /// https://*:443 (wildcard IP),<br/>
        /// https://your-secure-hostname.com:443 (use hostname for SSL subject name),<br/>
        /// https://localhost:0 (bind to any post)
        /// </summary>
        /// <param name="endpointString"></param>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        public static ServerBindingInfo BuildFromUriString(string endpointString)
        {
            Match match = URI_PARSE_REGEX.Match(endpointString);
            if (!match.Success)
            {
                throw new FormatException("Could not parse \"" + endpointString + "\" as a server binding URI");
            }

            bool tls = string.Equals(HttpConstants.SCHEME_HTTPS, match.Groups[1].Value, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(HttpConstants.SCHEME_WSS, match.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
            string endpoint = match.Groups[2].Value;
            int parsedPort;
            int? port = null;
            if (!int.TryParse(match.Groups[3].Value, out parsedPort))
            {
                throw new FormatException("Could not parse \"" + endpointString + "\" as a server binding URI: Invalid port");
            }

            if (parsedPort != 0)
            {
                port = parsedPort;
            }

            CertificateIdentifier certificateId = null;
            if (tls)
            {
                if (string.IsNullOrEmpty(endpoint) || string.Equals(WILDCARD_HOSTNAME, endpoint, StringComparison.Ordinal))
                {
                    certificateId = CertificateIdentifier.BySubjectName("localhost");
                }
                else
                {
                    certificateId = CertificateIdentifier.BySubjectName(endpoint);
                }

                // This is a bit of a limitation with the string parser - we can't specify a binding IP and a server
                // certificate host name in the same string. So we capture the certificate host and then set
                // endpoint to a wildcard so the underlying server can decide how to bind it
                endpoint = WILDCARD_HOSTNAME;
            }
            
            return new ServerBindingInfo(endpoint, port, certificateId);
        }

        /// <summary>
        /// Converts a list of url strings such as "http://*:80", "https://hostname.com:443",
        /// "http://localhost:0" to a list of structured server bindings.
        /// This method is somewhat limited in that it can't specify both a binding port and
        /// TLS host name in the same string.
        /// </summary>
        /// <param name="serverBindingStrings">The input list of endpoint strings</param>
        /// <returns>A parsed list of bindings</returns>
        public static IList<ServerBindingInfo> ParseBindingList(IEnumerable<string> serverBindingStrings)
        {
            IList<ServerBindingInfo> returnVal = new List<ServerBindingInfo>();
            foreach (var rawEndpoint in serverBindingStrings)
            {
                returnVal.Add(BuildFromUriString(rawEndpoint));
            }

            return returnVal;
        }
    }
}
