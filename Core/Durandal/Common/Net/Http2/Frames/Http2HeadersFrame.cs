using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Net.Http;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Frames
{
    /// <summary>
    /// HTTP/2 frame for sending headers or trailers
    /// </summary>
    public class Http2HeadersFrame : Http2Frame
    {
        private readonly int _headersStartIdx;
        private readonly int _headersLength;

        /// <inheritdoc />
        public override Http2FrameType FrameType => Http2FrameType.Headers;

        /// <summary>
        /// See the equivalent flag on Priority frame
        /// The exclusive flag causes the stream to become the sole dependency of its parent stream,
        /// causing other dependencies to become dependent on the exclusive stream.
        /// </summary>
        public bool? IsExclusive { get; private set; }

        /// <summary>
        /// A stream ID for the stream that this stream depends on (see Priority frame)
        /// </summary>
        public int? StreamDependency { get; private set; }

        /// <summary>
        /// The priority weight for this stream (see Priority frame)
        /// </summary>
        public byte? Weight { get; private set; }

        /// <summary>
        /// Indicates that this frame is the final frame of the stream. It is still possible
        /// for CONTINUATION frames to follow this, as they are logically part of this same
        /// headers frame.
        /// </summary>
        public bool EndStream { get; private set; }

        /// <summary>
        /// Indicates that this is the final frame of the header block.
        /// </summary>
        public bool EndHeaders { get; private set; }

        /// <summary>
        /// The array segment for the actual header data
        /// </summary>
        public ArraySegment<byte> HeaderData => new ArraySegment<byte>(_framePayload.Buffer, _headersStartIdx, _headersLength);

        /// <summary>
        /// Creates an outgoing Headers frame with no special properties.
        /// </summary>
        /// <param name="compressedHeaders">The header data to send</param>
        /// <param name="streamId">The stream ID</param>
        /// <param name="endHeaders">Whether this frame is the end of the headers block</param>
        /// <param name="endStream">Whether this header block is the end of its stream</param>
        /// <returns>A newly created headers frame</returns>
        public static Http2HeadersFrame CreateOutgoing(PooledBuffer<byte> compressedHeaders, int streamId, bool endHeaders, bool endStream)
        {
            byte flags = 0;
            if (endStream)
            {
                flags |= 0x1;
            }
            if (endHeaders)
            {
                flags |= 0x4;
            }

            return new Http2HeadersFrame(
                NetworkDirection.Outgoing,
                compressedHeaders,
                flags,
                streamId,
                headersStartIdx: 0, // no padding
                headersLength: compressedHeaders.Length,
                endStream: endStream,
                endHeaders: endHeaders,
                isExclusive: null,
                streamDependency: null,
                weight: null);
        }

        /// <summary>
        /// Creates an outgoing Headers frame with random padding.
        /// </summary>
        /// <param name="compressedHeaders">The header data to send</param>
        /// <param name="streamId">The stream ID</param>
        /// <param name="endHeaders">Whether this frame is the end of the headers block</param>
        /// <param name="endStream">Whether this header block is the end of its stream</param>
        /// <param name="rand">A random number generator</param>
        /// <returns>A newly created headers frame</returns>
        public static Http2HeadersFrame CreateOutgoingPadded(PooledBuffer<byte> compressedHeaders, int streamId, bool endHeaders, bool endStream, IRandom rand)
        {
            byte flags = 0x8; // padded flag
            if (endStream)
            {
                flags |= 0x1;
            }
            if (endHeaders)
            {
                flags |= 0x4;
            }

            byte paddingLength = (byte)rand.NextInt(0, 256);
            int compressedHeadersLength = compressedHeaders.Length;
            PooledBuffer<byte> paddedPayload = BufferPool<byte>.Rent(compressedHeadersLength + paddingLength + 1);
            paddedPayload.Buffer[0] = paddingLength;
            ArrayExtensions.MemCopy(compressedHeaders.Buffer, 0, paddedPayload.Buffer, 1, compressedHeadersLength);
            rand.NextBytes(paddedPayload.Buffer, 1 + compressedHeadersLength, paddingLength);
            compressedHeaders.Dispose();
            compressedHeaders = null;

            return new Http2HeadersFrame(
                NetworkDirection.Outgoing,
                paddedPayload,
                flags,
                streamId,
                headersStartIdx: 1,
                headersLength: compressedHeadersLength,
                endStream: endStream,
                endHeaders: endHeaders,
                isExclusive: null,
                streamDependency: null,
                weight: null);
        }

        /// <summary>
        /// Creates a new Headers frame using data read from a socket.
        /// </summary>
        /// <param name="payload">The frame data read from the wire</param>
        /// <param name="flags">The flag bits</param>
        /// <param name="streamId">The incoming stream id</param>
        /// <returns>A parsed headers frame</returns>
        /// <exception cref="Http2ProtocolException"></exception>
        public static Http2HeadersFrame CreateIncoming(PooledBuffer<byte> payload, byte flags, int streamId)
        {
            int headersStartIdx = 0;
            int headersLength = payload.Length;
            bool isPadded = (flags & 0x8) != 0;
            byte paddingLength = 0;
            if (isPadded)
            {
                paddingLength = payload.Buffer[0];
                headersStartIdx += 1;
                headersLength -= 1 + paddingLength;
            }

            bool endStream = (flags & 0x1) != 0;
            bool endHeaders = (flags & 0x4) != 0;
            bool? isExclusive = null;
            int? streamDependency = null;
            byte? weight = null;

            if ((flags & 0x20) != 0) // PRIORITY flag
            {
                uint streamDependencyField = BinaryHelpers.ByteArrayToUInt32BigEndian(payload.Buffer, isPadded ? 1 : 0);
                streamDependency = (int)streamDependencyField & 0x7FFFFFFF;
                isExclusive = (streamDependencyField & 0x80000000) != 0;
                weight = payload.Buffer[isPadded ? 5 : 4];
                headersStartIdx += 5;
                headersLength -= 5;
            }

            // validate the length of the frame to make sure padding adds up and everything
            if (headersStartIdx + headersLength + paddingLength != payload.Length)
            {
                throw new Http2ProtocolException("Bad format headers frame");
            }

            return new Http2HeadersFrame(
                NetworkDirection.Incoming,
                payload,
                flags,
                streamId,
                headersStartIdx: headersStartIdx,
                headersLength: headersLength,
                endStream: endStream,
                endHeaders: endHeaders,
                isExclusive: isExclusive,
                streamDependency: streamDependency,
                weight: weight);
        }

        /// <summary>
        /// internal constructor for the headers frame.
        /// Padding length is not explicitly set, we just care about the bounds of the header payload
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="payload"></param>
        /// <param name="flags"></param>
        /// <param name="streamId"></param>
        /// <param name="endStream"></param>
        /// <param name="endHeaders"></param>
        /// <param name="headersStartIdx"></param>
        /// <param name="headersLength"></param>
        /// <param name="isExclusive"></param>
        /// <param name="streamDependency"></param>
        /// <param name="weight"></param>
        private Http2HeadersFrame(
            NetworkDirection direction,
            PooledBuffer<byte> payload,
            byte flags,
            int streamId,
            bool endStream,
            bool endHeaders,
            int headersStartIdx,
            int headersLength,
            bool? isExclusive,
            int? streamDependency,
            byte? weight)
            : base(direction, payload, flags, streamId)
        {
            _headersStartIdx = headersStartIdx;
            _headersLength = headersLength;
            EndStream = endStream;
            EndHeaders = endHeaders;
            IsExclusive = isExclusive;
            StreamDependency = streamDependency;
            Weight = weight;
        }
    }
}
