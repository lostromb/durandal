using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net
{
    /// <summary>
    /// Enumerates network send / receive directions, for orienting sockets
    /// based on which direction data is going, and to control different
    /// send/receive channels on those sockets.
    /// </summary>
    public enum NetworkDuplex
    {
        /// <summary>
        /// Unknown or no duplex
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Specifies the read capability of a network connection.
        /// </summary>
        Read = 0x1,

        /// <summary>
        /// Specifies the write capability of a network connection.
        /// </summary>
        Write = 0x2,

        /// <summary>
        /// Specifies the simulataneous read/write capability of a network connection.
        /// </summary>
        ReadWrite = 0x3,
    }
}
