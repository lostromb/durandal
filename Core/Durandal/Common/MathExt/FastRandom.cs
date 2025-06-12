using Durandal.Common.IO;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// Pseudorandom number generator that is usually faster than the built-in C# class
    /// </summary>
    public class FastRandom : IRandom
    {
        // FIXME there are definitely endianness issues with this code, where big-endian output
        // won't have parity with little-endian if determinism is important across architectures.

        private const double DOUBLE_CAP = ((double)int.MaxValue) + 1;
        private const float FLOAT_CAP = ((float)int.MaxValue) + 1;

        // The shared entropy table will contain this many bytes of data
        // This size should be divisible by all common SIMD vector lengths
        // (which, if it's a power of two, should be almost guaranteed)
        private const int SHARED_ENTROPY_SIZE_BYTES = 512;

        // when generating more than this # of bytes at a time, SIMD performs better
        private const int SIMD_PIVOT_ARRAY = 100;

        /// <summary>
        /// A precalculated, readonly random bit field used for random byte generation
        /// </summary>
        private static readonly byte[] SharedEntropy;

        /// <summary>
        /// The current random bitfield.
        /// The order of the bytes depends on endianness
        /// </summary>
        private ulong _val;

        private int _bytesGenIndex = 0;

        private byte[] _vectorScratch;

        /// <summary>
        /// Default singleton instance of FastRandom based on system clock seed.
        /// </summary>
        public static FastRandom Shared { get; private set; }

        static FastRandom()
        {
            // Initialize vector bitfield that is shared by all instances of this random generator.
            // The field will use the same seed, but each random instance will still generate unique outputs
            // because they will prime the generation with their own initial seed before the XOR series.
            int sharedEntropySize = SHARED_ENTROPY_SIZE_BYTES;
            if (Vector.IsHardwareAccelerated)
            {
                sharedEntropySize = Math.Max(Vector<byte>.Count, SHARED_ENTROPY_SIZE_BYTES);
            }

            SharedEntropy = new byte[sharedEntropySize];
            ulong bitfield = 3266489917U;
            for (int c = 0; c < SharedEntropy.Length; c++)
            {
                unchecked
                {
                    bitfield = unchecked(bitfield * 0x5DEECE66DL + 0xBL) >> 16;
                    bitfield = unchecked(0x4182BED5 * bitfield);
                    SharedEntropy[c] = (byte)(bitfield & 0xFF);
                }
            }

            Shared = new FastRandom();
        }

        public FastRandom() : this((ulong)HighPrecisionTimer.GetCurrentTicks())
        {
        }

        public FastRandom(int seed) : this((ulong)seed)
        {
        }

        public FastRandom(ulong seed)
        {
            if (Vector.IsHardwareAccelerated)
            {
                _vectorScratch = new byte[SharedEntropy.Length];
            }

            SeedRand(seed);
        }

        public void SeedRand(int seed)
        {
            if (seed == 0) // don't let value collapse to zero
            {
                seed = -1;
            }

            ulong wideSeed = (ulong)(((long)seed) | ((long)seed << 32));
            _val = wideSeed;
            NextBitfield();
        }

        public void SeedRand(ulong seed)
        {
            if (seed == 0) // don't let value collapse to zero
            {
                seed = 0xFFFFFFFF_FFFFFFFFU;
            }

            _val = seed;
            NextBitfield();
        }

        /// <inheritdoc />
        public int NextInt()
        {
            return (int)(NextBitfield() & 0x7FFFFFFF);
        }

        /// <inheritdoc />
        public int NextInt(int maxValue)
        {
            return NextInt(0, maxValue);
        }

        /// <inheritdoc />
        public int NextInt(int minValue, int maxValue)
        {
            if (minValue == maxValue)
            {
                return minValue;
            }
            else if (maxValue < minValue)
            {
                throw new ArgumentOutOfRangeException("MaxValue must be greater than or equal to MinValue");
            }

            return minValue + (int)(NextBitfield() % (ulong)((long)maxValue - minValue));
        }

        /// <inheritdoc />
        public long NextInt64()
        {
            return (long)(NextBitfield() & 0x7FFFFFFF_FFFFFFFF);
        }

        /// <inheritdoc />
        public long NextInt64(long maxValue)
        {
            return NextInt64(0, maxValue);
        }

        /// <inheritdoc />
        public long NextInt64(long minValue, long maxValue)
        {
            if (minValue == maxValue)
            {
                return minValue;
            }
            else if (maxValue < minValue)
            {
                throw new ArgumentOutOfRangeException("MaxValue must be greater than or equal to MinValue");
            }

            ulong range = (ulong)((decimal)maxValue - minValue);
            ulong bit64Field = NextBitfield();
            ulong scale = bit64Field % range;
            return (long)((decimal)minValue + scale);
        }

        /// <inheritdoc />
        public double NextDouble()
        {
            return (double)NextInt() / DOUBLE_CAP;
        }

        /// <inheritdoc />
        public double NextDouble(double min, double max)
        {
            if (min >= max)
            {
                throw new ArgumentOutOfRangeException("Max must be greater than min");
            }

            double range = max - min;
            double scale = (double)NextInt() * (range / DOUBLE_CAP);
            return min + scale;
        }

        /// <inheritdoc />
        public float NextFloat()
        {
            // Naive implementation:
            return NextInt() / FLOAT_CAP;

            // Alternate variant that eliminates floating point division
            // Use bit manipulation to mess with the mantissa of a float32 directly
            // to generate values between 1.0 and 1.999999
            // Benchmarks to be slower on x86 processors (it was designed for MIPS...) so will leave it as a curio

            //Span<uint> scratch = stackalloc uint[1];
            //scratch[0] = 0x3F800000U | ((uint)NextBitfield() & 0x007FFF80);
            //return MemoryMarshal.Cast<uint, float>(scratch)[0] - 1.0f;
        }

#if NET6_0_OR_GREATER
        public void NextFloats(Span<float> dest)
        {
            int idx = 0;
            int end = dest.Length;
            if (Vector.IsHardwareAccelerated)
            {
                Vector<float> denom = new Vector<float>(FLOAT_CAP);
                Span<float> inputs = stackalloc float[Vector<float>.Count];
                int vectorEnd = dest.Length - (dest.Length % Vector<float>.Count);
                while (idx < vectorEnd)
                {
                    for (int inp = 0; inp < Vector<float>.Count; inp++)
                    {
                        inputs[inp] = (float)NextInt();
                    }

                    Vector.Divide(new Vector<float>(inputs), denom).CopyTo(dest.Slice(idx));
                    idx += Vector<float>.Count;
                }
            }

            while (idx < end)
            {
                dest[idx++] = NextInt() / FLOAT_CAP;
            }
        }
#else
        public void NextFloats(Span<float> dest)
        {
            for (int c = 0; c < dest.Length; c++)
            {
                dest[c] = NextInt() / FLOAT_CAP;
            }
        }
#endif

        /// <inheritdoc />
        public float NextFloat(float min, float max)
        {
            if (min >= max)
            {
                throw new ArgumentOutOfRangeException("Max must be greater than min");
            }

            float range = max - min;
            float scale = (float)NextInt() * (range / FLOAT_CAP);
            return min + scale;
        }

        /// <inheritdoc />
        public bool NextBool()
        {
            // return true if next random integer is odd
            return (NextBitfield() & 0x1) != 0;
        }

        /// <inheritdoc />
        public void NextBytes(byte[] buffer, int offset, int count)
        {
#if DEBUG
            if (Vector.IsHardwareAccelerated && count >= SIMD_PIVOT_ARRAY && ((_val & 0xFF) < 127))
#else
            if (Vector.IsHardwareAccelerated && count >= SIMD_PIVOT_ARRAY)
#endif
            {
                NextBytesSIMD(buffer.AsSpan(offset, count));
            }
            else
            {
                NextBytesSerial(buffer.AsSpan(offset, count));
            }
        }

        /// <inheritdoc />
        public void NextBytes(byte[] buffer)
        {
#if DEBUG
            if (Vector.IsHardwareAccelerated && buffer.Length >= SIMD_PIVOT_ARRAY && ((_val & 0xFF) < 127))
#else
            if (Vector.IsHardwareAccelerated && buffer.Length >= SIMD_PIVOT_ARRAY)
#endif
            {
                NextBytesSIMD(buffer.AsSpan());
            }
            else
            {
                NextBytesSerial(buffer.AsSpan());
            }
        }

        /// <inheritdoc />
        public void NextBytes(Span<byte> buffer)
        {
#if DEBUG
            if (Vector.IsHardwareAccelerated && buffer.Length >= SIMD_PIVOT_ARRAY && ((_val & 0xFF) < 127))
#else
            if (Vector.IsHardwareAccelerated && buffer.Length >= SIMD_PIVOT_ARRAY)
#endif
            {
                NextBytesSIMD(buffer);
            }
            else
            {
                NextBytesSerial(buffer);
            }
        }

        /// <summary>
        /// Primary generator function.
        /// Advances the stream of pseudorandom data that is stored in the 64-bit state field and returns it
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong NextBitfield()
        {
            unchecked
            {
                // the shift is because the lsb byte has lower entropy because the multiply shifted everything left
                _val = unchecked(_val * 0x5DEECE66DL + 0xBL) >> 16;
                _val = unchecked(0x4182BED5 * _val); // overflow occurs here!
                return _val;
            }
        }

        /// <summary>
        /// Non-SIMD implementation of NextBytes()
        /// </summary>
        /// <param name="buffer"></param>
        internal void NextBytesSerial(Span<byte> buffer)
        {
            int threadLocal_bytesGenIndex = _bytesGenIndex; // Hackish thread safety
            // First, see how cleanly we can cast byte -> int64 with respect to memory word lines
            int premainder = (sizeof(ulong) - (threadLocal_bytesGenIndex % sizeof(ulong))) % sizeof(ulong);
            bool willInt64BufferBeAligned = BinaryHelpers.GetMemoryAlignment<byte>(buffer, sizeof(ulong)) == premainder;
            if (buffer.Length >= 32 && (BinaryHelpers.SupportsUnalignedMemoryAccess || willInt64BufferBeAligned))
            {
                ReadOnlySpan<ulong> entropy = MemoryMarshal.Cast<byte, ulong>(SharedEntropy);
                int entropyIdxInt64 = threadLocal_bytesGenIndex >> 3; // / sizeof(ulong);
                int int64ValuesCopied = 0;

                Span<ulong> tmpInt64 = stackalloc ulong[1];
                Span<byte> tmpBytes = MemoryMarshal.Cast<ulong, byte>(tmpInt64);

                // Handle if the previous byte generation loop didn't copy an entire ulong last time
                if (premainder > 0)
                {
                    tmpInt64[0] = _val ^ entropy[entropyIdxInt64];
                    tmpBytes.Slice(sizeof(ulong) - premainder, premainder).CopyTo(buffer);
                    entropyIdxInt64++;

                    if (entropyIdxInt64 == entropy.Length)
                    {
                        entropyIdxInt64 = 0;
                        NextBitfield();
                    }
                }

                int remainder = (buffer.Length - premainder) % sizeof(ulong);
                int int64ValsToCopy = (buffer.Length - premainder - remainder) >> 3; // / sizeof(ulong)

                // xor with the shared entropy and copy to dest 64 bits at a time
                Span<ulong> destInt64 = MemoryMarshal.Cast<byte, ulong>(buffer.Slice(premainder));
                while (int64ValuesCopied < int64ValsToCopy)
                {
                    destInt64[int64ValuesCopied++] = _val ^ entropy[entropyIdxInt64++];

                    if (entropyIdxInt64 == entropy.Length)
                    {
                        entropyIdxInt64 = 0;
                        NextBitfield();
                    }
                }

                if (remainder > 0)
                {
                    tmpInt64[0] = _val ^ entropy[entropyIdxInt64];
                    tmpBytes.Slice(0, remainder).CopyTo(buffer.Slice(buffer.Length - remainder, remainder));
                }

                _bytesGenIndex = remainder + (entropyIdxInt64 << 3); // * sizeof(ulong)
            }
            else
            {
                // Fallback where there's only a few bytes to do,
                // or the buffer copy will be unaligned and the architecture doesn't support it (e.g. older ARM)
                int bytesWritten = 0;
                int valBitIdx = threadLocal_bytesGenIndex * 8;
                while (bytesWritten < buffer.Length)
                {
                    buffer[bytesWritten++] = (byte)(((_val >> valBitIdx) & 0xFF) ^ SharedEntropy[threadLocal_bytesGenIndex++]);
                    valBitIdx = (valBitIdx + 8) % 64;
                    if (threadLocal_bytesGenIndex == SharedEntropy.Length)
                    {
                        threadLocal_bytesGenIndex = 0;
                        NextBitfield();
                    }
                }

                _bytesGenIndex = threadLocal_bytesGenIndex;
            }
        }

        private void NextBytesSIMD(Span<byte> buffer)
        {
            // Use SIMD to hash 32 (or more) bytes at once
            // Since it's a bit tough to actually hash a bunch of fields in parallel,
            // this algorithm cheats and does sequential XORs with a previously generated entropy field
            int threadLocal_bytesGenIndex = _bytesGenIndex; // Hackish thread safety
            int outIdx = 0;
            while (true)
            {
                // Fill vector scratch with new entropy
                for (int vectorSourceOffset = (threadLocal_bytesGenIndex / Vector<byte>.Count) * Vector<byte>.Count;
                    vectorSourceOffset < _vectorScratch.Length;
                    vectorSourceOffset += Vector<byte>.Count)
                {
                    Vector.Xor(
                        Vector.AsVectorByte(new Vector<ulong>(_val)),
                        new Vector<byte>(SharedEntropy, vectorSourceOffset))
                        .CopyTo(_vectorScratch, vectorSourceOffset);
                }

                // Copy as much as we can to output
                int copySizeBytes = FastMath.Min(buffer.Length - outIdx, _vectorScratch.Length - threadLocal_bytesGenIndex);
                _vectorScratch.AsSpan(threadLocal_bytesGenIndex, copySizeBytes).CopyTo(buffer.Slice(outIdx, copySizeBytes));
                outIdx += copySizeBytes;
                threadLocal_bytesGenIndex += copySizeBytes;
                if (threadLocal_bytesGenIndex == SharedEntropy.Length)
                {
                    threadLocal_bytesGenIndex = 0;
                    NextBitfield();
                }

                if (outIdx == buffer.Length)
                {
                    _bytesGenIndex = threadLocal_bytesGenIndex;
                    return;
                }
            }
        }
    }
}
