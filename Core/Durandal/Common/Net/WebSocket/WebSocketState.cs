using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.WebSocket
{
    /// <summary>
    /// Enumerates the states of a web socket connection.
    /// </summary>
    public enum WebSocketState
    {
        /// <summary>
        /// Socket is in an unknown state.
        /// </summary>
        Unknown,

        /// <summary>
        /// The connection is negotiating the handshake with the remote endpoint.
        /// </summary>
        Connecting,

        /// <summary>
        /// The connection is active and data may be sent in either direction.
        /// </summary>
        Open,

        /// <summary>
        /// A close message was sent by this client. There may still be valid messages to read.
        /// </summary>
        HalfClosedLocal,

        /// <summary>
        /// A close message was sent by the remote endpoint. We may still send outgoing
        /// messages, but it is assumed that the next one will be a close acknowledgment.
        /// </summary>
        HalfClosedRemote,

        /// <summary>
        /// Indicates the WebSocket close handshake completed gracefully.
        /// </summary>
        Closed,

        /// <summary>
        /// Indicates that the websocket connection closed because of the underlying
        /// transport layer - for example the TCP socket being forcibly closed.
        /// </summary>
        Aborted
    }
}
