using Durandal.Common.Instrumentation;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// Approximately concurrent fixed-capacity queue implementation favoring maximum speed over accuracy.
    /// This can be used to create a cache of frequently-used items, generally to reduce allocations.
    /// So, for example, a call to Dequeue() from a non-empty pool may possibly return null.
    /// A call to Enqueue() is also not guaranteed to actually append anything, if there is no
    /// room immediately available on the queue.
    /// </summary>
    /// <typeparam name="E">The type of object to hold in this pool</typeparam>
    public class LockFreeCache<E> where E : class
    {
        private const int DEFAULT_READ_RETRY_COUNT = 8;
        private const int DEFAULT_WRITE_RETRY_COUNT = 8;

        // You might look at this and think it could benefit from padding to 64-byte cache lines
        // to avoid false sharing when multiple threads access adjacent array indices.
        // I benchmarked it and there's no visible change on x64. Perhaps it's because of how
        // Interlocked.Exchange interacts with L1 cache lines, don't know. But I did try it.
        private readonly E[] _pool;
        private readonly uint _poolSize;
        private readonly uint _poolSizeMask;

        // internal as they are used by DynamicReadOnlyCache to quickly estimate cache statistics
        internal int _readIndex = 0; // these values will go to int.MaxValue and wrap around; treat them as a uint
        internal int _writeIndex = 0; // they just have to be typed as int because Interlocked.Increment doesn't have an overload for ref uint
        internal int _approxItemsInPool = 0;

        /// <summary>
        /// Creates a new lock-free cache
        /// </summary>
        /// <param name="poolSize">The size of the pool. Must be a power of two</param>
        public LockFreeCache(int poolSize)
        {
            if (poolSize < 1)
            {
                throw new ArgumentOutOfRangeException("Pool size must be a positive integer");
            }

            _poolSize = (uint)poolSize;

            // Create the bit mask to convert a read/write index into a bounded pool index
            // TODO there should be a common helper in BinaryHelpers (?) to do this for us now
            int bitCount = 0;
            uint shiftedPoolSize = (uint)_poolSize;
            _poolSizeMask = 0;
            while (shiftedPoolSize > 0)
            {
                if ((shiftedPoolSize & 0x1) != 0)
                {
                    bitCount++;
                }

                shiftedPoolSize = shiftedPoolSize >> 1;
                _poolSizeMask |= shiftedPoolSize;
            }

            // Check that it's a power of two
            if (bitCount != 1)
            {
                // This rule is enforced simply for performance reasons, so that we can use a simple bit mask
                // instead of a more expensive modulo operator
                throw new ArgumentException("Pool size must be a power of two");
            }

            _pool = new E[_poolSize];
        }

        /// <summary>
        /// Gets the number of items this pool can hold
        /// </summary>
        public uint PoolSize => _poolSize;

        /// <summary>
        /// Gets the approximate number of items stored in this pool
        /// </summary>
        public int ApproxItemsInPool => _approxItemsInPool;

        /// <summary>
        /// Gets the amount of items that are in this cache relative to its maximum capacity.
        /// </summary>
        public float UseRatio => (float)(ApproxItemsInPool) / (float)_poolSize;

        /// <summary>
        /// Attempts to remove an item from the cache. Returns null if nothing was available.
        /// </summary>
        /// <returns>A cached item, or null</returns>
        public E TryDequeue()
        {
            return TryDequeueInternalAtomic();
        }

        /// <summary>
        /// Tries to dequeue an item, checking the entire pool if necessary.
        /// This can be very slow for large caches.
        /// </summary>
        /// <returns>A cached item, or null</returns>
        public E TryDequeueComprehensive()
        {
            // don't bother atomically updating the read index each time since we might iterate the entire list anyways
            // So just do very rough indexing with a local variable
            int attempt = 0;
            int readIndex = _readIndex - 1;
            while (attempt++ < (int)_poolSize)
            {
                E returnVal = Interlocked.Exchange(ref _pool[readIndex++ & _poolSizeMask], null);

                if (returnVal != null)
                {
                    _readIndex = readIndex; // VERY ROUGHLY update the global read index based on where we found this entry
                    Interlocked.Decrement(ref _approxItemsInPool);
                    return returnVal;
                }
            }

            // Out of retries. Assume pool is empty
            return null;
        }

        /// <summary>
        /// Attempts to add an item to the cache, without caring if it succeeded or not.
        /// </summary>
        /// <param name="toEnqueue">The item to try and enqueue. This
        /// object reference becomes invalid after calling this method.</param>
        public void TryEnqueue(E toEnqueue)
        {
            TryEnqueueInternalAtomic(ref toEnqueue);
        }

        /// <summary>
        /// Attempts to add an item to the cache, returning an indication of success or failure,
        /// and potentially returning an orphaned item if the cache is full. This method signature
        /// is intended for <see cref="IDisposable"/> objects which you want to dispose in the case
        /// that the cache is full. Note that if the return value is non-null, it may refer to a 
        /// different object than the one you passed in initially.
        /// </summary>
        /// <param name="toEnqueue">The item to try and enqueue. If the enqueue fails,
        /// this reference may point to a different object than was originally passed in.</param>
        /// <returns>True if the enqueue succeeded</returns>
        public bool TryEnqueue(ref E toEnqueue)
        {
            return TryEnqueueInternalAtomic(ref toEnqueue);
        }

        private E TryDequeueInternalAtomic()
        {
            // Pessimistically check if the pool is empty or near-empty
            if (_readIndex >= _writeIndex)
            {
                return null;
            }

            int attempt = 0;
            while (attempt < DEFAULT_READ_RETRY_COUNT)
            {
                // Atomically increment the read index
                int reservedReadIndex = (int)((uint)(Interlocked.Increment(ref _readIndex) - 1) & _poolSizeMask);

                // Then try to atomically pull an item from the pool
                E returnVal = Interlocked.Exchange(ref _pool[reservedReadIndex], null);

                if (returnVal != null)
                {
                    // If we got something valid, we're good
                    Interlocked.Decrement(ref _approxItemsInPool);
                    return returnVal;
                }

                attempt++;
            }

            // Out of retries. Assume pool is empty
            return null;
        }

        // note that if this method returns false, the toEnqueue parameter
        // likely will refer to a different object than what was passed in
        // originally. This is why it is a ref parameter
        private bool TryEnqueueInternalAtomic(ref E toEnqueue)
        {
            int attempt = 0;
            while (attempt < DEFAULT_WRITE_RETRY_COUNT)
            {
                // Atomically increment the write index; this gives us a "loosely guaranteed" place that should be empty
                //int reservedWriteIndex = (int)((uint)_writeIndex++ & _poolSizeMask); //nonatomic variant
                int reservedWriteIndex = (int)(((uint)Interlocked.Increment(ref _writeIndex) - 1) & _poolSizeMask);

                // Try and put the item into the pool
                // If there was already something else in that spot, try and put it back into the next place on the buffer, or until retries are exhausted
                toEnqueue = Interlocked.Exchange(ref _pool[reservedWriteIndex], toEnqueue);

                if (toEnqueue == null)
                {
                    break;
                }
                else
                {
                    attempt++;
                }
            }

            if (toEnqueue == null)
            {
                Interlocked.Increment(ref _approxItemsInPool);
                return true;
            }
            else
            {
                return false;
            }
        }

        // Nonatomic variants don't offer much benefit and perform worse under contention
        //private E TryDequeueInternalNonAtomic()
        //{
        //    // Pessimistically check if the pool is empty or near-empty
        //    uint localReadIndex = (uint)_readIndex;
        //    if (localReadIndex >= (uint)_writeIndex)
        //    {
        //        return null;
        //    }

        //    int attempt = 1;
        //    while (attempt <= DEFAULT_READ_RETRY_COUNT)
        //    {
        //        // Then try to atomically pull an item from the pool
        //        E returnVal = Interlocked.Exchange(ref _pool[localReadIndex & _poolSizeMask], null);

        //        if (returnVal != null)
        //        {
        //            // If we got something valid, we're good
        //            Interlocked.Decrement(ref _approxItemsInPool);
        //            Interlocked.Add(ref _readIndex, attempt);
        //            return returnVal;
        //        }

        //        localReadIndex++;
        //        attempt++;
        //    }

        //    // Out of retries. Assume pool is empty
        //    return null;
        //}

        // this function is broken, do not use
        //private bool TryEnqueueInternalNonAtomic(ref E toEnqueue)
        //{
        //    int attempt = 1;
        //    uint localWriteIndex = (uint)_writeIndex;
        //    while (attempt <= DEFAULT_WRITE_RETRY_COUNT)
        //    {
        //        // Try and put the item into the pool
        //        // If there was already something else in that spot, try and put it back into the next place on the buffer, or until retries are exhausted
        //        toEnqueue = Interlocked.Exchange(ref _pool[localWriteIndex & _poolSizeMask], toEnqueue);

        //        if (toEnqueue == null)
        //        {
        //            break;
        //        }
        //        else
        //        {
        //            localWriteIndex++;
        //            attempt++;
        //        }
        //    }

        //    Interlocked.Add(ref _writeIndex, attempt);
        //    if (toEnqueue == null)
        //    {
        //        Interlocked.Increment(ref _approxItemsInPool);
        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}
    }
}
