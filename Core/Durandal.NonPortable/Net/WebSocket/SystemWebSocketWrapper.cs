using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DurandalWS = Durandal.Common.Net.WebSocket;
using SystemWS = System.Net.WebSockets;

namespace Durandal.Common.Net.WebSocket
{
    /// <summary>
    /// Implementation of <see cref="DurandalWS.IWebSocket"/> which wraps the default .Net WebSocket client.
    /// </summary>
    public class SystemWebSocketWrapper : DurandalWS.IWebSocket
    {
        // Fragment messages to 64Kb by default.
        // 14 here is the maximum size of a single frame header.
        private static readonly int FRAGMENTATION_THRESHOLD = 65536 - 14;
        private readonly SystemWS.WebSocket _socket;
        private int _disposed = 0;

        public SystemWebSocketWrapper(SystemWS.WebSocket socket)
        {
            _socket = socket.AssertNonNull(nameof(socket));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~SystemWebSocketWrapper()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public string SubProtocol => _socket.SubProtocol;

        /// <inheritdoc />
        public DurandalWS.WebSocketState State
        {
            get
            {
                return Convert(_socket.State);
            }
        }

        /// <inheritdoc />
        public DurandalWS.WebSocketCloseReason? RemoteCloseReason
        {
            get
            {
                if (!_socket.CloseStatus.HasValue)
                {
                    return null;
                }
                else
                {
                    return Convert(_socket.CloseStatus.Value);
                }
            }
        }

        /// <inheritdoc />
        public string RemoteCloseMessage
        {
            get
            {
                return _socket.CloseStatusDescription;
            }
        }

        /// <inheritdoc />
        public async Task CloseWrite(
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            WebSocketCloseReason? status = null,
            string debugCloseMessage = null)
        {
            await _socket.CloseOutputAsync(
                (SystemWS.WebSocketCloseStatus)(status.GetValueOrDefault(DurandalWS.WebSocketCloseReason.NormalClosure)),
                debugCloseMessage,
                cancelToken).ConfigureAwait(false);
        }

        public async Task WaitForGracefulClose(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, cancelToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async ValueTask<WebSocketBufferResult> ReceiveAsBufferAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            PooledBuffer<byte> scratch = BufferPool<byte>.Rent();
            try
            {
                SystemWS.WebSocketReceiveResult rr = await _socket.ReceiveAsync(scratch.AsArraySegment, cancelToken);

                if (rr.MessageType == SystemWS.WebSocketMessageType.Close)
                {
                    // Socket connection has been closed. Return failure message
                    return new DurandalWS.WebSocketBufferResult(Convert(rr.CloseStatus.GetValueOrDefault(WebSocketCloseStatus.Empty)), rr.CloseStatusDescription);
                }

                // Catch the message type of the first packet in case there are continuation frames.
                DurandalWS.WebSocketMessageType messageType = Convert(rr.MessageType);
                if (rr.EndOfMessage)
                {
                    // Got the entire message in one buffer. Pass it along directly
                    scratch.Shrink(rr.Count);
                    DurandalWS.WebSocketBufferResult returnVal = new DurandalWS.WebSocketBufferResult(messageType, scratch);
                    scratch = null;
                    return returnVal;
                }
                else
                {
                    // Buffer the rest of the message in multiple successive reads.
                    using (RecyclableMemoryStream buffer = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                    {
                        if (rr.Count > 0)
                        {
                            buffer.Write(scratch.Buffer, 0, rr.Count);
                        }

                        // Loop until we get the actual end of the message.
                        while (!rr.EndOfMessage)
                        {
                            rr = await _socket.ReceiveAsync(scratch.AsArraySegment, cancelToken);

                            if (rr.MessageType == SystemWS.WebSocketMessageType.Close)
                            {
                                return new DurandalWS.WebSocketBufferResult(Convert(rr.CloseStatus.GetValueOrDefault(WebSocketCloseStatus.Empty)), rr.CloseStatusDescription);
                            }
                            else if (rr.Count > 0)
                            {
                                buffer.Write(scratch.Buffer, 0, rr.Count);
                            }
                        }

                        // Then convert the buffer to a pooled instance (not the most ideal allocation-wise, but if you're
                        // sending messages larger than 256Kb each then that's a design problem)
                        return new DurandalWS.WebSocketBufferResult(messageType, buffer.ToPooledBuffer());
                    }
                }
            }
            finally
            {
                scratch?.Dispose();
            }
        }

        /// <inheritdoc />
        public async ValueTask<DurandalWS.WebSocketStreamResult> ReceiveAsStreamAsync(NonRealTimeStream destinationStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            PooledBuffer<byte> scratch = BufferPool<byte>.Rent();
            try
            {
                SystemWS.WebSocketReceiveResult rr = await _socket.ReceiveAsync(scratch.AsArraySegment, cancelToken);

                if (rr.MessageType == SystemWS.WebSocketMessageType.Close)
                {
                    // Socket connection has been closed. Return failure message
                    return new DurandalWS.WebSocketStreamResult(Convert(rr.CloseStatus.GetValueOrDefault(WebSocketCloseStatus.Empty)), rr.CloseStatusDescription);
                }

                // Catch the message type of the first packet in case there are continuation frames.
                DurandalWS.WebSocketMessageType messageType = Convert(rr.MessageType);
                long totalMessageLength = rr.Count;
                if (rr.EndOfMessage)
                {
                    // Got the entire message in one read.
                    if (rr.Count > 0)
                    {
                        await destinationStream.WriteAsync(scratch.Buffer, 0, rr.Count, cancelToken, realTime).ConfigureAwait(false);
                    }

                    return new DurandalWS.WebSocketStreamResult(messageType, totalMessageLength);
                }
                else
                {
                    if (rr.Count > 0)
                    {
                        await destinationStream.WriteAsync(scratch.Buffer, 0, rr.Count, cancelToken, realTime).ConfigureAwait(false);
                    }

                    // Loop until we get the actual end of the message.
                    while (!rr.EndOfMessage)
                    {
                        rr = await _socket.ReceiveAsync(scratch.AsArraySegment, cancelToken);

                        if (rr.MessageType == SystemWS.WebSocketMessageType.Close)
                        {
                            return new DurandalWS.WebSocketStreamResult(Convert(rr.CloseStatus.GetValueOrDefault(WebSocketCloseStatus.Empty)), rr.CloseStatusDescription);
                        }
                        else if (rr.Count > 0)
                        {
                            await destinationStream.WriteAsync(scratch.Buffer, 0, rr.Count, cancelToken, realTime).ConfigureAwait(false);
                            totalMessageLength += rr.Count;
                        }
                    }

                    return new DurandalWS.WebSocketStreamResult(messageType, totalMessageLength);
                }
            }
            finally
            {
                scratch?.Dispose();
            }
        }

        /// <inheritdoc />
        public async ValueTask<bool> SendAsync(ArraySegment<byte> buffer, DurandalWS.WebSocketMessageType messageType, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            SystemWS.WebSocketMessageType convertedMessageType = Convert(messageType);
            int bytesSent = 0;
            bool isLastFrame = false;
            while (!isLastFrame)
            {
                int thisFrameSize = FastMath.Min(buffer.Count - bytesSent, FRAGMENTATION_THRESHOLD);
                isLastFrame = bytesSent + thisFrameSize == buffer.Count;
                await _socket.SendAsync(
                    new ArraySegment<byte>(buffer.Array, buffer.Offset + bytesSent, thisFrameSize),
                    convertedMessageType,
                    isLastFrame,
                    cancelToken).ConfigureAwait(false);

                bytesSent += thisFrameSize;
            }

            return _socket.State == SystemWS.WebSocketState.Open || _socket.State == SystemWS.WebSocketState.CloseSent;
        }

        /// <inheritdoc />
        public async ValueTask<bool> SendAsync(NonRealTimeStream stream, DurandalWS.WebSocketMessageType messageType, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            SystemWS.WebSocketMessageType convertedMessageType = Convert(messageType);
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent(FRAGMENTATION_THRESHOLD))
            {
                int bytesReadFromStream = 1;
                while (bytesReadFromStream > 0)
                {
                    bytesReadFromStream = await stream.ReadAsync(scratch.Buffer, 0, FRAGMENTATION_THRESHOLD, cancelToken, realTime).ConfigureAwait(false);
                    if (bytesReadFromStream > 0)
                    {
                        await _socket.SendAsync(new ArraySegment<byte>(scratch.Buffer, 0, bytesReadFromStream), convertedMessageType, false, cancelToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _socket.SendAsync(new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY), convertedMessageType, true, cancelToken).ConfigureAwait(false);
                    }
                }
            }

            return _socket.State == SystemWS.WebSocketState.Open || _socket.State == SystemWS.WebSocketState.CloseSent;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                // We don't really care about graceful shutdown here.
                // The only place this would matter is if there is still data to be sent or received
                // that was in-transit when the shutdown began. But since we are disposing of the client,
                // we have to assume that the caller of this code doesn't care about that data anyways.
                //if (_socket.State == SystemWS.WebSocketState.CloseReceived)
                //{
                //    _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).Await();
                //}

                _socket.Dispose();
            }
        }

        private static DurandalWS.WebSocketState Convert(SystemWS.WebSocketState state)
        {
            switch (state)
            {
                case SystemWS.WebSocketState.Open:
                    return DurandalWS.WebSocketState.Open;
                case SystemWS.WebSocketState.Closed:
                    return DurandalWS.WebSocketState.Closed;
                case SystemWS.WebSocketState.CloseReceived:
                    return DurandalWS.WebSocketState.HalfClosedRemote;
                case SystemWS.WebSocketState.CloseSent:
                    return DurandalWS.WebSocketState.HalfClosedLocal;
                case SystemWS.WebSocketState.Connecting:
                    return DurandalWS.WebSocketState.Connecting;
                case SystemWS.WebSocketState.None:
                    return DurandalWS.WebSocketState.Unknown;
                case SystemWS.WebSocketState.Aborted:
                    return DurandalWS.WebSocketState.Aborted;
                default:
                    return DurandalWS.WebSocketState.Unknown;
            }
        }

        private static DurandalWS.WebSocketMessageType Convert(SystemWS.WebSocketMessageType type)
        {
            switch (type)
            {
                case SystemWS.WebSocketMessageType.Text:
                    return DurandalWS.WebSocketMessageType.Text;
                case SystemWS.WebSocketMessageType.Binary:
                    return DurandalWS.WebSocketMessageType.Binary;
                default:
                    return DurandalWS.WebSocketMessageType.Unknown;
            }
        }

        private static SystemWS.WebSocketMessageType Convert(DurandalWS.WebSocketMessageType type)
        {
            switch (type)
            {
                case DurandalWS.WebSocketMessageType.Text:
                    return SystemWS.WebSocketMessageType.Text;
                case DurandalWS.WebSocketMessageType.Binary:
                    return SystemWS.WebSocketMessageType.Binary;
                default:
                    throw new ArgumentException("Invalid websocket message type");
            }
        }

        private static DurandalWS.WebSocketCloseReason Convert(SystemWS.WebSocketCloseStatus status)
        {
            switch (status)
            {
                case SystemWS.WebSocketCloseStatus.NormalClosure:
                    return DurandalWS.WebSocketCloseReason.NormalClosure;
                case SystemWS.WebSocketCloseStatus.ProtocolError:
                    return DurandalWS.WebSocketCloseReason.ProtocolError;
                case SystemWS.WebSocketCloseStatus.InternalServerError:
                    return DurandalWS.WebSocketCloseReason.InternalServerError;
                case SystemWS.WebSocketCloseStatus.Empty:
                    return DurandalWS.WebSocketCloseReason.Empty;
                case SystemWS.WebSocketCloseStatus.InvalidMessageType:
                    return DurandalWS.WebSocketCloseReason.InvalidMessageType;
                case SystemWS.WebSocketCloseStatus.MandatoryExtension:
                    return DurandalWS.WebSocketCloseReason.MandatoryExtension;
                case SystemWS.WebSocketCloseStatus.PolicyViolation:
                    return DurandalWS.WebSocketCloseReason.PolicyViolation;
                case SystemWS.WebSocketCloseStatus.EndpointUnavailable:
                    return DurandalWS.WebSocketCloseReason.EndpointUnavailable;
                case SystemWS.WebSocketCloseStatus.InvalidPayloadData:
                    return DurandalWS.WebSocketCloseReason.InvalidPayloadData;
                case SystemWS.WebSocketCloseStatus.MessageTooBig:
                    return DurandalWS.WebSocketCloseReason.MessageTooBig;
                default:
                    return DurandalWS.WebSocketCloseReason.Empty;
            }
        }
    }
}
