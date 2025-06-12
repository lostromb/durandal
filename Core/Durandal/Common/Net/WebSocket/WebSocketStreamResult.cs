using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.WebSocket
{
    /// <summary>
    /// Represents the result of a websocket read operation where the message has been piped to a stream.
    /// </summary>
    public sealed class WebSocketStreamResult : WebSocketReceiveResult
    {
        /// <summary>
        /// Constructs a receive result indicating a success.
        /// </summary>
        /// <param name="messageType">The type of message received.</param>
        /// <param name="messageLength">The message length that was written to the stream.</param>
        public WebSocketStreamResult(WebSocketMessageType messageType, long messageLength) : base()
        {
            MessageType = messageType;
            MessageLength = messageLength;
        }

        /// <summary>
        /// Constructs a receive result indicating that the connection has closed.
        /// </summary>
        /// <param name="closeReason">The reason code.</param>
        /// <param name="closeMessage">An option string containing debug data sent by the remote peer.</param>
        /// <param name="messageLength">The amount of data that may have been written to the output stream before an unexpected closure.</param>
        public WebSocketStreamResult(WebSocketCloseReason closeReason, string closeMessage, long messageLength = 0) : base(closeReason, closeMessage)
        {
            MessageLength = messageLength;
        }
        /// <summary>
        /// The type of message that was received.
        /// </summary>
        public WebSocketMessageType MessageType { get; private set; }

        /// <summary>
        /// The number of bytes that were read as part of this message.
        /// </summary>
        public long MessageLength { get; private set; }
    }
}
