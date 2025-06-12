using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Frames
{
    /// <summary>
    /// The CONTINUATION frame (type=0x9) is used to continue a sequence of header block fragments.
    /// Any number of CONTINUATION frames can be sent, as long as the preceding frame is on the same stream and
    /// is a HEADERS, PUSH_PROMISE, or CONTINUATION frame without the END_HEADERS flag set.
    /// </summary>
    public class Http2ContinuationFrame : Http2Frame
    {
        /// <inheritdoc />
        public override Http2FrameType FrameType => Http2FrameType.Continuation;

        /// <summary>
        /// Flag which indicates whether this is the end of the headers block
        /// </summary>
        public bool EndHeaders { get; private set; }

        /// <summary>
        /// The compressed header data contained in this frame
        /// </summary>
        public ArraySegment<byte> HeaderData => new ArraySegment<byte>(_framePayload.Buffer, 0, PayloadLength);

        /// <summary>
        /// Constructs an outgoing continuation frame to be written to a socket
        /// </summary>
        /// <param name="compressedHeaders">The compressed header payload</param>
        /// <param name="streamId">The stream ID (must be nonzero)</param>
        /// <param name="endHeaders">Indicates whether this frame is last frame in a header block.</param>
        /// <returns>A newly created continuation frame</returns>
        public static Http2ContinuationFrame CreateOutgoing(PooledBuffer<byte> compressedHeaders, int streamId, bool endHeaders)
        {
            byte flags = 0;
            if (endHeaders)
            {
                flags |= 0x4;
            }

            return new Http2ContinuationFrame(NetworkDirection.Outgoing, compressedHeaders, flags, streamId, endHeaders);
        }

        /// <summary>
        /// Creates a continuation frame using data parsed from an incoming socket
        /// </summary>
        /// <param name="payload">The raw frame payload</param>
        /// <param name="flags">The parsed flags</param>
        /// <param name="streamId">The parsed stream ID</param>
        /// <returns>A newly created continuation frame</returns>
        public static Http2ContinuationFrame CreateIncoming(PooledBuffer<byte> payload, byte flags, int streamId)
        {
            bool endHeaders = (flags & 0x4) != 0;
            return new Http2ContinuationFrame(NetworkDirection.Incoming, payload, flags, streamId, endHeaders);
        }

        /// <summary>
        /// Private constructor for contination frame
        /// </summary>
        /// <param name="direction">The network direction of the frame relative to this peer</param>
        /// <param name="payload">The frame data, cannot be null</param>
        /// <param name="flags">The frame flags</param>
        /// <param name="streamId">The frame stream ID</param>
        /// <param name="endHeaders">the EndHeaders flag</param>
        private Http2ContinuationFrame(NetworkDirection direction, PooledBuffer<byte> payload, byte flags, int streamId, bool endHeaders)
            : base(direction, payload, flags, streamId)
        {
            if (streamId == 0)
            {
                throw new Http2ProtocolException("CONTINUATION frame cannot be associated with stream 0");
            }

            if (payload == null)
            {
                throw new Http2ProtocolException("CONTINUATION frame must have a non-null payload");
            }

            EndHeaders = endHeaders;
        }
    }
}
