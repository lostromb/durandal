using Durandal.Common.Collections;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;

namespace Durandal.Common.IO.Json
{
    /// <summary>
    /// Builds a string. Unlike <see cref="System.Text.StringBuilder"/> this class lets you reuse its internal buffer.
    /// </summary>
    internal struct LocalStringBuffer
    {
        private char[] _buffer;
        private int _position;

        public int Position
        {
            get => _position;
            set => _position = value;
        }

        public bool IsEmpty => _buffer == null;

        public LocalStringBuffer(IArrayPool<char> bufferPool, int initalSize) : this(new char[initalSize])
        {
        }

        private LocalStringBuffer(char[] buffer)
        {
            _buffer = buffer;
            _position = 0;
        }

        public void Append(IArrayPool<char> bufferPool, char value)
        {
            // test if the buffer array is large enough to take the value
            if (_position == _buffer.Length)
            {
                EnsureSize(bufferPool, 1);
            }

            // set value and increment poisition
            _buffer[_position++] = value;
        }

        public void Append(IArrayPool<char> bufferPool, char[] buffer, int startIndex, int count)
        {
            if (_position + count >= _buffer.Length)
            {
                EnsureSize(bufferPool, count);
            }

            Array.Copy(buffer, startIndex, _buffer, _position, count);

            _position += count;
        }

        public void Clear(IArrayPool<char> bufferPool)
        {
            if (_buffer != null)
            {
                _buffer = null;
            }
            _position = 0;
        }

        private void EnsureSize(IArrayPool<char> bufferPool, int appendLength)
        {
            char[] newBuffer = new char[(_position + appendLength) * 2];

            if (_buffer != null)
            {
                ArrayExtensions.MemCopy(_buffer, 0, newBuffer, 0, _position);
            }

            _buffer = newBuffer;
        }

        public override string ToString()
        {
            return ToString(0, _position);
        }

        public string ToString(int start, int length)
        {
            _buffer.AssertNonNull(nameof(_buffer));
            return new string(_buffer, start, length);
        }

        public char[] InternalBuffer => _buffer;
    }
}