using System;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Logger;
using System.Collections.Generic;
using Durandal.Common.Time;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// Defines the interface for a cache of objects. Several types of cache behaviors are supported:
    /// - Items can be stored indefinitely, bounded by some kind of memory limit or equivalent mechanism.
    /// - Items can be stored with an explicit expire time, and are automatically deleted at exactly that time.
    /// - Items can be stored with a lifetime, and remain in the cache as long as they are continually touched (accessed) within an increment shorter than that lifetime.
    /// </summary>
    /// <typeparam name="T">The type of items to be stored in the cache.</typeparam>
    public interface ICache<T> : IDisposable
    {
        /// <summary>
        /// Stores an item in the cache.
        /// </summary>
        /// <param name="key">The key to store the item under. If an item already exists with this key, the old value will be replaced.</param>
        /// <param name="item">The item to be stored.</param>
        /// <param name="expireTime">The absolute expire time of this object.
        /// If this is null, the expire time will be set to the current time + lifetime.
        /// If both values are null, the item should be cached indefinitely.</param>
        /// <param name="lifetime">The lifetime of this object. Setting this to a non-null value enables "touching" of cache items, meaning
        /// whenever an object is read from the cache, its expire time is updated to be no earlier than the current time + this lifetime.</param>
        /// <param name="fireAndForget">If true, perform the actual caching in the background and do not report task status to the caller.</param>
        /// <param name="queryLogger">A logger for tracing the cache operation.</param>
        /// <param name="realTime"></param>
        /// <returns>An async task</returns>
        Task Store(string key, T item, DateTimeOffset? expireTime, TimeSpan? lifetime, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime);

        /// <summary>
        /// Stores an item in the cache.
        /// </summary>
        /// <param name="item">The item to be stored</param>
        /// <param name="fireAndForget">If true, perform the actual caching in the background and do not report task status to the caller.</param>
        /// <param name="queryLogger">A logger for tracing the cache operation.</param>
        /// <param name="realTime"></param>
        /// <returns>An async task</returns>
        Task Store(CachedItem<T> item, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime);

        /// <summary>
        /// Store multiple items into the cache in parallel
        /// </summary>
        /// <param name="items">The items to store</param>
        /// <param name="fireAndForget">If true, perform the actual caching in the background and do not report task status to the caller.</param>
        /// <param name="queryLogger">A logger for tracing the cache operation.</param>
        /// <param name="realTime"></param>
        /// <returns>An async task</returns>
        Task Store(IList<CachedItem<T>> items, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime);

        /// <summary>
        /// Deletes an item with a specified key from the cache, if it exists
        /// </summary>
        /// <param name="key">The key to be deleted.</param>
        /// <param name="fireAndForget">If true, perform the actual delete in the background and do not report task status to the caller.</param>
        /// <param name="queryLogger">A logger for tracing the cache operation.</param>
        /// <returns>An async task</returns>
        Task Delete(string key, bool fireAndForget, ILogger queryLogger);

        /// <summary>
        /// Deletes a set of keys from the cache, if they exist.
        /// </summary>
        /// <param name="keys">The keys to be deleted.</param>
        /// <param name="fireAndForget">If true, perform the actual delete in the background and do not report task status to the caller.</param>
        /// <param name="queryLogger">A logger for tracing the cache operation.</param>
        /// <returns>An async task</returns>
        Task Delete(IList<string> keys, bool fireAndForget, ILogger queryLogger);

        /// <summary>
        /// Attempts to retrieve an item from the cache asynchronously.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="queryLogger">A logger for tracing the cache operation.</param>
        /// <param name="realTime"></param>
        /// <param name="maxSpinTime">The maximum amount of time to wait for the cached value to be written, in the case of a read-write race condition. If null, this method should block as little as possible</param>
        /// <returns>An async task</returns>
        Task<RetrieveResult<T>> TryRetrieve(string key, ILogger queryLogger, IRealTimeProvider realTime, TimeSpan? maxSpinTime = null);
    }
}
