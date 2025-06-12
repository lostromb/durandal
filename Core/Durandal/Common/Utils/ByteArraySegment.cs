using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// Functionally the same as an array segment of bytes, but it uses the actual underlying
    /// byte data for Equals() and GetHashCode(), which means that comparisons among equal
    /// segments of byte data will work as expected.
    /// </summary>
    public struct ByteArraySegment
    {
        private readonly byte[] _array;
        private readonly int _offset;
        private readonly int _count;

        public ByteArraySegment(byte[] array, int offset, int count)
        {
            if (offset < 0 || offset >= array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (count < 0 || count + offset > array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _array = array.AssertNonNull(nameof(array));
            _offset = offset;
            _count = count;
        }

        public byte[] Array => _array;

        public int Offset => _offset;

        public int Count => _count;

        public override int GetHashCode()
        {
            ulong returnVal = 0;

            unchecked
            {
                // stolen from the twister code in FastRandom
                for (int c = _offset; c < _offset + _count; c++)
                {
                    returnVal = unchecked((returnVal ^ _array[c]) * 0x5DEECE66DL + 0xBL) >> 16;
                    returnVal = unchecked(0x4182BED5 * returnVal);
                }
            }

            return (int)(returnVal & 0xFFFFFFFF);
        }

        public override bool Equals(object obj)
        {
            if (obj is ArraySegment<byte>)
            {
                throw new InvalidCastException("Cannot compare ByteArraySegment with ArraySegment<byte> as this is highly prone to errors");
            }

            if (!(obj is ByteArraySegment))
            {
                return false;
            }

            ByteArraySegment other = (ByteArraySegment)obj;

            if (_count != other._count)
            {
                return false;
            }

            int localIter = _offset;
            int remoteIter = other._offset;
            for (int c = 0; c < _count; c++)
            {
                if (_array[localIter++] != other._array[remoteIter++])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
