using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using System;
using System.Threading;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// An enhancement of <see cref="LockFreeCache{E}"/>. In this instance, the cache size is not fixed, but rather
    /// underlying caches of multiple sizes are scaled to arbitrary demand in an attempt to keep cache usage optimal,
    /// having the balance the requirements of avoiding cache misses while also avoiding keeping huge amounts of useless
    /// objects in memory.
    /// </summary>
    /// <typeparam name="E">The type of object to hold in this pool</typeparam>
    public class DynamicLockFreeCache<E> where E : class
    {
        // Minimum cache size
        private const int MIN_CACHE_SIZE = 8;

        // Maximum cache size - 16 million entries
        private const int MAX_CACHE_SIZE = 0x1 << 24;

        // Make an evaluation for resizing after each of this many cache operations
        private const int SCALE_CHECK_INTERVAL = 1000;

        // Used for the moving average estimate of required capacity
        // Larger numbers = smoother updates
        private const float CAPACITY_UPDATE_SMOOTHNESS = 16;
        private const float CAPACITY_UPDATE_SMOOTHNESS_NUMERATOR = CAPACITY_UPDATE_SMOOTHNESS - 1;
        private const float CAPACITY_OVERESTIMATE_BUFFER = 1.2f;

        // The minimum amount of time to allow to pass in between rescaling the cache
        private readonly long _minScaleIntervalTicks;

        // Flag that indicates whether we need to be extra careful about leaking objects
        private readonly bool _poolObjectsAreIDisposable;

        // Mutex used while rescaling
        private object _lock = new object();

        // Attempts to track the ideal size of the cache
        private float _idealCapacity = 0;

        // Counter that is used to periodically check if we can rescale
        private int _scaleCheckCounter = 0;

        // The timestamp in ticks that this table last finished a rescale operation
        private long _lastRescaleTimeTicks;
        
        // Volatile flag for whether we're currently rescaling
        private volatile int _isRescaling = 0;

        // The current actual cache. During rescaling, this is only read from until it is drained.
        private LockFreeCache<E> _currentCache;
        private int _currentCacheSize;
        private int _maxItemsFoundInCurrentCache;

        // If != currentcache, this is the next cache that we are currently rescaling to.
        private LockFreeCache<E> _nextCache;
        private int _nextCacheSize;

        /// <summary>
        /// Creates a dynamic lock-free cache with the specified initial cache size. The cache size must be a power of two.
        /// </summary>
        /// <param name="initialCacheSize">The initial cache size. Must be a power of two no less than 8.</param>
        /// <param name="minTimeBetweenResizes">The minimum amount of time to let elapse in between resizes of the cache. Defaults to 5 seconds.</param>
        public DynamicLockFreeCache(
            int initialCacheSize = MIN_CACHE_SIZE,
            TimeSpan? minTimeBetweenResizes = null)
        {
            if (initialCacheSize > MAX_CACHE_SIZE)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCacheSize), "Cache size must be no greater than " + MAX_CACHE_SIZE);
            }
            else if (initialCacheSize < MIN_CACHE_SIZE)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCacheSize), "Cache size must be no smaller than " + MIN_CACHE_SIZE);
            }

            if (FastMath.RoundUpToPowerOf2(initialCacheSize) != initialCacheSize)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCacheSize), "Initial size must be a power of two");
            }

            if (minTimeBetweenResizes.HasValue && minTimeBetweenResizes < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minTimeBetweenResizes), "Minimum time between resizes cannot be negative");
            }

            _minScaleIntervalTicks = minTimeBetweenResizes.GetValueOrDefault(TimeSpan.FromSeconds(5)).Ticks;
            _currentCacheSize = initialCacheSize;
            _nextCacheSize = initialCacheSize;
            _currentCache = new LockFreeCache<E>(_currentCacheSize);
            _idealCapacity = ((float)_currentCacheSize) / 2.0f;
            _maxItemsFoundInCurrentCache = 0;
            _lastRescaleTimeTicks = HighPrecisionTimer.GetCurrentTicks();
#if NET5_0_OR_GREATER
            _poolObjectsAreIDisposable = typeof(E).IsAssignableTo(typeof(IDisposable));
#else
            _poolObjectsAreIDisposable = true;
#endif
        }

        /// <summary>
        /// Gets the number of items this pool can hold at the moment.
        /// </summary>
        public uint PoolSize => (uint)_currentCacheSize;

        /// <summary>
        /// Gets the approximate number of items stored in this pool.
        /// </summary>
        public int ApproxItemsInPool => _currentCache.ApproxItemsInPool;

        /// <summary>
        /// Gets the amount of items that are in this cache relative to its (current) maximum capacity.
        /// Keep in mind the cache resizes as needed so this use ratio shouldn't be relied on
        /// </summary>
        public float UseRatio => _currentCache.UseRatio;

        /// <summary>
        /// Attempts to remove an item from the cache. Returns null if nothing was available.
        /// </summary>
        /// <returns>A cached item, or null.</returns>
#if DEBUG
        public E TryDequeue(
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0)
#else
        public E TryDequeue()
        #endif
        {
            if (_isRescaling != 0)
            {
                // We are in the middle of a rescaling operation.
                // This means we dequeue from the current cache but enqueue to the next cache.
                E returnVal = _poolObjectsAreIDisposable ? _currentCache.TryDequeueComprehensive() : _currentCache.TryDequeue();

                if (returnVal == null)
                {
                    // If we're rescaling and have drained the current cache, commit the rescale right now
                    lock (_lock)
                    {
                        if (_isRescaling != 0)
                        {
                            // COMPLETE A SCALE INCREASE OR DECREASE
                            _currentCacheSize = _nextCacheSize;
                            _currentCache = _nextCache;
                            _maxItemsFoundInCurrentCache = 0;
                            _lastRescaleTimeTicks = HighPrecisionTimer.GetCurrentTicks();
                            _isRescaling = 0; // _isRescaling has to be the last value set because it enforces memory ordering
                            returnVal = _currentCache.TryDequeue(); // And try to provide a return val to the caller

                            if (_nextCacheSize == MAX_CACHE_SIZE)
                            {
                                ILogger logger = DebugLogger.Default.Clone("DynamicLockFreeCache");
                                logger.LogFormat(
                                    LogLevel.Err,
                                    DataPrivacyClassification.SystemMetadata,
                                    "DynamicLockFreeCache<{0}> has scaled to maximum capacity; check your code for object pool leaks",
                                    typeof(E).Name,
                                    _currentCacheSize,
                                    _nextCacheSize,
                                    _currentCache.UseRatio);
#if DEBUG
                                logger.LogFormat(
                                    LogLevel.Wrn,
                                    DataPrivacyClassification.SystemMetadata,
                                    "Function call which triggered the size increase: {0}, in {1} line {2}",
                                    callerMemberName, callerFilePath, callerLineNumber);
#endif
                            }
                        }
                    }
                }

                return returnVal;
            }
            else
            {
                E returnVal = _currentCache.TryDequeue();

                // See if we need to trigger a scale increase based on non-zero cache miss rate
                if (CanWeTryResizing())
                {
                    if (_currentCacheSize < MAX_CACHE_SIZE &&
                        _currentCacheSize < (int)_idealCapacity)
                    {
                        lock (_lock)
                        {
                            if (_isRescaling == 0 && _currentCacheSize < MAX_CACHE_SIZE)
                            {
                                // BEGIN A SCALE INCREASE
                                _nextCacheSize = _currentCacheSize << 1;
                                _nextCache = new LockFreeCache<E>(_nextCacheSize);
//#if DEBUG
//                                DebugLogger.Default.Log(
//                                    LogLevel.Std,
//                                    DataPrivacyClassification.SystemMetadata,
//                                    "Scaling {0} pool UP from {1} to {2} (ratio is {3})",
//                                    typeof(E).Name,
//                                    _currentCacheSize,
//                                    _nextCacheSize,
//                                    _currentCache.UseRatio);
//#endif
                                _isRescaling = 1; // _isRescaling has to be the last value set because it enforces memory ordering
                            }
                        }
                    }
                }

                return returnVal;
            }
        }

        /// <summary>
        /// Tries to dequeue an item, checking the entire pool if necessary.
        /// This can be very slow for large caches.
        /// </summary>
        /// <returns>A cached item, or null</returns>
        public E TryDequeueComprehensive()
        {
            if (_isRescaling != 0)
            {
                // We are in the middle of a rescaling operation.
                // This means we dequeue from the current cache but enqueue to the next cache.
                E returnVal = _currentCache.TryDequeueComprehensive();

                if (returnVal == null)
                {
                    // If we're rescaling and have drained the current cache, commit the rescale right now
                    lock (_lock)
                    {
                        if (_isRescaling != 0)
                        {
                            // COMPLETE A SCALE INCREASE OR DECREASE
                            _currentCacheSize = _nextCacheSize;
                            _currentCache = _nextCache;
                            _maxItemsFoundInCurrentCache = 0;
                            _lastRescaleTimeTicks = HighPrecisionTimer.GetCurrentTicks();
                            _isRescaling = 0; // _isRescaling has to be the last value set because it enforces memory ordering
                            returnVal = _currentCache.TryDequeueComprehensive(); // And try to provide a return val to the caller
                        }
                    }
                }

                return returnVal;
            }
            else
            {
                E returnVal = _currentCache.TryDequeueComprehensive();

                // See if we need to trigger a scale increase based on non-zero cache miss rate
                if (CanWeTryResizing())
                {
                    if (_currentCacheSize < (int)_idealCapacity)
                    {
                        lock (_lock)
                        {
                            if (_isRescaling == 0 && _currentCacheSize < MAX_CACHE_SIZE)
                            {
                                // BEGIN A SCALE INCREASE
                                _nextCacheSize = _currentCacheSize << 1;
                                _nextCache = new LockFreeCache<E>(_nextCacheSize);
//#if DEBUG
//                                DebugLogger.Default.Log(
//                                    LogLevel.Std,
//                                    DataPrivacyClassification.SystemMetadata,
//                                    "Scaling {0} pool UP from {1} to {2} (ratio is {3})",
//                                    typeof(E).Name,
//                                    _currentCacheSize,
//                                    _nextCacheSize,
//                                    _currentCache.UseRatio);
//#endif
                                _isRescaling = 1; // _isRescaling has to be the last value set because it enforces memory ordering
                            }
                        }
                    }
                }

                return returnVal;
            }
        }

        /// <summary>
        /// Attempts to add an item to the cache, without caring if it succeeded or not.
        /// </summary>
        /// <param name="toEnqueue">The item to try and enqueue. This
        /// object reference becomes invalid after calling this method.</param>
        public void TryEnqueue(E toEnqueue)
        {
            if (_isRescaling != 0)
            {
                _nextCache.TryEnqueue(toEnqueue);
            }
            else
            {
                if (CanWeTryResizing() &&
                    // Only downscale if we can safely go to 1/4 of the current size.
                    // This avoids fluctuations when we're right around the 50% usage mark
                    (_currentCacheSize >> 2) > (int)_idealCapacity)
                {
                    lock (_lock)
                    {
                        if (_isRescaling == 0 && _currentCacheSize > MIN_CACHE_SIZE)
                        {
                            // BEGIN A SCALE DECREASE
                            _nextCacheSize = _currentCacheSize >> 1;
                            _nextCache = new LockFreeCache<E>(_nextCacheSize);
//#if DEBUG
//                            DebugLogger.Default.Log(
//                                LogLevel.Std,
//                                DataPrivacyClassification.SystemMetadata,
//                                "Scaling {0} pool DOWN from {1} to {2} (ratio is {3})",
//                                typeof(E).Name,
//                                _currentCacheSize,
//                                _nextCacheSize,
//                                _currentCache.UseRatio);
//#endif
                            _isRescaling = 1; // _isRescaling has to be the last value set because it enforces memory ordering
                            _nextCache.TryEnqueue(toEnqueue);
                            return;
                        }
                    }
                }

                _currentCache.TryEnqueue(toEnqueue);
            }
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
            bool returnVal;
            if (_isRescaling != 0)
            {
                returnVal = _nextCache.TryEnqueue(ref toEnqueue);
                return returnVal;
            }
            else
            {
                if (CanWeTryResizing() &&
                    // Only downscale if we can safely go to 1/4 of the current size.
                    // This avoids fluctuations when we're right around the 50% usage mark
                    (_currentCacheSize >> 2) > (int)_idealCapacity)
                {
                    lock (_lock)
                    {
                        if (_isRescaling == 0 && _currentCacheSize > MIN_CACHE_SIZE)
                        {
                            // BEGIN A SCALE DECREASE
                            _nextCacheSize = _currentCacheSize >> 1;
                            _nextCache = new LockFreeCache<E>(_nextCacheSize);
//#if DEBUG
//                            DebugLogger.Default.Log(
//                                LogLevel.Std,
//                                DataPrivacyClassification.SystemMetadata,
//                                "Scaling {0} pool DOWN from {1} to {2} (ratio is {3})",
//                                typeof(E).Name,
//                                _currentCacheSize,
//                                _nextCacheSize,
//                                _currentCache.UseRatio);
//#endif
                            _isRescaling = 1; // _isRescaling has to be the last value set because it enforces memory ordering
                            returnVal = _nextCache.TryEnqueue(ref toEnqueue);
                            return returnVal;
                        }
                    }
                }

                returnVal = _currentCache.TryEnqueue(ref toEnqueue);
                return returnVal;
            }
        }

        private bool CanWeTryResizing()
        {
            if (Interlocked.Increment(ref _scaleCheckCounter) > SCALE_CHECK_INTERVAL)
            {
                lock (_lock) // Lock is not strictly necessary here but may avoid some strange behavior when updating the capacity targets
                {
                    if (_scaleCheckCounter > SCALE_CHECK_INTERVAL) // ensure only one thread does the update each period
                    {
                        _scaleCheckCounter = 0;
                        // Update ideal capacity based on a moving average of the maximum usage seen in the current pool
                        _maxItemsFoundInCurrentCache = Math.Max(_maxItemsFoundInCurrentCache, _currentCache.ApproxItemsInPool);
                        _idealCapacity = ((_idealCapacity * CAPACITY_UPDATE_SMOOTHNESS_NUMERATOR) + (_maxItemsFoundInCurrentCache * CAPACITY_OVERESTIMATE_BUFFER)) / CAPACITY_UPDATE_SMOOTHNESS;
                        if (_idealCapacity < MIN_CACHE_SIZE)
                        {
                            _idealCapacity = MIN_CACHE_SIZE;
                        }
                        else if (_idealCapacity > MAX_CACHE_SIZE)
                        {
                            _idealCapacity = MAX_CACHE_SIZE;
                        }

                        //DebugLogger.Default.Log($"{typeof(E).Name} Usage {(float)_maxItemsFoundInCurrentCache / (float)_currentCacheSize}, CurSize {_currentCacheSize}, TargetCap {newIdealCapacity}, ActualCap {_idealCapacity}");
                        return (HighPrecisionTimer.GetCurrentTicks() - _lastRescaleTimeTicks) > _minScaleIntervalTicks;
                    }
                }
            }

            return false;
        }
    }
}
