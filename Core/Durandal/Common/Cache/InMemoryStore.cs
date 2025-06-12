using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;

namespace Durandal.Common.Cache
{
    public class InMemoryStore<T> : IStore<T>
    {
        private ReaderWriterLockSlim _mutex;
        private IDictionary<string, T> _values;
        private int _disposed = 0;

        public InMemoryStore()
        {
            _values = new Dictionary<string, T>();
            _mutex = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~InMemoryStore()
        {
            Dispose(false);
        }
#endif

        public void CreateOrUpdate(string key, T item)
        {
            _mutex.EnterWriteLock();
            try
            {
                if (_values.ContainsKey(key))
                {
                    _values.Remove(key);
                }

                _values.Add(key, item);
            }
            finally
            {
                _mutex.ExitWriteLock();
            }
        }

        public void Delete(string key)
        {
            _mutex.EnterWriteLock();
            try
            {
                if (_values.ContainsKey(key))
                {
                    _values.Remove(key);
                }
            }
            finally
            {
                _mutex.ExitWriteLock();
            }
        }

        public Task<RetrieveResult<T>> TryRetrieveAsync(string key)
        {
            return Task.FromResult(TryRetrieve(key));
        }

        public RetrieveResult<T> TryRetrieve(string key)
        {
            _mutex.EnterReadLock();
            try
            {
                if (_values.ContainsKey(key))
                {
                    return new RetrieveResult<T>(_values[key]);
                }
                else
                {
                    return new RetrieveResult<T>();
                }
            }
            finally
            {
                _mutex.ExitReadLock();
            }
        }

        public void Clear()
        {
            _mutex.EnterWriteLock();
            try
            {
                _values.Clear();
            }
            finally
            {
                _mutex.ExitWriteLock();
            }
        }

        public bool ContainsKey(string key)
        {
            _mutex.EnterWriteLock();
            try
            {
                return _values.ContainsKey(key);
            }
            finally
            {
                _mutex.ExitWriteLock();
            }
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
                _mutex?.Dispose();
            }
        }
    }
}
