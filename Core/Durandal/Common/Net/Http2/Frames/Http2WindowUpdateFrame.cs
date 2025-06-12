using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Frames
{
    /// <summary>
    /// Represents an HTTP/2 WINDOW_UPDATE frame
    /// </summary>
    public class Http2WindowUpdateFrame : Http2Frame
    {
        /// <inheritdoc />
        public override Http2FrameType FrameType => Http2FrameType.WindowUpdate;

        /// <summary>
        /// The number of credits to apply towards the flow control window - must be positive integer
        /// </summary>
        public int WindowSizeIncrement { get; private set; }

        /// <summary>
        /// creates 
        /// </summary>
        /// <param name="streamId">The stream ID to apply this window update towards, or 0 for the connection in general</param>
        /// <param name="windowSizeIncrement">The number of credits (bytes) to increment the window by. Must be a positive integer.</param>
        /// <returns>A newly created window update frame</returns>
        public static Http2WindowUpdateFrame CreateOutgoing(int streamId, int windowSizeIncrement)
        {
            PooledBuffer<byte> payload = BufferPool<byte>.Rent(4);
            BinaryHelpers.Int32ToByteArrayBigEndian(windowSizeIncrement, payload.Buffer, 0);
            return new Http2WindowUpdateFrame(NetworkDirection.Outgoing, payload, flags: 0, streamId: streamId, windowSizeIncrement: windowSizeIncrement);
        }

        /// <summary>
        /// Creates a window update frame using data read from the socket
        /// </summary>
        /// <param name="payload">The frame data payload</param>
        /// <param name="flags">The flag bitset</param>
        /// <param name="streamId">The stream ID that this window update applies to</param>
        /// <returns>A newly parsed frame</returns>
        public static Http2WindowUpdateFrame CreateIncoming(PooledBuffer<byte> payload, byte flags, int streamId)
        {
            int windowSizeIncrement = BinaryHelpers.ByteArrayToInt32BigEndian(payload.Buffer, 0);
            return new Http2WindowUpdateFrame(NetworkDirection.Incoming, payload, flags, streamId, windowSizeIncrement);
        }

        private Http2WindowUpdateFrame(NetworkDirection direction, PooledBuffer<byte> payload, byte flags, int streamId, int windowSizeIncrement)
            : base(direction, payload, flags, streamId)
        {
            if (payload == null || payload.Length != 4)
            {
                throw new Http2ProtocolException("WINDOW_UPDATE frame must have a payload length of 4");
            }
            if (windowSizeIncrement <= 0)
            {
                throw new Http2ProtocolException("WINDOW_UPDATE frame must have a positive size increment");
            }

            WindowSizeIncrement = windowSizeIncrement;
        }
    }
}
