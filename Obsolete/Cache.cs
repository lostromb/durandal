namespace Stromberg.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// A simple cache for storing things that have an expiration time.
    /// This class is thread-safe.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Cache<T> where T : class
    {
        private IDictionary<string, CachedItem<T>> _cache;
        private readonly int _defaultCacheSeconds;
        private int _gcCounter = 0;
        private ReaderWriterLockSlim _mutex = new ReaderWriterLockSlim();

        public Cache(int defaultExpirationTimeSeconds)
        {
            this._cache = new Dictionary<string, CachedItem<T>>();
            this._defaultCacheSeconds = defaultExpirationTimeSeconds;
        }

        public string Store(T item)
        {
            return this.Store(item, _defaultCacheSeconds);
        }

        public string Store(T item, int secondsToCache)
        {
            return this.Store(Guid.NewGuid().ToString("N"), item, secondsToCache);
        }

        public string Store(string key, T item)
        {
            return this.Store(key, item, _defaultCacheSeconds);
        }

        public string Store(string key, T item, int secondsToCache)
        {
            this.ClearOldEntries();
            int cacheExpireTime = secondsToCache;
            if (cacheExpireTime <= 0)
            {
                cacheExpireTime = _defaultCacheSeconds;
            }
            
            CachedItem<T> thing = new CachedItem<T>()
            {
                Value = item,
                StoreTime = DateTime.Now,
                ExpireTime = DateTime.Now.AddSeconds(cacheExpireTime)
            };
            _mutex.EnterWriteLock();
            try
            {
                // Does the thing already exist?
                if (this._cache.ContainsKey(key))
                {
                    // Overwrite it
                    this._cache.Remove(key);
                }
                this._cache.Add(key, thing);
            }
            finally
            {
                _mutex.ExitWriteLock();
            }
            return key;
        }

        public bool ContainsKey(string key)
        {
            _mutex.EnterReadLock();
            try
            {
                return this._cache.ContainsKey(key);
            }
            finally
            {
                _mutex.ExitReadLock();
            }
        }

        public T Retrieve(string key)
        {
            _mutex.EnterReadLock();
            try
            {
                if (this._cache.ContainsKey(key))
                {
                    return this._cache[key].Value;
                }
                return null;
            }
            finally
            {
                _mutex.ExitReadLock();
            }
        }

        private void ClearOldEntries()
        {
            if (this._cache.Count > 0 && this._gcCounter > 10)
            {
                _mutex.EnterWriteLock();
                try
                {
                    this._gcCounter = 0;
                    DateTime now = DateTime.Now;
                    HashSet<string> itemsToRemove = new HashSet<string>();
                    foreach (string key in this._cache.Keys)
                    {
                        if (this._cache[key].ExpireTime < now)
                        {
                            itemsToRemove.Add(key);
                        }
                    }
                    foreach (string key in itemsToRemove)
                    {
                        this._cache.Remove(key);
                    }
                }
                finally
                {
                    _mutex.ExitWriteLock();
                }
            }
        }
    }
}
