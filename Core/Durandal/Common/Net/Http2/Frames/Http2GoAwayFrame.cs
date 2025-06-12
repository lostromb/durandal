using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Frames
{
    /// <summary>
    /// The GOAWAY frame (type=0x7) is used to initiate shutdown of a connection or to signal serious error conditions.
    /// GOAWAY allows an endpoint to gracefully stop accepting new streams while still finishing processing of
    /// previously established streams. This enables administrative actions, like server maintenance.
    /// </summary>
    public class Http2GoAwayFrame : Http2Frame
    {
        /// <inheritdoc />
        public override Http2FrameType FrameType => Http2FrameType.GoAway;

        /// <summary>
        /// The last stream ID that was processed by this peer.
        /// "Processed" means its data was sent upwards to another layer of the network stack
        /// </summary>
        public int LastStreamId { get; private set; }

        /// <summary>
        /// The reason code for this message, or NoError to just mean a graceful shutdown
        /// </summary>
        public Http2ErrorCode Error { get; private set; }

        /// <summary>
        /// Any additional data sent by the remote peer, such as an error code. Its format is not specified.
        /// </summary>
        public ArraySegment<byte> DebugData => new ArraySegment<byte>(_framePayload.Buffer, 8, PayloadLength - 8);

        /// <summary>
        /// Additional debug data sent by the peer, interpreted as an ASCII string.
        /// </summary>
        public string DebugString => PayloadLength == 8 ? string.Empty : StringUtils.ASCII_ENCODING.GetString(_framePayload.Buffer, 8, PayloadLength - 8);

        /// <summary>
        /// Creates an outgoing GoAway frame with the specified last stream ID and reason code
        /// </summary>
        /// <param name="lastStreamId">The last remote-initiated stream ID that was processed by this peer</param>
        /// <param name="error">The reason or error code for this message, or just NoError by default.</param>
        /// <param name="errorMessage">An optional error message to send</param>
        /// <returns>A newly created frame.</returns>
        /// <exception cref="Http2ProtocolException"></exception>
        public static Http2GoAwayFrame CreateOutgoing(int lastStreamId, Http2ErrorCode error = Http2ErrorCode.NoError, string errorMessage = "")
        {
            errorMessage = errorMessage ?? string.Empty;
            int extraStringDataLength = StringUtils.ASCII_ENCODING.GetByteCount(errorMessage);
            PooledBuffer<byte> buffer = BufferPool<byte>.Rent(8 + extraStringDataLength);
            BinaryHelpers.UInt32ToByteArrayBigEndian((uint)lastStreamId, buffer.Buffer, 0);
            BinaryHelpers.UInt32ToByteArrayBigEndian((uint)error, buffer.Buffer, 4);
            if (extraStringDataLength > 0)
            {
                StringUtils.ASCII_ENCODING.GetBytes(errorMessage, 0, errorMessage.Length, buffer.Buffer, 8);
            }

            return new Http2GoAwayFrame(NetworkDirection.Outgoing, buffer, flags: 0, streamId: 0, lastStreamId: lastStreamId, error: error);
        }

        /// <summary>
        /// Creates an incoming GOAWAY frame using data from an incoming socket
        /// </summary>
        /// <param name="payload">The data payload read from the socket</param>
        /// <param name="flags">The flag bitfield (not used)</param>
        /// <param name="streamId">The incoming stream ID (0 is the only valid option)</param>
        /// <returns>A parsed frame</returns>
        /// <exception cref="Http2ProtocolException"></exception>
        public static Http2GoAwayFrame CreateIncoming(PooledBuffer<byte> payload, byte flags, int streamId)
        {
            int lastStreamId = (int)BinaryHelpers.ByteArrayToUInt32BigEndian(payload.Buffer, 0) & 0x7FFFFFFF;
            Http2ErrorCode error = (Http2ErrorCode)BinaryHelpers.ByteArrayToUInt32BigEndian(payload.Buffer, 4);
            return new Http2GoAwayFrame(NetworkDirection.Incoming, payload, flags, streamId, lastStreamId, error);
        }

        /// <summary>
        /// Internal constructor
        /// </summary>
        /// <param name="direction">The network direction</param>
        /// <param name="payload">Data payload, length must be at least 8</param>
        /// <param name="flags">The flag bitfield</param>
        /// <param name="streamId">The stream ID (must be 0)</param>
        /// <param name="lastStreamId">The ID of the last processed stream by this peer</param>
        /// <param name="error">A reason or error code</param>
        /// <exception cref="Http2ProtocolException"></exception>
        private Http2GoAwayFrame(NetworkDirection direction, PooledBuffer<byte> payload, byte flags, int streamId, int lastStreamId, Http2ErrorCode error)
            : base(direction, payload, flags: flags, streamId: streamId)
        {
            if (streamId != 0)
            {
                throw new Http2ProtocolException("GOAWAY frame must be associated with stream 0");
            }

            if (PayloadLength < 8)
            {
                throw new Http2ProtocolException("GOAWAY frame must be at least 8 bytes long");
            }

            LastStreamId = lastStreamId;
            Error = error;
        }
    }
}
