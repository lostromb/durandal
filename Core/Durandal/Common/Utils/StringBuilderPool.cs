using System;
using Durandal.Common.Collections;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Tasks;
using System.Globalization;
using Durandal.Common.Cache;
using System.Diagnostics;
using System.Threading;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// A centralized, static pool for getting string builder objects.
    /// This can be used anywhere your existing code does a "var sb = new StringBuilder() .... sb.ToString()".
    /// By avoiding the initial allocation you can speed up performance by around 15%.
    /// </summary>
    public static class StringBuilderPool
    {
        // obsolete with the introduction of the dynamic lockfree cache
        //private const int MAX_BUFFERS_PER_THREAD_POOL = 16; // must be power of two
        //private const int MAX_BUFFERS_PER_GLOBAL_POOL = 65536; // must be power of two

        // I tried using a threadlocal cache like this but it ended up degrading performance
        //private static readonly ThreadLocal<LockFreeCache<PooledStringBuilder>> _threadLocalCache = new ThreadLocal<LockFreeCache<PooledStringBuilder>>(CreateThreadLocalCache);
        private static readonly DynamicLockFreeCache<PooledStringBuilder> _globalCache = new DynamicLockFreeCache<PooledStringBuilder>(256);

        //private static LockFreeCache<PooledStringBuilder> CreateThreadLocalCache()
        //{
        //    return new LockFreeCache<PooledStringBuilder>(MAX_BUFFERS_PER_THREAD_POOL);
        //}

#if DEBUG
        /// <summary>
        /// Rents a string builder from the pool.
        /// The state of the returned builder is always guaranteed to be cleared, so you don't need to prepare anything after this call.
        /// </summary>
        /// <param name="callerFilePath">Ignore this, the compiler will fill it in</param>
        /// <param name="callerMemberName">Ignore this, the compiler will fill it in</param>
        /// <param name="callerLineNumber">Ignore this, the compiler will fill it in</param>
        /// <returns>A clear StringBuilder ready to use</returns>
        public static PooledStringBuilder Rent(
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0)
#else
        /// <summary>
        /// Rents a string builder from the pool.
        /// The state of the returned builder is always guaranteed to be cleared, so you don't need to prepare anything after this call.
        /// </summary>
        /// <returns>A clear StringBuilder ready to use</returns>
        public static PooledStringBuilder Rent()
#endif
        {
            // Try to fetch new entry from the pool, using the thread-local instance first
            PooledStringBuilder returnVal = _globalCache.TryDequeue();

            if (returnVal == null)
            {
                // Nothing in pool; create a new buffer that will populate the pool after it's disposed.
                returnVal = new PooledStringBuilder();
            }
            else
            {
                returnVal.Reset();
            }

#if DEBUG
            returnVal.MarkRented(callerFilePath, callerMemberName, callerLineNumber);
#endif
            return returnVal;
        }

        internal static void ReturnStringBuilder(PooledStringBuilder builder)
        {
            builder.Builder.Clear();
            _globalCache.TryEnqueue(ref builder);
        }
    }

    /// <summary>
    /// A handle to a <see cref="StringBuilder"/> that has been rented from the <see cref="StringBuilderPool"/>, in
    /// much the same way that a <see cref="Durandal.Common.IO.PooledBuffer{T}"/> is rented from the <see cref="Durandal.Common.IO.BufferPool{T}"/>.
    /// 
    /// </summary>
    public sealed class PooledStringBuilder : IDisposable
    {
        // performance tuned value to give a decent balance between resident memory footprint and reallocation count
        // max capacity in the CLR is 8000, so this gives room for an even 2^6 expansion if needed
        private const int BUFFER_MIN_CAPACITY = 125;
        private int _disposed = 0;

        public StringBuilder Builder { get; private set; }

        internal PooledStringBuilder()
        {
            Builder = new StringBuilder(capacity: BUFFER_MIN_CAPACITY);
        }

        public void Reset()
        {
            _disposed = 0;
        }

#if DEBUG
#pragma warning disable CA1063 // suppress "improper" finalizer style
        ~PooledStringBuilder()
        {
#if NETFRAMEWORK
            Console.WriteLine($"StringBuilder pool leak detected: Builder was rented by {_lastRentalStackTrace}");
#else
            Debug.WriteLine($"StringBuilder pool leak detected: Builder was rented by {_lastRentalStackTrace}");
#endif
        }
#pragma warning restore CA1063

        private string _lastRentalStackTrace = "Unknown";

        internal void MarkRented(string callerFilePath, string callerMemberName, int callerLineNumber)
        {
            if (callerMemberName != null)
            {
                _lastRentalStackTrace = string.Format("{0}, in {1} line {2}", callerMemberName, callerFilePath, callerLineNumber);
            }

            //Debug.WriteLine($"StringBuilder marked rented by {_lastRentalStackTrace}");
        }
#endif // DEBUG

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize",
            Justification = "Disposal semantics are different here; we are not freeing memory but instead returning objects to a pool")]
        public void Dispose()
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                // We can theoretically just ignore this, but it's set this way to make sure that users of pooled string builder
                // are very very fastidious about who owns what builders and when they get disposed.
                //Debug.WriteLine($"ERROR: Double disposal detected on {typeof(T)} buffer {Id}");
                throw new ObjectDisposedException(nameof(PooledStringBuilder), "Multiple disposal of PooledStringBuilder detected! You must dispose of buffers exactly once");
            }

            StringBuilderPool.ReturnStringBuilder(this);
#if TRACK_IDISPOSABLE_LEAKS
            GC.SuppressFinalize(this);
#endif
        }
    }
}
