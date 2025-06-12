// I wanted to align this pool with ArrayPool internally, however, that
// causes some DLL binding issues with .Net Framework projects, especially when remoting is involved.
// See Github issue https://github.com/dotnet/runtime/issues/1830
// So keeping this in stasis for now.

using Durandal.Common.Cache;
using Durandal.Common.Instrumentation;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Durandal.Common.IO
{
    /// <summary>
    /// A centralized, static pool for getting multipurpose buffers of any type.
    /// This is intended for cases like stream copies where you just need a temporary scratch space; using
    /// a byte buffer helps avoid excessive allocations. Since this is a type-parametric static class,
    /// a separate instance of the pool is created for each kind of buffer used in your program.
    /// </summary>
    public static class BufferPool<T>
    {
        private const int MAX_BUFFERS_PER_THREAD_POOL = 16; // must be power of two
        private const int INITIAL_BUFFERS_PER_GLOBAL_POOL = 1024; // must be power of two

        /// <summary>
        /// The most commonly used buffer size for the pool, for cases where the actual size isn't too relevant.
        /// </summary>
        public const int DEFAULT_BUFFER_SIZE = 65536;

        /// <summary>
        /// The largest pooled size available to us. You can still request larger, you just won't get a pooled array back.
        /// </summary>
        public const int MAX_BUFFER_SIZE = 262144;

        private static readonly PooledBuffer<T> EMPTY_POOLED_BUFFER = new PooledBuffer<T>(new T[0], 0, UNMAPPED_POOL);

        // Allocate a series of array queues, each one holding a different size of buffer
        // These should be enough to handle a variety of common requested sizes.
        private const int NUM_POOLS = 6;
        internal const int UNMAPPED_POOL = NUM_POOLS;
        private const sbyte FAST_PATH_POOL = 4; // The index of the pool containing buffers of the default size
        private static readonly int[] POOL_SIZES = new int[]
        {
            0x1 << 6,  // 64
            0x1 << 10, // 1024
            0x1 << 12, // 4096
            0x1 << 14, // 16384
            0x1 << 16, // 65536
            0x1 << 18, // 262144
        };

        private static readonly ThreadLocal<LockFreeCache<PooledBuffer<T>>[]> _threadLocalBuffers;
        private static readonly DynamicLockFreeCache<PooledBuffer<T>>[] _globalBuffers;

        private static IMetricCollector _metrics = null;
        private static DimensionSet _dimensions = DimensionSet.Empty;

        public static IMetricCollector Metrics
        {
            get
            {
                return _metrics;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(Metrics));
                }

                _metrics = value;
            }
        }

        public static DimensionSet MetricDimensions
        {
            get
            {
                return _dimensions;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(MetricDimensions));
                }

                _dimensions = value;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1810:Initialize reference type static fields inline", Justification = "Complicated object construction needs to happen here")]
        static BufferPool()
        {
            // Sanity checks
            System.Diagnostics.Debug.Assert(NUM_POOLS == POOL_SIZES.Length, "Buffer pool sizes do not match up");
            System.Diagnostics.Debug.Assert(POOL_SIZES[FAST_PATH_POOL] == DEFAULT_BUFFER_SIZE, "Fast path buffer pool is not set correctly");

            _threadLocalBuffers = new ThreadLocal<LockFreeCache<PooledBuffer<T>>[]>(CreateThreadLocalBuffer);
            _globalBuffers = new DynamicLockFreeCache<PooledBuffer<T>>[NUM_POOLS];
            for (int c = 0; c < NUM_POOLS; c++)
            {
                _globalBuffers[c] = new DynamicLockFreeCache<PooledBuffer<T>>(INITIAL_BUFFERS_PER_GLOBAL_POOL);
            }
        }

        private static LockFreeCache<PooledBuffer<T>>[] CreateThreadLocalBuffer()
        {
            LockFreeCache<PooledBuffer<T>>[] returnVal = new LockFreeCache<PooledBuffer<T>>[NUM_POOLS];
            for (int c = 0; c < NUM_POOLS; c++)
            {
                returnVal[c] = new LockFreeCache<PooledBuffer<T>>(MAX_BUFFERS_PER_THREAD_POOL);
            }

            return returnVal;
        }

#if DEBUG
        /// <summary>
        /// Returns a disposable <see cref="PooledBuffer{T}" /> instance containing an array of some arbitrary number of elements (default 64Kb, but don't count on that)
        /// </summary>
        /// <param name="callerFilePath">Ignore this, the compiler will fill it in</param>
        /// <param name="callerMemberName">Ignore this, the compiler will fill it in</param>
        /// <param name="callerLineNumber">Ignore this, the compiler will fill it in</param>
        /// <returns>A pooled buffer of some arbitrary size. Buffers are not cleared between rentals, so it may initially contain garbage.</returns>
        public static PooledBuffer<T> Rent(
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0)
#else
        /// <summary>
        /// Returns a disposable <see cref="PooledBuffer{T}" /> instance containing an array of some arbitrary number of elements (default 64Kb, but don't count on that)
        /// </summary>
        /// <returns>A pooled buffer of some arbitrary size. Buffers are not cleared between rentals, so it may initially contain garbage.</returns>
        public static PooledBuffer<T> Rent()
#endif
        {
            // This method is a separate fast path so we don't have to do as much validation
            PooledBuffer<T> returnVal = _threadLocalBuffers.Value[FAST_PATH_POOL].TryDequeue();
            if (returnVal == null)
            {
                returnVal = _globalBuffers[FAST_PATH_POOL].TryDequeue();
            }

            if (returnVal != null)
            {
                // Reset pooled buffer object with new parameters (yes, we pool the pooled handles themselves since they are reference types)
                returnVal.Reset(DEFAULT_BUFFER_SIZE);
                if (_metrics != null)
                {
                    _metrics.ReportInstant(CommonInstrumentation.Key_Counter_BufferPool_BuffersRented, _dimensions);
                    _metrics.ReportInstant(CommonInstrumentation.Key_Counter_BufferPool_ElementsRented, _dimensions, DEFAULT_BUFFER_SIZE);
                }
            }
            else
            {
                // Nothing in pool; create a new buffer that will populate the pool after it's disposed.
                returnVal = new PooledBuffer<T>(new T[DEFAULT_BUFFER_SIZE], DEFAULT_BUFFER_SIZE, FAST_PATH_POOL);
                if (_metrics != null)
                {
                    _metrics.ReportInstant(CommonInstrumentation.Key_Counter_BufferPool_BuffersRented, _dimensions);
                    _metrics.ReportInstant(CommonInstrumentation.Key_Counter_BufferPool_ElementsRented, _dimensions, DEFAULT_BUFFER_SIZE);
                    _metrics.ReportInstant(CommonInstrumentation.Key_Counter_BufferPool_PooledAllocations, _dimensions, DEFAULT_BUFFER_SIZE);
                }
            }

#if DEBUG
            returnVal.MarkRented(callerFilePath, callerMemberName, callerLineNumber);
#endif
            return returnVal;
        }

#if DEBUG
        /// <summary>
        /// Returns a disposable <see cref="PooledBuffer{T}" /> instance containing an array of at least the specified number of elements.
        /// </summary>
        /// <param name="minimumRequestedSize">The minimum number of elements that you require</param>
        /// <param name="callerFilePath">Ignore this, the compiler will fill it in</param>
        /// <param name="callerMemberName">Ignore this, the compiler will fill it in</param>
        /// <param name="callerLineNumber">Ignore this, the compiler will fill it in</param>
        /// <returns>A pooled buffer of at least the requested size. Buffers are not cleared between rentals, so it may initially contain garbage.</returns>
        public static PooledBuffer<T> Rent(
            int minimumRequestedSize,
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0)
#else
        /// <summary>
        /// Returns a disposable <see cref="PooledBuffer{T}" /> instance containing an array of at least the specified number of elements.
        /// </summary>
        /// <param name="minimumRequestedSize">The minimum number of elements that you require</param>
        /// <returns>A pooled buffer of at least the requested size. Buffers are not cleared between rentals, so it may initially contain garbage.</returns>
        public static PooledBuffer<T> Rent(
            int minimumRequestedSize)
#endif
        {
            if (minimumRequestedSize < 0)
            {
                throw new ArgumentOutOfRangeException("Requested buffer size must be a positive number");
            }

            _metrics?.ReportInstant(CommonInstrumentation.Key_Counter_BufferPool_BuffersRented, _dimensions);
            if (minimumRequestedSize == 0)
            {
                return EMPTY_POOLED_BUFFER;
            }

            _metrics?.ReportInstant(CommonInstrumentation.Key_Counter_BufferPool_ElementsRented, _dimensions, minimumRequestedSize);

            // Find the bin containing buffers long enough to hold satisfy this request
            sbyte targetBin;
            if (minimumRequestedSize == DEFAULT_BUFFER_SIZE)
            {
                // Fast path for callers using the default buffer size
                targetBin = FAST_PATH_POOL;
            }
            else if (minimumRequestedSize > POOL_SIZES[NUM_POOLS - 1])
            {
                // If the requested size is something huge, we just allocate the memory directly and ignore pooling entirely.
                // Kind of defeats the purpose, but it's better than keeping around massive buffers that might only be used a few times.
                _metrics?.ReportInstant(CommonInstrumentation.Key_Counter_BufferPool_UnpooledAllocations, _dimensions, minimumRequestedSize);
                //_metrics?.ReportPercentile("BufferPool Large Allocation Size", minimumRequestedSize, _dimensions);
                return new PooledBuffer<T>(new T[minimumRequestedSize], minimumRequestedSize, UNMAPPED_POOL);
            }
            else
            {
                // Find the correct pool containing buffers which can handle this request
                // OPT we could do like a vectorized compare + POPCNT to reduce this loop to branchless
                for (targetBin = 0; minimumRequestedSize > POOL_SIZES[targetBin]; targetBin++) ;
            }

            PooledBuffer<T> returnVal = _threadLocalBuffers.Value[targetBin].TryDequeue();
            if (returnVal == null)
            {
                returnVal = _globalBuffers[targetBin].TryDequeue();
            }

            if (returnVal != null)
            {
                // Reset pooled buffer object with new parameters (yes, we pool the pooled handles themselves since they are reference types)
                returnVal.Reset(minimumRequestedSize);
            }
            else
            {
                // Nothing in pool; create a new buffer that will populate the pool after it's disposed.
                _metrics?.ReportInstant(CommonInstrumentation.Key_Counter_BufferPool_PooledAllocations, _dimensions, minimumRequestedSize);
                returnVal = new PooledBuffer<T>(new T[POOL_SIZES[targetBin]], minimumRequestedSize, targetBin);
            }

#if DEBUG
            returnVal.MarkRented(callerFilePath, callerMemberName, callerLineNumber);
#endif
            return returnVal;
        }

        /// <summary>
        /// Iterates through all currently reclaimed buffers and writes garbage to them.
        /// This method is intended for unit testing to ensure that no code maintains
        /// a pointer to the pooled buffer data after it has been reclaimed
        /// </summary>
        public static void Shred()
        {
            Queue<PooledBuffer<T>> backlog = new Queue<PooledBuffer<T>>(INITIAL_BUFFERS_PER_GLOBAL_POOL);
            foreach (DynamicLockFreeCache<PooledBuffer<T>> queue in _globalBuffers)
            {
                PooledBuffer<T> pooledBuf;

                do
                {
                    pooledBuf = queue.TryDequeue();
                    if (pooledBuf != null)
                    {
                        T[] buf = pooledBuf.Buffer;
                        Span<byte> castByteSpan = Span<byte>.Empty;
                        if (buf is byte[])
                        {
                            castByteSpan = (buf as byte[]).AsSpan(0, buf.Length);
                        }
                        else if (buf is short[])
                        {
                            castByteSpan = MemoryMarshal.AsBytes((buf as short[]).AsSpan());
                        }
                        else if (buf is int[])
                        {
                            castByteSpan = MemoryMarshal.AsBytes((buf as int[]).AsSpan());
                        }
                        else if (buf is char[])
                        {
                            castByteSpan = MemoryMarshal.AsBytes((buf as char[]).AsSpan());
                        }
                        else if (buf is float[])
                        {
                            castByteSpan = MemoryMarshal.AsBytes((buf as float[]).AsSpan());
                        }
                        else if (buf is double[])
                        {
                            castByteSpan = MemoryMarshal.AsBytes((buf as double[]).AsSpan());
                        }
                        else if (buf is long[])
                        {
                            castByteSpan = MemoryMarshal.AsBytes((buf as long[]).AsSpan());
                        }

                        if (!castByteSpan.IsEmpty)
                        {
                            FastRandom.Shared.NextBytes(castByteSpan);
                        }

                        // don't enqueue it back yet because otherwise we waste a lot of effort shredding the same buffer over again
                        backlog.Enqueue(pooledBuf);
                    }
                } while (pooledBuf != null);

                while (backlog.Count > 0)
                {
                    queue.TryEnqueue(backlog.Dequeue());
                }
            }
        }

        internal static void ReturnBuffer(PooledBuffer<T> buffer)
        {
            int sourceBin = buffer._sourceBin;

#if DEBUG
            // Relying on the PooledBuffer.Dispose method to check this for us
            System.Diagnostics.Debug.Assert(sourceBin < NUM_POOLS);
            buffer = buffer.AssertNonNull(nameof(buffer));
            if (buffer.Buffer.Length != POOL_SIZES[sourceBin])
            {
                throw new Exception("Buffer pool size is invalid");
            }
#endif
            //int bufId = buffer.Id;
            bool queueSuccess = _threadLocalBuffers.Value[sourceBin].TryEnqueue(ref buffer);
            if (!queueSuccess)
            {
                queueSuccess = _globalBuffers[sourceBin].TryEnqueue(ref buffer);
            }

            if (_metrics != null)
            {
                if (queueSuccess)
                {
                    _metrics.ReportInstant(CommonInstrumentation.Key_Counter_BufferPool_ElementsReclaimed, _dimensions, buffer._originalLength);
                }
                else
                {
                    _metrics.ReportInstant(CommonInstrumentation.Key_Counter_BufferPool_ElementsLost, _dimensions, buffer._originalLength);
                    //Debug.WriteLine($"Lost buffer {bufId}");
                }
            }
        }
    }
}