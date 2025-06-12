using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Cache
{
    /// <summary>
    ///  An even simpler definition of a cache - defines a memcache which only allows retrieval,
    ///  which must implement its own logic to determine how to optimize each retrieval
    ///  without being tied to any specific implementation. Think of it like memcache daemon on linux.
    ///  Most-Frequently-Used and Most-Recently-Used caches would fall in this category
    /// </summary>
    public interface IReadThroughCache<K, V> : IDisposable
    {
        /// <summary>
        /// Retrieves an item, either from the cache (fast) or in the case of a cache miss, from the persistent backend (slow).
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<V> GetCacheAsync(K key);

        /// <summary>
        /// Retrieves an item synchronously, either from the cache (fast) or in the case of a cache miss, from the persistent backend (slow).
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        V GetCache(K key);

        /// <summary>
        /// Clears all items from the cache, but not from the backend.
        /// </summary>
        void Clear();

        /// <summary>
        /// The maximum number of items that can be cached.
        /// </summary>
        int CacheCapacity { get; }

        /// <summary>
        /// The current number of items stored in the cache.
        /// </summary>
        int ItemsCached { get; }
    }
}
