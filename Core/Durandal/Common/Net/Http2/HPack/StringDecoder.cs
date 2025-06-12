using Durandal.Common.Collections;
using Durandal.Common.IO;
using System;
using System.Text;

namespace Durandal.Common.Net.Http2.HPack
{
    /// <summary>
    /// Decodes string values according to the HPACK specification.
    /// </summary>
    public class StringDecoder : IDisposable
    {
        private enum State: byte
        {
            StartDecode,
            DecodeLength,
            DecodeData,
        }

        /// <summary>The result of the decode operation</summary>
        public string Result;
        /// <summary>The length of the decoded string</summary>
        public int StringLength;
        /// <summary>
        /// Whether decoding was completed.
        /// This is set after a call to Decode().
        /// If a complete integer could be decoded from the input buffer
        /// the value is true. If a complete integer could not be decoded
        /// then more bytes are needed and decodeCont must be called until
        /// done is true before reading the result.
        /// </summary>
        public bool Done = true;

        /// <summary>Whether the input data is huffman encoded</summary>
        private bool _huffman;
        /// <summary>The state of the decoder</summary>
        private State _state = State.StartDecode;
        /// <summary>The number of octets in the string</summary>
        private int _octetLength;
        /// <summary>Buffer for the received bytes of the string</summary>
        private PooledBuffer<byte> _stringBuffer;
        /// <summary>The number of bytes that already have been read</summary>
        private int _bufferOffset;
        /// <summary>The maximum allowed byte length for strings</summary>
        private int _maxLength;
        /// <summary>Decoder for the string length</summary>
        private IntDecoder _lengthDecoder = new IntDecoder();


        public StringDecoder(int maxLength)
        {
            if (maxLength < 1) throw new ArgumentException(nameof(maxLength));
            _maxLength = maxLength;
        }

        public void Dispose()
        {
            if (_stringBuffer != null)
            {
                _stringBuffer.Dispose();
                _stringBuffer = null;
            }
        }

        public int Decode(ArraySegment<byte> buf)
        {
            var offset = buf.Offset;
            var length = buf.Count;

            // Check if there's a leftover string from the last decode process.
            // Should normally not happen.
            if (_stringBuffer != null)
            {
                _stringBuffer.Dispose();
                _stringBuffer = null;
            }

            var bt = buf.Array[offset];
            _huffman = (bt & 0x80) == 0x80;
            Done = false;
            _state = State.DecodeLength;
            var consumed = _lengthDecoder.Decode(7, buf);
            length -= consumed;
            offset += consumed;

            if (_lengthDecoder.Done)
            {
                var len = _lengthDecoder.Result;
                if (len > _maxLength)
                    throw new Exception("Maximum string length exceeded");
                _octetLength = len;
                _stringBuffer = BufferPool<byte>.Rent(_octetLength);
                _bufferOffset = 0;
                _state = State.DecodeData;
                consumed += DecodeCont(new ArraySegment<byte>(buf.Array, offset, length));
                return consumed;
            }
            else
            {
                // Need more input data to decode octetLength
                return consumed;
            }
        }

        private int DecodeContLength(ArraySegment<byte> buf)
        {
            var offset = buf.Offset;
            var length = buf.Count;

            var consumed = _lengthDecoder.DecodeCont(buf);
            length -= consumed;
            offset += consumed;

            if (_lengthDecoder.Done)
            {
                var len = _lengthDecoder.Result;
                if (len > _maxLength)
                    throw new Exception("Maximum string length exceeded");
                _octetLength = len;
                _stringBuffer = BufferPool<byte>.Rent(_octetLength);
                _bufferOffset = 0;
                _state = State.DecodeData;
            }
            // else need more data to decode octetLength

            return consumed;
        }

        private int DecodeContByteData(ArraySegment<byte> buf)
        {
            var offset = buf.Offset;
            var count = buf.Count;

            // Check how many bytes are available and how much we need
            var available = count;
            var need = _octetLength - _bufferOffset;

            var toCopy = available >= need ? need : available;
            if (toCopy > 0)
            {
                // Return is wrong because it doesn't handle 0byte strings
                // Copy that amount of data into our target buffer
                ArrayExtensions.MemCopy(buf.Array, offset, _stringBuffer.Buffer, _bufferOffset, toCopy);
                _bufferOffset += toCopy;
                // Adjust the offset of the input and output buffer
                offset += toCopy;
                count -= toCopy;
            }

            if (_bufferOffset == _octetLength)
            {
                // Copied everything
                var view = new ArraySegment<byte>(
                    _stringBuffer.Buffer, 0, _octetLength
                );
                if (_huffman)
                {
                    // We need to perform huffman decoding
                    Result = Huffman.Decode(view);
                }
                else
                {
                    // TODO: Check if encoding is really correct
                    Result =
                        Encoding.UTF8.GetString(view.Array, view.Offset, view.Count);
                }
                // TODO: Optionally check here for valid HTTP/2 header names
                Done = true;
                // The string length for the table is used without huffman encoding
                // TODO: This might by a different result than Encoding.ASCII.GetByteCount
                // Might be required to streamline that
                StringLength = Result.Length;
                _state = State.StartDecode;

                _stringBuffer.Dispose();
                _stringBuffer = null;
            }
            // Else we need more input data

            return offset - buf.Offset;
        }

        public int DecodeCont(ArraySegment<byte> buf)
        {
            var offset = buf.Offset;
            var count = buf.Count;

            if (_state == State.DecodeLength && count > 0)
            {
                // Continue to decode the data length
                var consumed = DecodeContLength(buf);
                offset += consumed;
                count -= consumed;
                // Decoding the length might have moved us into the DECODE_DATA state
            }

            if (_state == State.DecodeData)
            {
                // Continue to decode the data
                var consumed = DecodeContByteData(
                    new ArraySegment<byte>(buf.Array, offset, count));
                offset += consumed;
                count -= consumed;
            }

            return offset - buf.Offset;
        }
    }
}

// TODO: This will leak memory from the pool if a string could not be fully decoded
// However as that's not the normal situation it won't be too bad, as it will
// still get garbage collected.