using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.Security;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static Durandal.Common.Net.Http2.HPack.HuffmanTable;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// Contains some common logic shared between WorkSharingCache implementations.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    public abstract class WorkSharingCacheBase<TInput, TOutput>
    {
        protected static readonly TimeSpan MAX_WORKITEM_TIMEOUT = TimeSpan.FromMinutes(10);
        private static readonly IRandom _random = new FastRandom();
        private readonly int _cacheCapacity;

        protected readonly FastConcurrentDictionary<TInput, ProducerTask> _productionTasks;
        protected readonly TimeSpan _cacheLifetime;

        // keep a single function reference so we don't keep recreating a delegate object every time
        private readonly FastConcurrentDictionary<TInput, ProducerTask>.AugmentationDelegate<TInput, ProducerTask, IRealTimeProvider> _cacheEvictionDelegateSingleton;

        /// <summary>
        /// Constructs a new cache base which stores some common logic shared between async and non-async caches.
        /// </summary>
        /// <param name="cacheLifetime">The lifetime of each cache item.</param>
        /// <param name="maxItemCapacity">The maximum number of items to store in the cache.</param>
        protected WorkSharingCacheBase(TimeSpan cacheLifetime, int maxItemCapacity)
        {
            if (cacheLifetime <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("Cache lifetime must be a positive timespan");
            }

            if (maxItemCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException("Cache capacity must be a positive number");
            }

            _productionTasks = new FastConcurrentDictionary<TInput, ProducerTask>();
            _cacheLifetime = cacheLifetime;
            _cacheCapacity = maxItemCapacity;
            _cacheEvictionDelegateSingleton = CacheEvictionDelegate;
        }
        
        protected void CheckForStaleCacheEntries(IRealTimeProvider realTime)
        {
            // Select a single random item from the work list and check it for expiration.
            // This is intended to prune work items for keys that have been queried once and then not again
            // for a long time.
            _productionTasks.AugmentRandomItem(_random, _cacheEvictionDelegateSingleton, realTime);
        }

        private void CacheEvictionDelegate(TInput key,ref bool exists, ref ProducerTask value, IRealTimeProvider realTime)
        {
            // Prune the cache item if it's task has completed and it is either
            // 1. expired or 2. the cache is full and we need to start randomly evicting
            if (exists &&
                value.IsCompleted() &&
                (value.ExpireTime < realTime.Time ||
                _productionTasks.Count >= _cacheCapacity))
            {
                exists = false;
            }
        }

        protected struct ProducerTask
        {
            public DateTimeOffset ExpireTime;
            public Task<TOutput> Worker;

            public bool IsCompleted()
            {
                return Worker == null || Worker.IsFinished();
            }
        }
    }
}
