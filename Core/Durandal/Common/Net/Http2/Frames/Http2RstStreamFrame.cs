using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Frames
{
    /// <summary>
    /// Defines an HTTP/2 RST_STREAM frame
    /// </summary>
    public class Http2RstStreamFrame : Http2Frame
    {
        /// <inheritdoc />
        public override Http2FrameType FrameType => Http2FrameType.RstStream;

        /// <summary>
        /// The reason code or error that caused this stream to be reset
        /// </summary>
        public Http2ErrorCode Error { get; private set; }

        /// <summary>
        /// Creates an outgoing ResetStream frame
        /// </summary>
        /// <param name="streamId">The stream ID to reset</param>
        /// <param name="error">The error or reason code for the reset</param>
        /// <returns>A newly created frame</returns>
        public static Http2RstStreamFrame CreateOutgoing(int streamId, Http2ErrorCode error = Http2ErrorCode.NoError)
        {
            PooledBuffer<byte> buffer = BufferPool<byte>.Rent(4);
            BinaryHelpers.UInt32ToByteArrayBigEndian((uint)error, buffer.Buffer, 0);
            return new Http2RstStreamFrame(NetworkDirection.Outgoing, payload: buffer, flags: 0, streamId: streamId);
        }

        /// <summary>
        /// Creates a RstStream frame using data read from a socket
        /// </summary>
        /// <param name="payload">The raw frame data</param>
        /// <param name="flags">The flag bitfield</param>
        /// <param name="streamId">The stream ID being reset</param>
        /// <returns>A parsed frame</returns>
        public static Http2RstStreamFrame CreateIncoming(PooledBuffer<byte> payload, byte flags, int streamId)
        {
            return new Http2RstStreamFrame(NetworkDirection.Incoming, payload, flags, streamId);
        }

        private Http2RstStreamFrame(NetworkDirection direction, PooledBuffer<byte> payload, byte flags, int streamId)
            : base(direction, payload, flags, streamId)
        {
            if (payload.Length != 4)
            {
                throw new Http2ProtocolException("RST_STREAM frame must have length of exactly 4");
            }

            Error = (Http2ErrorCode)BinaryHelpers.ByteArrayToUInt32BigEndian(_framePayload.Buffer, 0);
        }
    }
}
