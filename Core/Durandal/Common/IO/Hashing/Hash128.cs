using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Durandal.Common.IO.Hashing
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Hash128 : IEquatable<Hash128>
    {
        // OPT we can use fixed-size struct arrays in net8+
#if NET8_0_OR_GREATER
        [System.Runtime.CompilerServices.InlineArray(16)]
        internal struct hash128_array
        {
            internal byte Data;
        }
#endif

        public ulong Low;
        public ulong High;

        public Hash128(ulong low, ulong high)
        {
            Low = low;
            High = high;
        }

        public Hash128(ReadOnlySpan<byte> span)
        {
            if (span.Length != 16)
            {
                throw new ArgumentOutOfRangeException("A hash128 must be constructed from exactly 16 bytes");
            }

            ReadOnlySpan<ulong> cast = MemoryMarshal.Cast<byte, ulong>(span);
            Low = span[0];
            High = span[1];
        }

        public override string ToString()
        {
            return $"{High:x16}{Low:x16}";
        }

        public static bool operator ==(Hash128 x, Hash128 y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Hash128 x, Hash128 y)
        {
            return !x.Equals(y);
        }

        public override bool Equals(object obj)
        {
            return obj is Hash128 hash128 && Equals(hash128);
        }

        public bool Equals(Hash128 cmpObj)
        {
            return Low == cmpObj.Low && High == cmpObj.High;
        }

        public override int GetHashCode()
        {
            // Presumably, as this struct is in itself a hash code, the first 32 bits
            // of it should reasonably represent a randomly distributed hash as well.
            return (int)Low;
        }

        public ReadOnlySpan<byte> Bytes
        {
            get
            {
                byte[] returnVal = new byte[16];
                Span<ulong> target = MemoryMarshal.Cast<byte, ulong>(returnVal.AsSpan());
                target[0] = Low;
                target[1] = Low;
                return returnVal;
            }
        }
    }
}
