using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using Newtonsoft.Json;
using Durandal.Common.Utils;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// In-memory implementation of ICache.
    /// This class is aware of <see cref="IDisposable"/> cache values and will handle them properly.
    /// </summary>
    /// <typeparam name="T">The types of values being stored in this cache.</typeparam>
    public class InMemoryCache<T> : ICache<T>
    {
        // attempt to cull the cache in increments of this many store operations
        private const int CACHE_CULL_INTERVAL = 50;

        private readonly FastConcurrentDictionary<string, CachedItem<T>> _values;
        private readonly SemaphoreSlim _valuesUpdatedSignal;

        private int _gcCounter = 0;
        private int _disposed = 0;

        public InMemoryCache()
        {
            _values = new FastConcurrentDictionary<string, CachedItem<T>>();
            _valuesUpdatedSignal = new SemaphoreSlim(0, 1000); // ghetto race condition here: if more than 1000 threads increment the semaphore at once, it overflows
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~InMemoryCache()
        {
            Dispose(false);
        }
#endif

        public Task Store(string key, T item, DateTimeOffset? expireTime, TimeSpan? lifetime, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime)
        {
            CachedItem<T> convertedItem = new CachedItem<T>(key, item, lifetime, expireTime);
            return Store(convertedItem, fireAndForget, queryLogger, realTime);
        }

        public Task Store(CachedItem<T> item, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime)
        {
            CullCache(realTime);

            // If expire time is not set but lifetime is, calculate the expire time from that
            if (!item.ExpireTime.HasValue && item.LifeTime.HasValue)
            {
                item.ExpireTime = realTime.Time + item.LifeTime.Value;
            }

            CachedItem<T> existingItem;
            if (_values.TryGetValueOrSet(item.Key, out existingItem, item) && existingItem.Item is IDisposable)
            {
                // We just overwrote an existing value. Dispose of the old one if necessary.
                ((IDisposable)existingItem.Item).Dispose();
            }

            if (_valuesUpdatedSignal.CurrentCount == 0)
            {
                _valuesUpdatedSignal.Release();
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        public Task Store(IList<CachedItem<T>> items, bool fireAndForget, ILogger queryLogger, IRealTimeProvider realTime)
        {
            CullCache(realTime);

            foreach (CachedItem<T> item in items)
            {
                // If expire time is not set but lifetime is, calculate the expire time from that
                if (!item.ExpireTime.HasValue && item.LifeTime.HasValue)
                {
                    item.ExpireTime = realTime.Time + item.LifeTime.Value;
                }

                CachedItem<T> existingItem;
                if (_values.TryGetValueOrSet(item.Key, out existingItem, item) && existingItem.Item is IDisposable)
                {
                    // We just overwrote an existing value. Dispose of the old one if necessary.
                    ((IDisposable)existingItem.Item).Dispose();
                }
            }

            if (_valuesUpdatedSignal.CurrentCount == 0)
            {
                _valuesUpdatedSignal.Release();
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        public Task Delete(string key, bool fireAndForget, ILogger queryLogger)
        {
            CachedItem<T> cItem;
            if (_values.TryGetValueAndRemove(key, out cItem))
            {
                if (cItem.Item is IDisposable)
                {
                    ((IDisposable)cItem.Item).Dispose();
                }
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        public Task Delete(IList<string> keys, bool fireAndForget, ILogger queryLogger)
        {
            foreach (string key in keys)
            {
                CachedItem<T> cItem;
                if (_values.TryGetValueAndRemove(key, out cItem))
                {
                    if (cItem.Item is IDisposable)
                    {
                        ((IDisposable)cItem.Item).Dispose();
                    }
                }
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        public async Task<RetrieveResult<T>> TryRetrieve(string key, ILogger queryLogger, IRealTimeProvider realTime, TimeSpan? maxSpinTime = null)
        {
            long startTime = realTime.TimestampMilliseconds;
            long endTime = startTime + (long)maxSpinTime.GetValueOrDefault(TimeSpan.Zero).TotalMilliseconds;
            bool spinwait = maxSpinTime.HasValue;
            long backoffIncrementMs = 1;
            const int MAX_BACKOFF_MS = 100;
            do
            {
                CachedItem<T> cItem;
                if (_values.TryGetValue(key, out cItem))
                {
                    if (!cItem.ExpireTime.HasValue)
                    {
                        // Item has infinite lifetime
                        return new RetrieveResult<T>(cItem.Item);
                    }
                    else
                    {
                        // Has it expired?
                        if (realTime.Time > cItem.ExpireTime.Value)
                        {
                            // This code looks super jank, but multiple disposal shouldn't actually a problem in the event
                            // of a race condition here.
                            if (cItem.Item is IDisposable)
                            {
                                ((IDisposable)cItem.Item).Dispose();
                            }

                            _values.Remove(key);
                            return new RetrieveResult<T>();
                        }

                        // Hasn't expired. Good.
                        // Touch the expire time if needed
                        if (cItem.LifeTime.HasValue)
                        {
                            DateTimeOffset touchTime = realTime.Time + cItem.LifeTime.Value;
                            if (touchTime > cItem.ExpireTime.Value)
                            {
                                cItem.ExpireTime = touchTime;
                            }
                        }

                        return new RetrieveResult<T>(cItem.Item);
                    }
                }

                if (spinwait)
                {
                    bool signalHappened = await _valuesUpdatedSignal.WaitAsync(TimeSpan.FromMilliseconds(backoffIncrementMs));
                    if (signalHappened)
                    {
                        backoffIncrementMs = Math.Min(Math.Min(backoffIncrementMs * 2, endTime - realTime.TimestampMilliseconds), MAX_BACKOFF_MS);
                    }
                }
            }
            while (realTime.TimestampMilliseconds < endTime);

            return new RetrieveResult<T>();
        }

        public RetrieveResult<T> TryRetrieveTentative(string key, IRealTimeProvider realTime)
        {
            CachedItem<T> cItem;
            if (_values.TryGetValue(key, out cItem))
            {
                if (!cItem.ExpireTime.HasValue)
                {
                    // Item has infinite lifetime
                    return new RetrieveResult<T>(cItem.Item);
                }
                else
                {
                    // Has it expired?
                    if (realTime.Time > cItem.ExpireTime.Value)
                    {
                        if (cItem.Item is IDisposable)
                        {
                            ((IDisposable)cItem.Item).Dispose();
                        }

                        _values.Remove(key);
                        return new RetrieveResult<T>();
                    }

                    // Hasn't expired. Good.
                    // Touch the expire time if needed
                    if (cItem.LifeTime.HasValue)
                    {
                        DateTimeOffset touchTime = realTime.Time + cItem.LifeTime.Value;
                        if (touchTime > cItem.ExpireTime.Value)
                        {
                            cItem.ExpireTime = touchTime;
                        }
                    }

                    return new RetrieveResult<T>(cItem.Item);
                }
            }

            return new RetrieveResult<T>();
        }

        public void Clear()
        {
            var valueEnumerator = _values.GetValueEnumerator();
            while (valueEnumerator.MoveNext())
            {
                if (valueEnumerator.Current.Value is IDisposable)
                {
                    ((IDisposable)valueEnumerator.Current.Value).Dispose();
                }
            }

            _values.Clear();
        }

        public bool ContainsKey(string key)
        {
            return _values.ContainsKey(key);
        }

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
                // This should dispose of all child values in the cache if they are also disposable.
                Clear();
            }
        }

        private void CullCache(IRealTimeProvider realTime)
        {
            if (_values.Count > 0 && ++_gcCounter > CACHE_CULL_INTERVAL)
            {
                _gcCounter = 0;
                DateTimeOffset now = realTime.Time;
                var valueEnumerator = _values.GetValueEnumerator();
                while (valueEnumerator.MoveNext())
                {
                    if (valueEnumerator.Current.Value.ExpireTime.HasValue && valueEnumerator.Current.Value.ExpireTime.Value < now)
                    {
                        // We are allowed to remove from the collection we are iterating from specifically because this is a FastConcurrentDictionary
                        if (valueEnumerator.Current.Value.Item is IDisposable)
                        {
                            ((IDisposable)valueEnumerator.Current.Value.Item).Dispose();
                        }

                        _values.Remove(valueEnumerator.Current.Key);
                    }
                }
            }
        }
    }
}
