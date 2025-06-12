using Durandal.Common.Collections;
using Durandal.Common.Utils;
using Durandal.Internal.CoreOntology.SchemaDotOrg;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Durandal.Common.IO
{
    /// <summary>
    /// Wraps a binary stream representing UTF8 characters and exposes a Read method for reading chars directly from that stream.
    /// </summary>
    public class Utf8StreamReader : IDisposable
    {
        private readonly StreamReader _internal;
        private readonly Stream _innerStream;
        private readonly bool _leaveOpen;
        private readonly PooledBuffer<byte> _byteBuffer;
        private readonly PooledBuffer<char> _charBuffer;
        private readonly PooledStringBuilder _lineBuilder;
        private readonly Decoder _decoder;
        private int _bytesInByteBuffer = 0;
        private int _charsInCharBuffer = 0;

        public Utf8StreamReader(Stream stream)
            : this(stream, false)
        {
        }

        public Utf8StreamReader(Stream stream, bool leaveOpen)
        {
            _innerStream = stream.AssertNonNull(nameof(stream));
            _leaveOpen = leaveOpen;
            _byteBuffer = BufferPool<byte>.Rent();
            _charBuffer = BufferPool<char>.Rent(StringUtils.UTF8_WITHOUT_BOM.GetMaxCharCount(_byteBuffer.Buffer.Length));
            _lineBuilder = StringBuilderPool.Rent();
            _decoder = StringUtils.UTF8_WITHOUT_BOM.GetDecoder();
        }

        public Stream BaseStream => _innerStream;

        public bool EndOfStream { get; private set; }

        //public int Read()
        //{
        //    throw new NotImplementedException();
        //}

        public int Read(char[] buffer, int index, int count)
        {
            if (!TryFillInternalBuffer())
            {
                return 0;
            }

            int toReadFromBuffer = Math.Min(count, _charsInCharBuffer);
            _charBuffer.Buffer.AsSpan(0, toReadFromBuffer).CopyTo(buffer.AsSpan(index, toReadFromBuffer));
            if (toReadFromBuffer == _charsInCharBuffer)
            {
                _charsInCharBuffer = 0;
            }
            else
            {
                _charsInCharBuffer -= toReadFromBuffer;
                ArrayExtensions.MemMove(_charBuffer.Buffer, toReadFromBuffer, 0, _charsInCharBuffer);
            }

            return toReadFromBuffer;
        }

        private bool TryFillInternalBuffer()
        {
            // See if we need to refill the char buffer
            if (_charsInCharBuffer == 0)
            {
                // Read from source stream, try and fill internal buffer
                int bytesRead = _innerStream.Read(_byteBuffer.Buffer, _bytesInByteBuffer, _byteBuffer.Buffer.Length - _bytesInByteBuffer);
                if (bytesRead == 0 && _bytesInByteBuffer == 0)
                {
                    EndOfStream = true;
                    return false;
                }

                _bytesInByteBuffer += bytesRead;
                int bytesUsed;
                int charsProduced;
                bool finished;

                // Decode UTF8
                _decoder.Convert(_byteBuffer.Buffer, 0, _bytesInByteBuffer, _charBuffer.Buffer, 0, _charBuffer.Buffer.Length, bytesRead == 0, out bytesUsed, out charsProduced, out finished);
                if (bytesUsed == _bytesInByteBuffer)
                {
                    _bytesInByteBuffer = 0;
                }
                else
                {
                    _bytesInByteBuffer -= bytesUsed;
                    ArrayExtensions.MemMove(_byteBuffer.Buffer, bytesUsed, 0, _bytesInByteBuffer);
                }

                _charsInCharBuffer += charsProduced;
            }

            return true;
        }

        public void Dispose()
        {
            _byteBuffer.Dispose();
            _charBuffer.Dispose();
            _lineBuilder.Dispose();

            if (!_leaveOpen)
            {
                _innerStream.Dispose();
            }
        }
    }
}
