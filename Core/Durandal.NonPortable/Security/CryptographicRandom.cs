using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Security
{
    /// <summary>
    /// Cryptographically secure random implementation
    /// </summary>
    public class CryptographicRandom : IRandom, IDisposable
    {
        private const double DOUBLE_CAP = ((double)int.MaxValue) + 1;
        private const float FLOAT_CAP = ((float)int.MaxValue) + 1;

        private const int SCRATCH_SIZE = 256;
        private readonly RandomNumberGenerator _crypto;
        private readonly byte[] _scratch = new byte[SCRATCH_SIZE];
        private readonly object _mutex = new object(); // mutex covers use of scratch space only
        private int _disposed = 0;

        public CryptographicRandom()
        {
            _crypto = RandomNumberGenerator.Create();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~CryptographicRandom()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public int NextInt()
        {
            lock (_mutex)
            {
                _crypto.GetBytes(_scratch, 0, 4);
                uint bitfield = BinaryHelpers.ByteArrayToUInt32LittleEndian(_scratch, 0);
                _scratch.AsSpan(0, 4).Fill(0);
                return (int)(bitfield & 0x7FFFFFFF);
            }
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

            lock (_mutex)
            {
                _crypto.GetBytes(_scratch, 0, 4);
                uint bitfield = BinaryHelpers.ByteArrayToUInt32LittleEndian(_scratch, 0);
                _scratch.AsSpan(0, 4).Fill(0);
                return minValue + (int)(bitfield % (ulong)((long)maxValue - minValue));
            }
        }

        /// <inheritdoc />
        public long NextInt64()
        {
            lock (_mutex)
            {
                _crypto.GetBytes(_scratch, 0, 8);
                ulong bit64Field = BinaryHelpers.ByteArrayToUInt64LittleEndian(_scratch, 0);
                _scratch.AsSpan(0, 8).Fill(0);
                return (long)(bit64Field & 0x7FFFFFFF_FFFFFFFF);
            }
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

            lock (_mutex)
            {
                ulong range = (ulong)((decimal)maxValue - minValue);
                _crypto.GetBytes(_scratch, 0, 8);
                ulong bit64Field = BinaryHelpers.ByteArrayToUInt64LittleEndian(_scratch, 0);
                _scratch.AsSpan(0, 8).Fill(0);
                ulong scale = bit64Field % range;
                return (long)((decimal)minValue + scale);
            }
        }

        /// <inheritdoc />
        public void NextBytes(byte[] buffer)
        {
            _crypto.GetBytes(buffer);
        }

        /// <inheritdoc />
        public void NextBytes(byte[] buffer, int offset, int count)
        {
            _crypto.GetBytes(buffer, offset, count);
        }

        /// <inheritdoc />
        public void NextBytes(Span<byte> buffer)
        {
            lock (_mutex)
            {
                int used = 0;
                while (used < buffer.Length)
                {
                    _crypto.GetBytes(_scratch);
                    int thisSize = FastMath.Min(buffer.Length - used, SCRATCH_SIZE);
                    _scratch.AsSpan(0, thisSize).CopyTo(buffer.Slice(used, thisSize));
                    used += thisSize;
                }

                _scratch.AsSpan().Fill(0);
            }
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
            return NextInt() / FLOAT_CAP;
        }

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
            lock (_mutex)
            {
                _crypto.GetBytes(_scratch, 0, 1);
                byte bitfield = _scratch[0];
                _scratch[0] = 0;
                return (bitfield & 0x1) != 0;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _crypto?.Dispose();
            }
        }
    }
}
