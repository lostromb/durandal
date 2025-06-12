using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Frames
{
    /// <summary>
    /// Represents an HTTP/2 PRIORITY frame
    /// </summary>
    public class Http2PriorityFrame : Http2Frame
    {
        /// <inheritdoc />
        public override Http2FrameType FrameType => Http2FrameType.Priority;

        /// <summary>
        /// An exclusive flag allows for the insertion of a new level of dependencies.
        /// The exclusive flag causes the stream to become the sole dependency of its
        /// parent stream, causing other dependencies to become dependent on the exclusive stream.
        /// </summary>
        public bool IsExclusive { get; private set; }

        /// <summary>
        /// The ID of the stream that this is dependent on.
        /// </summary>
        public int StreamDependency { get; private set; }

        /// <summary>
        /// The prioritization weight of this stream. Higher values mean higher priority
        /// </summary>
        public byte Weight { get; private set; }

        /// <summary>
        /// Creates an outgoing Priority frame.
        /// </summary>
        /// <param name="streamId">The stream ID that this frame describes</param>
        /// <param name="streamDependency">The ID of the stream that this depends on</param>
        /// <param name="isExclusive">Whether to create an exclusive dependency</param>
        /// <param name="weight">The prioritization weight</param>
        /// <returns>A newly created priority frame</returns>
        public static Http2PriorityFrame CreateOutgoing(int streamId, int streamDependency, bool isExclusive, byte weight)
        {
            PooledBuffer<byte> payload = BufferPool<byte>.Rent(5);
            uint firstWord = (uint)streamDependency;
            if (isExclusive)
            {
                firstWord |= 0x80000000;
            }

            BinaryHelpers.UInt32ToByteArrayBigEndian(firstWord, payload.Buffer, 0);
            payload.Buffer[4] = weight;
            return new Http2PriorityFrame(NetworkDirection.Outgoing, payload: payload, flags: 0, streamId: streamId, streamDependency, isExclusive, weight);
        }

        /// <summary>
        /// Creates a priority frame using data read from a socket
        /// </summary>
        /// <param name="payload">The frame payload</param>
        /// <param name="flags">The flag bitfield</param>
        /// <param name="streamId">The stream ID that this frame applies to</param>
        /// <returns>A parsed frame</returns>
        public static Http2PriorityFrame CreateIncoming(PooledBuffer<byte> payload, byte flags, int streamId)
        {
            uint firstWord = BinaryHelpers.ByteArrayToUInt32BigEndian(payload.Buffer, 0);
            int streamDependency = (int)firstWord & 0x7FFFFFFF;
            bool isExclusive = (firstWord & 0x80000000) != 0;
            byte weight = payload.Buffer[4];
            return new Http2PriorityFrame(NetworkDirection.Incoming, payload, flags, streamId, streamDependency, isExclusive, weight);
        }

        private Http2PriorityFrame(
            NetworkDirection direction,
            PooledBuffer<byte> payload,
            byte flags,
            int streamId,
            int streamDependency,
            bool isExclusive,
            byte weight)
            : base(direction, payload, flags, streamId)
        {
            if (payload.Length != 5)
            {
                throw new Http2ProtocolException("PRIORITY frame must have a data length of 5");
            }

            StreamDependency = streamDependency;
            IsExclusive = isExclusive;
            Weight = weight;
        }
    }
}
