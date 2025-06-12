using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.WebSocket
{
    /// <summary>
    /// Defines a type of data for a WebSocket message. This is a public subset of all websocket opcodes.
    /// </summary>
    public enum WebSocketMessageType
    {
        /// <summary>
        /// The message data is unknown (usually implies that there is no data).
        /// </summary>
        Unknown,

        /// <summary>
        /// The message is UTF-8 formatted text.
        /// </summary>
        Text,

        /// <summary>
        /// The message is binary whose interpretation is up to the application.
        /// </summary>
        Binary
    }
}
