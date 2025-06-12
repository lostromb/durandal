using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Frames
{
    /// <summary>
    /// Represents an HTTP/2 PUSH_PROMISE frame
    /// </summary>
    public class Http2PushPromiseFrame : Http2Frame
    {
        private readonly bool _isPadded;
        private readonly byte _paddingLength;

        /// <inheritdoc />
        public override Http2FrameType FrameType => Http2FrameType.PushPromise;

        /// <summary>
        /// The stream ID that the sender of this frame reserves for transmitting data frames associated with this response
        /// </summary>
        public int PromisedStreamId { get; private set; }

        /// <summary>
        /// Whether this frame contains a complete set of headers, or if there are continuation frames to follow
        /// </summary>
        public bool EndHeaders { get; private set; }

        /// <summary>
        /// Compressed header data. This does NOT represent the response headers for the promised response. Rather, it is
        /// the set of "expected" request headers that we expect the client to send to match this request.
        /// Typically, these headers only include :method, :path, :authority, and :scheme.
        /// </summary>
        public ArraySegment<byte> HeaderData => new ArraySegment<byte>(
            _framePayload.Buffer,
            _isPadded ? 5 : 4,
            _isPadded ? PayloadLength - _paddingLength - 5 : PayloadLength - 4);

        /// <summary>
        /// Creates an outgoing push promise frame
        /// </summary>
        /// <param name="requestHeaderData">Compressed header data for the expected request headers</param>
        /// <param name="streamId">
        /// The stream ID of the response that this promise is initially associated with.
        /// That is, the stream ID of the resource which the client explicitly requested that triggered this promise.
        /// </param>
        /// <param name="promisedStreamId">The stream ID to reserve for sending the pushed response data</param>
        /// <param name="endHeaders">Whether this frame contains a complete set of request headers</param>
        /// <returns>A newly created push promise frame</returns>
        public static Http2PushPromiseFrame CreateOutgoing(
            PooledBuffer<byte> requestHeaderData,
            int streamId,
            int promisedStreamId,
            bool endHeaders)
        {
            PooledBuffer<byte> newBuffer = BufferPool<byte>.Rent(requestHeaderData.Length + 4);
            BinaryHelpers.Int32ToByteArrayBigEndian(promisedStreamId, newBuffer.Buffer, 0);
            ArrayExtensions.MemCopy(requestHeaderData.Buffer, 0, newBuffer.Buffer, 4, requestHeaderData.Length);
            requestHeaderData.Dispose();
            requestHeaderData = null;
            byte flags = endHeaders ? (byte)0x4 : (byte)0x0; // we don't support padding on outgoing frames yet
            return new Http2PushPromiseFrame(
                NetworkDirection.Outgoing,
                newBuffer,
                flags: flags,
                streamId: streamId,
                promisedStreamId: promisedStreamId,
                endHeaders: endHeaders,
                isPadded: false,
                paddingLength: 0);
        }

        /// <summary>
        /// Constructs a push promise frame based on data read from the wire.
        /// </summary>
        /// <param name="payload">The incoming frame data</param>
        /// <param name="flags">The flag bitset</param>
        /// <param name="streamId">The associated stream id</param>
        /// <returns>A parsed push promise frame</returns>
        public static Http2PushPromiseFrame CreateIncoming(PooledBuffer<byte> payload, byte flags, int streamId)
        {
            bool isPadded = (flags & 0x8) != 0;
            byte paddingLength = 0;
            if (isPadded)
            {
                paddingLength = payload.Buffer[0];
            }

            int promisedStreamId = (int)BinaryHelpers.ByteArrayToUInt32BigEndian(payload.Buffer, isPadded ? 1 : 0) & 0x7FFFFFFF;
            bool endHeaders = (flags & 0x4) != 0;
            return new Http2PushPromiseFrame(NetworkDirection.Incoming, payload, flags, streamId, promisedStreamId, endHeaders, isPadded, paddingLength);
        }

        private Http2PushPromiseFrame(
            NetworkDirection direction,
            PooledBuffer<byte> payload,
            byte flags,
            int streamId,
            int promisedStreamId,
            bool endHeaders,
            bool isPadded,
            byte paddingLength)
            : base(direction, payload, flags, streamId)
        {
            if (promisedStreamId <= 0)
            {
                throw new Http2ProtocolException("PUSH_PROMISE frame stream ID must be a positive integer");
            }
            if ((promisedStreamId % 2) != 0)
            {
                throw new Http2ProtocolException("PUSH_PROMISE frame stream ID must be an even integer");
            }
            if (promisedStreamId <= 0)
            {
                throw new Http2ProtocolException("PUSH_PROMISE frame does not have a valid promised stream ID");
            }

            PromisedStreamId = promisedStreamId;
            EndHeaders = endHeaders;
            _isPadded = isPadded;
            _paddingLength = paddingLength;
        }
    }
}
