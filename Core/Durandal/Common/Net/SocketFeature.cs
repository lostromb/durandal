using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net
{
    /// <summary>
    /// An enumeration of features that an <see cref="ISocket"/> can have, such as negotiated encryption protocols.
    /// </summary>
    public enum SocketFeature
    {
        /// <summary>
        /// Placeholder for the default value - needed for if we are parsing features or adding them to a dictionary
        /// </summary>
        Unknown,

        /// <summary>
        /// The socket connection is encrypted with any common SSL protocol. Feature value is empty.
        /// </summary>
        SecureConnection,

        /// <summary>
        /// The socket connection is encrypted using TLS. Feature value is a <see cref="Version"/> of the protocol version.
        /// </summary>
        TlsProtocolVersion,

        /// <summary>
        /// The socket connection is operating entirely within local shared memory, bypassing any actual network hardware. Feature value is empty.
        /// </summary>
        MemorySocket,

        /// <summary>
        /// The socket connection is pooled and potentially reused between connections. Feature value is empty.
        /// </summary>
        PooledConnection,

        // <summary>
        // Indicates that someone else initiated this socket connection to you. Feature value is empty.
        // </summary>
        //IsServer,

        /// <summary>
        /// The socket connection has negotiated the use of the HTTP/2 protocol during the TLS handshake. Feature value is empty.
        /// </summary>
        NegotiatedHttp2Support,
    }
}
