using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Net.WebSocket
{
    /// <summary>
    /// Internal enumeration of websocket opcodes, defined by RFC 6455 section 5.2
    /// </summary>
    internal enum WebSocketOpcode : byte
    {
        /// <summary>
        /// The frame is a continuation of the previous fragmented frame.
        /// </summary>
        Continuation = 0x00,

        /// <summary>
        /// The frame contains text data.
        /// </summary>
        TextFrame = 0x01,

        /// <summary>
        /// The frame contains binary data.
        /// </summary>
        BinaryFrame = 0x02,

        /// <summary>
        /// The frame is a control frame requesting a graceful closing of the connection.
        /// </summary>
        CloseConnection = 0x08,

        /// <summary>
        /// The frame is a Ping control frame.
        /// </summary>
        Ping = 0x09,

        /// <summary>
        /// The frame is a Pong control frame.
        /// </summary>
        Pong = 0x0A
    }
}
