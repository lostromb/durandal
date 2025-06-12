using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using Durandal.Common.Time;

namespace Durandal.Common.Net.WebSocket
{
    /// <summary>
    /// Represents an endpoint of the WebSocket protocol (either as a server or a client),
    /// according to RFC 6455 (as well as extensions such as RFC 8441 for HTTP2).
    /// </summary>
    public interface IWebSocket : IDisposable
    {
        /// <summary>
        /// The specific subprotocol that was negotiated between client and server at
        /// connection time, if any were specified.
        /// </summary>
        string SubProtocol { get; }

        /// <summary>
        /// Gets the current connection state of the socket.
        /// </summary>
        WebSocketState State { get; }

        /// <summary>
        /// If the connection has been half-closed by the remote endpoint, this is the close reason they sent.
        /// </summary>
        WebSocketCloseReason? RemoteCloseReason { get; }

        /// <summary>
        /// If the connection has been half-closed by the remote endpoint and they sent a close message,
        /// it will be stored here.
        /// </summary>
        string RemoteCloseMessage { get; }

        /// <summary>
        /// Begins graceful shutdown of a websocket connection by sending a close message to the
        /// remote endpoint. You may not send data during or after the close operation, but
        /// you may continue to receive valid messages until the remote endpoint acknowledges the close.
        /// </summary>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A real time definition.</param>
        /// <param name="status">The status code to send to the remote peer.</param>
        /// <param name="debugCloseMessage">The debug message to send to the remote peer, if any.</param>
        /// <returns>An async task.</returns>
        Task CloseWrite(
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            WebSocketCloseReason? status = null,
            string debugCloseMessage = null);

        Task WaitForGracefulClose(CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Sends a buffer of data to the remote endpoint as a complete message.
        /// </summary>
        /// <param name="buffer">The message to send.</param>
        /// <param name="messageType">The message type.</param>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>True if the message was written successfully.</returns>
        ValueTask<bool> SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Sends the contents of a stream of data to the remote endpoint as a complete message.
        /// </summary>
        /// <param name="stream">The stream containing data to send.</param>
        /// <param name="messageType">The message type.</param>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>True if the message was written successfully.</returns>
        ValueTask<bool> SendAsync(NonRealTimeStream stream, WebSocketMessageType messageType, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Blocks until the remote peer either sends a complete message, or closes the connection.
        /// Returns a read result indicating either way.
        /// </summary>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>A read result containing either a close status,
        /// or a buffer containing a complete message along with its type.</returns>
        ValueTask<WebSocketBufferResult> ReceiveAsBufferAsync(CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Blocks until the remote peer either sends a complete message, or closes the connection.
        /// The incoming message is written directly to the given stream.
        /// In exceptional cases, it is possible that some data may be written to the stream before
        /// the connection is abruptly closed. In that case, your application will have to handle the
        /// partially written data after receiving the close result.
        /// </summary>
        /// <param name="destinationStream">The stream to write the incoming message to.</param>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>A read result containing either a close status,
        /// or metadata about the message which was written to the destination stream.</returns>
        ValueTask<WebSocketStreamResult> ReceiveAsStreamAsync(NonRealTimeStream destinationStream, CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}
