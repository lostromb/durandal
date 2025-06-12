using Durandal.Common.IO;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Durandal.Common.Net.Http2.Frames
{
    /// <summary>
    /// DATA frames (type=0x0) convey arbitrary, variable-length sequences of octets associated with a stream.
    /// One or more DATA frames are used, for instance, to carry HTTP request or response payloads.
    /// DATA frames MAY also contain padding.Padding can be added to DATA frames to obscure the size of messages.
    /// </summary>
    public class Http2DataFrame : Http2Frame
    {
        private readonly bool _isPadded;
        private readonly byte _paddingLength;

        /// <inheritdoc />
        public override Http2FrameType FrameType => Http2FrameType.Data;

        /// <summary>
        /// Indicates whether this frame is the final frame of its associated stream
        /// </summary>
        public bool EndStream { get; private set; }

        /// <summary>
        /// Gets the frame payload data as an array segment.
        /// </summary>
        public ArraySegment<byte> PayloadData
        {
            get
            {
                if (_framePayload == null)
                {
                    return new ArraySegment<byte>(BinaryHelpers.EMPTY_BYTE_ARRAY);
                }
                else if (!_isPadded)
                {
                    return new ArraySegment<byte>(_framePayload.Buffer, 0, _framePayload.Length);
                }
                else
                {
                    return new ArraySegment<byte>(_framePayload.Buffer, 1, _framePayload.Length - _paddingLength - 1 );
                }
            }
        }

        public string _debugContentString
        {
            get
            {
                if (_framePayload == null)
                {
                    return string.Empty;
                }
                else if (!_isPadded)
                {
                    return StringUtils.ASCII_ENCODING.GetString(_framePayload.Buffer, 0, _framePayload.Length);
                }
                else
                {
                    return StringUtils.ASCII_ENCODING.GetString(_framePayload.Buffer, 1, _framePayload.Length - _paddingLength - 1);
                }
            }
        }

        /// <summary>
        /// Creates an outgoing data frame
        /// </summary>
        /// <param name="payload">The data to send (non-null)</param>
        /// <param name="streamId">The stream ID for this data</param>
        /// <param name="endStream">Indicates whether this frame is the end of its associated stream</param>
        /// <returns>The newly created data frame</returns>
        public static Http2DataFrame CreateOutgoing(PooledBuffer<byte> payload, int streamId, bool endStream = false)
        {
            byte flags = (byte)(endStream ? 0x1 : 0);
            return new Http2DataFrame(NetworkDirection.Outgoing, payload, flags: flags, streamId: streamId, endStream, isPadded: false);
        }

        /// <summary>
        /// Creates an incoming data frame based on values parsed from a socket
        /// </summary>
        /// <param name="payload">The incoming data payload</param>
        /// <param name="flags">The flags field</param>
        /// <param name="streamId">The stream ID</param>
        /// <returns>A parsed data frame</returns>
        public static Http2DataFrame CreateIncoming(PooledBuffer<byte> payload, byte flags, int streamId)
        {
            bool endStream = (flags & 0x1) != 0;
            bool isPadded = (flags & 0x8) != 0;
            return new Http2DataFrame(NetworkDirection.Incoming, payload, flags, streamId, endStream, isPadded);
        }

        /// <summary>
        /// Private constructor for a DATA frame.
        /// </summary>
        /// <param name="direction">The network direction relative to this peer</param>
        /// <param name="payload">The frame payload (non-null)</param>
        /// <param name="flags">The flag bitfield</param>
        /// <param name="streamId">The stream ID (non-zero)</param>
        /// <param name="endStream">end of stream flag</param>
        /// <param name="isPadded">whether this frame is padded</param>
        /// <exception cref="Http2ProtocolException"></exception>
        private Http2DataFrame(NetworkDirection direction, PooledBuffer<byte> payload, byte flags, int streamId, bool endStream, bool isPadded)
            : base(direction, payload, flags, streamId)
        {
            if (streamId == 0)
            {
                throw new Http2ProtocolException("DATA frames may not be sent on stream 0");
            }

            EndStream = endStream;
            _isPadded = isPadded;
            if (_isPadded)
            {
                _paddingLength = _framePayload.Buffer[0];
            }
        }
    }
}
