using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2
{
    /// <summary>
    /// Key used to identify a single unique HTTP/2 session with a specific host, so
    /// we can reuse existing sessions without creating unnecessary new ones.
    /// </summary>
    internal struct Http2SessionKey : IEquatable<Http2SessionKey>
    {
        /// <summary>
        /// Creates a new <see cref="Http2SessionKey"/>
        /// </summary>
        /// <param name="scheme">The connection scheme e.g. "https"</param>
        /// <param name="hostName">The hostname string of the remote host</param>
        /// <param name="port">The remote port number</param>
        public Http2SessionKey(string scheme, string hostName, int? port)
        {
            Scheme = scheme.AssertNonNullOrEmpty(nameof(scheme));
            Hostname = hostName.AssertNonNullOrEmpty(nameof(hostName));
            Port = port;
        }

        /// <summary>
        /// The scheme of the connection, e.g. "https"
        /// </summary>
        public string Scheme { get; set; }

        /// <summary>
        /// The DNS hostname of the remote host
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// The remote port number of the connection
        /// </summary>
        public int? Port { get; set; }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return Equals((Http2SessionKey)obj);
        }

        public bool Equals(Http2SessionKey other)
        {
            return string.Equals(Scheme, other.Scheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Scheme, other.Scheme, StringComparison.OrdinalIgnoreCase) &&
                Port == other.Port;
        }

        public override int GetHashCode()
        {
            return Scheme.GetHashCode() ^ Hostname.GetHashCode() ^ Port.GetHashCode();
        }
    }
}
