using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.WebSocket
{
    /// <summary>
    /// Represents the result of a websocket read operation where the message is contained in a single buffer.
    /// </summary>
    public sealed class WebSocketBufferResult : WebSocketReceiveResult, IDisposable
    {
        /// <summary>
        /// Constructs a receive result indicating a success.
        /// </summary>
        /// <param name="messageType">The type of message received.</param>
        /// <param name="result">A buffer containing the full message.</param>
        public WebSocketBufferResult(WebSocketMessageType messageType, PooledBuffer<byte> result) : base()
        {
            MessageType = messageType;
            Result = result.AssertNonNull(nameof(result));
        }

        /// <summary>
        /// Constructs a receive result indicating that the connection has closed.
        /// </summary>
        /// <param name="closeReason">The reason code.</param>
        /// <param name="closeMessage">An option string containing debug data sent by the remote peer.</param>
        public WebSocketBufferResult(WebSocketCloseReason closeReason, string closeMessage) : base(closeReason, closeMessage)
        {
        }

        /// <summary>
        /// The type of message that was received.
        /// </summary>
        public WebSocketMessageType MessageType { get; private set; }

        /// <summary>
        /// The buffer containing the full message data.
        /// </summary>
        public PooledBuffer<byte> Result { get; private set; }

        public void Dispose()
        {
            Result?.Dispose();
        }
    }
}
