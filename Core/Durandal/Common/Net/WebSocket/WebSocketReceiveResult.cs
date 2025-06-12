using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.WebSocket
{
    /// <summary>
    /// Abstract result for any WebSocket Receive() operation. Contains metadata
    /// about whether a message was actually received, and if not, conveys
    /// the close status and potentially extra debugging data.
    /// </summary>
    public abstract class WebSocketReceiveResult
    {
        /// <summary>
        /// Constructs a receive result indicating a success.
        /// </summary>
        public WebSocketReceiveResult()
        {
            Success = true;
            CloseReason = null;
            CloseMessage = null;
        }

        /// <summary>
        /// Constructs a receive result indicating that the connection has closed.
        /// </summary>
        /// <param name="closeReason">The reason code.</param>
        /// <param name="closeMessage">An option string containing debug data sent by the remote peer.</param>
        public WebSocketReceiveResult(WebSocketCloseReason closeReason, string closeMessage)
        {
            Success = false;
            CloseReason = closeReason;
            CloseMessage = closeMessage;
        }

        /// <summary>
        /// Whether a message was successfully received. If this is false,
        /// it implies that the socket has closed.
        /// </summary>
        public bool Success { get; private set; }

        /// <summary>
        /// If the socket has closed, this contains the reason
        /// for the closure reported by the remote endpoint.
        /// </summary>
        public WebSocketCloseReason? CloseReason { get; private set; }

        /// <summary>
        /// If the socket closed and the remote endpoint sent a
        /// reason message, it will be stored here.
        /// </summary>
        public string CloseMessage { get; private set; }
    }
}
