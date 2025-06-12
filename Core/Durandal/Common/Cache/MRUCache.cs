using Durandal.Common.Collections;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// Implementation of a most-recently-used cache. This class is thread safe.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class MRUCache<K, V> : IReadThroughCache<K, V>
    {
        private readonly FastConcurrentDictionary<K, V> _cachedValues;
        private readonly ConcurrentQueue<K> _keyQueue;
        private readonly int _cacheCapacity;
        private readonly Func<K, V> _producer;

        public MRUCache(Func<K, V> producer, int cacheCapacity)
        {
            _cacheCapacity = cacheCapacity;
            _producer = producer;
            _cachedValues = new FastConcurrentDictionary<K, V>();
            _keyQueue = new ConcurrentQueue<K>();
        }

        public int CacheCapacity => _cacheCapacity;

        public int ItemsCached => _cachedValues.Count;

        public void Clear()
        {
            _cachedValues.Clear();
            _keyQueue.Clear();
        }

        public Task<V> GetCacheAsync(K key)
        {
            return Task.FromResult(GetCache(key));
        }

        public V GetCache(K key)
        {
            V returnVal;
            if (!_cachedValues.TryGetValue(key, out returnVal))
            {
                // Cache miss. Prune the oldest value if applicable

                if (ItemsCached > CacheCapacity)
                {
                    K oldestKey;
                    // bugbug: there's no guarantee that this queue is consistent with the dictionary,
                    // so a key could potentially get removed and added at the same time and the queue won't track it, leading to a memory leak
                    if (_keyQueue.TryDequeue(out oldestKey))
                    {
                        _cachedValues.Remove(oldestKey);
                    }
                }

                // And add a new value
                returnVal = _producer(key);
                _keyQueue.Enqueue(key);
                _cachedValues[key] = returnVal;
            }

            return returnVal;
        }

        public void Dispose() { }
    }
}
