using Durandal.Common.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Frames
{
    /// <summary>
    /// An HTTP2 Ping frame
    /// </summary>
    public class Http2PingFrame : Http2Frame
    {
        /// <inheritdoc />
        public override Http2FrameType FrameType => Http2FrameType.Ping;

        /// <summary>
        /// The opaque ping data
        /// </summary>
        public PooledBuffer<byte> PingData => _framePayload;

        /// <summary>
        /// Whether this frame is an acknowledgement of a previous ping
        /// </summary>
        public bool Ack { get; private set; }

        /// <summary>
        /// Creates an outgoing ping frame
        /// </summary>
        /// <param name="payload">The data to send - must be 8 bytes long. THIS CLASS TAKES OWNERSHIP OF THE DISPOSABLE BUFFER</param>
        /// <param name="ack">Whether this is an ack to a previous ping</param>
        /// <returns>A new frame</returns>
        public static Http2PingFrame CreateOutgoing(PooledBuffer<byte> payload, bool ack)
        {
            byte flags = (byte)(ack ? 0x1 : 0);
            return new Http2PingFrame(NetworkDirection.Outgoing, payload, flags, streamId: 0, ack);
        }

        /// <summary>
        /// Creates an incoming ping frame using data read from a socket
        /// </summary>
        /// <param name="payload">The incoming frame data</param>
        /// <param name="flags">The flag field</param>
        /// <param name="streamId">The stream ID (must be 0)</param>
        /// <returns>A parsed ping frame</returns>
        public static Http2PingFrame CreateIncoming(PooledBuffer<byte> payload, byte flags, int streamId)
        {
            bool ack = (flags & 0x1) != 0;
            return new Http2PingFrame(NetworkDirection.Incoming, payload, flags, streamId, ack);
        }

        private Http2PingFrame(NetworkDirection direction, PooledBuffer<byte> payload, byte flags, int streamId, bool ack)
            : base(direction, payload, flags, streamId)
        {
            if (streamId != 0)
            {
                throw new Http2ProtocolException("PING frame must be associated with stream 0");
            }
            //if ((flags & ~0x1) != 0)
            //{
            //    throw new Http2ProtocolException("PING frame contains unsupported flags");
            //}
            if (payload == null || payload.Length != 8)
            {
                throw new Http2ProtocolException("PING frame must have a payload length of 8");
            }

            Ack = ack;
        }
    }
}
