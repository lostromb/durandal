using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Net.Http2.Frames
{
    /// <summary>
    /// The base class for an HTTP/2 frame. Contains at minimum a stream ID, flag field, and (potentially empty) payload buffer.
    /// Subclasses can define their own parameters.
    /// </summary>
    public abstract class Http2Frame : IDisposable
    {
        /// <summary>
        /// The raw frame data - may be null
        /// </summary>
        protected PooledBuffer<byte> _framePayload;

        /// <summary>
        /// The length of this frame's entire data
        /// </summary>
        public int PayloadLength => _framePayload == null ? 0 : _framePayload.Length;

        /// <summary>
        /// The type of frame this is
        /// </summary>
        public abstract Http2FrameType FrameType { get; }

        /// <summary>
        /// The flag field of this frame
        /// </summary>
        public byte Flags { get; private set; }

        /// <summary>
        /// The stream ID of this frame
        /// </summary>
        public int StreamId { get; protected set; }

        /// <summary>
        /// The network direction of this frame relative to this peer.
        /// </summary>
        public NetworkDirection Direction { get; private set; }

        private int _disposed = 0;

        /// <summary>
        /// Constructs a new abstract HTTP2 frame.
        /// </summary>
        /// <param name="direction">The network direction</param>
        /// <param name="payload">A frame payload (can be null)</param>
        /// <param name="flags">Flag set</param>
        /// <param name="streamId">Stream ID</param>
        protected Http2Frame(NetworkDirection direction, PooledBuffer<byte> payload, byte flags, int streamId)
        {
            Flags = flags;
            StreamId = streamId;
            Direction = direction;
            _framePayload = payload;

            if (streamId < 0)
            {
                throw new Http2ProtocolException("Stream ID cannot be negative");
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~Http2Frame()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Writes the data of this frame to an outgoing socket
        /// </summary>
        /// <param name="socket">The socket to write to</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">Real time definition</param>
        /// <returns>An async task</returns>
        public async ValueTask WriteToSocket(ISocket socket, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // I would love to use stackalloc byte[9] here but I can't because this is async!
            using (PooledBuffer<byte> frameHeaderBuilder = BufferPool<byte>.Rent(9))
            {
                BinaryHelpers.UInt24ToByteArrayBigEndian((uint)PayloadLength, frameHeaderBuilder.Buffer, 0);
                frameHeaderBuilder.Buffer[3] = (byte)FrameType;
                frameHeaderBuilder.Buffer[4] = Flags;
                BinaryHelpers.Int32ToByteArrayBigEndian(StreamId, frameHeaderBuilder.Buffer, 5);
                await socket.WriteAsync(frameHeaderBuilder.Buffer, 0, 9, cancelToken, realTime).ConfigureAwait(false);
                if (PayloadLength > 0)
                {
                    await socket.WriteAsync(_framePayload.Buffer, 0, PayloadLength, cancelToken, realTime).ConfigureAwait(false);
                }
            }
        }

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
                _framePayload?.Dispose();
            }
        }

        /// <summary>
        /// Attempts to parse an HTTP2 frame from an incoming socket. This method will wait until
        /// socket data is available, or until the cancel token is signalled.
        /// Throws an Http2ProtocolException if the incoming frame is malformed.
        /// </summary>
        /// <param name="socket">The socket to read from</param>
        /// <param name="maxFrameSize">The maximum frame size this host is willing to receive</param>
        /// <param name="scratch">A scratch byte buffer containing at least 9 bytes</param>
        /// <param name="isServer">Whether this HTTP2 endpoint is acting as a server.</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The parsed HTTP2 frame.</returns>
        /// <exception cref="Http2ProtocolException"></exception>
        public static async ValueTask<Http2Frame> ReadFromSocket(
            ISocket socket,
            int maxFrameSize,
            byte[] scratch,
            bool isServer,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            // Read the frame header
            await socket.ReadAsync(scratch, 0, 9, cancelToken, realTime).ConfigureAwait(false);
            int payloadLength = (int)BinaryHelpers.ByteArrayToUInt24BigEndian(scratch, 0);
            Http2FrameType frameType = (Http2FrameType)scratch[3];
            byte flags = scratch[4];
            int streamId = BinaryHelpers.ByteArrayToInt32BigEndian(scratch, 5);

            if (payloadLength > maxFrameSize)
            {
                throw new Http2ProtocolException("HTTP2 frame exceeds maximum configured frame size");
            }

            PooledBuffer<byte> payload = null;
            if (payloadLength > 0)
            {
                payload = BufferPool<byte>.Rent(payloadLength);
                try
                {
                    await socket.ReadAsync(payload.Buffer, 0, payloadLength, cancelToken, realTime).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    payload.Dispose();
                    throw;
                }
            }

            switch(frameType)
            {
                case Http2FrameType.Continuation:
                    return Http2ContinuationFrame.CreateIncoming(payload, flags, streamId);
                case Http2FrameType.Data:
                    return Http2DataFrame.CreateIncoming(payload, flags, streamId);
                case Http2FrameType.GoAway:
                    return Http2GoAwayFrame.CreateIncoming(payload, flags, streamId);
                case Http2FrameType.Headers:
                    return Http2HeadersFrame.CreateIncoming(payload, flags, streamId);
                case Http2FrameType.Ping:
                    return Http2PingFrame.CreateIncoming(payload, flags, streamId);
                case Http2FrameType.Priority:
                    return Http2PriorityFrame.CreateIncoming(payload, flags, streamId);
                case Http2FrameType.PushPromise:
                    return Http2PushPromiseFrame.CreateIncoming(payload, flags, streamId);
                case Http2FrameType.RstStream:
                    return Http2RstStreamFrame.CreateIncoming(payload, flags, streamId);
                case Http2FrameType.Settings:
                    return Http2SettingsFrame.CreateIncoming(payload, flags, streamId, isServer);
                case Http2FrameType.WindowUpdate:
                    return Http2WindowUpdateFrame.CreateIncoming(payload, flags, streamId);
                default:
                    throw new Http2ProtocolException(string.Format("Received unknown HTTP2 frame Type 0x{0:X2} Flags {1:X2} Len {2}", scratch[3], flags, payloadLength));
            }
        }
    }
}
