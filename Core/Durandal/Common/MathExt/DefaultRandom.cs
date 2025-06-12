using Durandal.Common.Collections;
using Durandal.Common.IO;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// IRandom provider using the platform-provided Random class
    /// </summary>
    public class DefaultRandom : IRandom
    {
        private Random _rand;

        public DefaultRandom()
        {
            _rand = new Random();
        }

        public DefaultRandom(int seed)
        {
            _rand = new Random(seed);
        }

        public void SeedRand(int seed)
        {
            _rand = new Random(seed);
        }

        /// <inheritdoc />
        public int NextInt()
        {
            return _rand.Next();
        }

        /// <inheritdoc />
        public int NextInt(int maxValue)
        {
            return _rand.Next(maxValue);
        }

        /// <inheritdoc />
        public int NextInt(int minValue, int maxValue)
        {
            return _rand.Next(minValue, maxValue);
        }

        /// <inheritdoc />
        public long NextInt64()
        {
            ulong left = (ulong)_rand.Next(int.MinValue, int.MaxValue);
            ulong right = (ulong)_rand.Next(int.MinValue, int.MaxValue) << 32;
            ulong bit64Field = left | right;
            return (long)(bit64Field & 0x7FFFFFFF_FFFFFFFF);
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
            ulong left = (ulong)_rand.Next(int.MinValue, int.MaxValue);
            ulong right = (ulong)_rand.Next(int.MinValue, int.MaxValue) << 32;
            ulong bit64Field = left | right;
            ulong scale = bit64Field % range;
            return (long)((decimal)minValue + scale);
        }

        /// <inheritdoc />
        public void NextBytes(byte[] buffer)
        {
            _rand.NextBytes(buffer);
        }

        /// <inheritdoc />
        public void NextBytes(byte[] buffer, int offset, int count)
        {
            NextBytes(buffer.AsSpan(offset, count));
        }

        /// <inheritdoc />
        public void NextBytes(Span<byte> buffer)
        {
            using (PooledBuffer<byte> scratch = BufferPool<byte>.Rent(FastMath.Min(buffer.Length, BufferPool<byte>.DEFAULT_BUFFER_SIZE)))
            {
                int used = 0;
                while (used < buffer.Length)
                {
                    _rand.NextBytes(scratch.Buffer);
                    int thisSize = FastMath.Min(buffer.Length - used, scratch.Buffer.Length);
                    scratch.Buffer.AsSpan(0, thisSize).CopyTo(buffer.Slice(used, thisSize));
                    used += thisSize;
                }
            }
        }

        /// <inheritdoc />
        public double NextDouble()
        {
            return _rand.NextDouble();
        }

        /// <inheritdoc />
        public double NextDouble(double min, double max)
        {
            if (min >= max)
            {
                throw new ArgumentOutOfRangeException("Max must be greater than min");
            }

            return min + (_rand.NextDouble() * (max - min));
        }

        /// <inheritdoc />
        public float NextFloat()
        {
            return (float)_rand.NextDouble();
        }

        /// <inheritdoc />
        public float NextFloat(float min, float max)
        {
            if (min >= max)
            {
                throw new ArgumentOutOfRangeException("Max must be greater than min");
            }

            return min + ((float)_rand.NextDouble() * (max - min));
        }

        /// <inheritdoc />
        public bool NextBool()
        {
            return (_rand.Next() & 0x1) != 0;
        }
    }
}
