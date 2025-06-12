using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Durandal.Common.Collections
{
    /// <summary>
    /// A wrapper over a dictionary that uses a ReaderWriter lock to enforce concurrent access.
    /// Enumeration is not supported by this class.
    /// </summary>
    /// <typeparam name="A">The key type</typeparam>
    /// <typeparam name="B"></typeparam>
    [Obsolete("Use FastConcurrentDictionary instead")]
    public class ConcurrentDictionary<A, B> : IDictionary<A, B>, IDisposable
    {
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private readonly Dictionary<A, B> _dict = new Dictionary<A, B>();
        private int _disposed = 0;

        public ConcurrentDictionary()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ConcurrentDictionary()
        {
            Dispose(false);
        }
#endif

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
                _lock.Dispose();
            }
        }

        public ICollection<A> Keys
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return new List<A>(_dict.Keys);
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public ICollection<B> Values
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return new List<B>(_dict.Values);
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _dict.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public B this[A key]
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _dict[key];
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }

            set
            {
                _lock.EnterWriteLock();
                try
                {
                    _dict[key] = value;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
        }

        public void Add(A key, B value)
        {
            _lock.EnterWriteLock();
            try
            {
                _dict.Add(key, value);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool ContainsKey(A key)
        {
            _lock.EnterReadLock();
            try
            {
                return _dict.ContainsKey(key);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool Remove(A key)
        {
            _lock.EnterWriteLock();
            try
            {
                return _dict.Remove(key);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryGetValue(A key, out B value)
        {
            _lock.EnterReadLock();
            try
            {
                return _dict.TryGetValue(key, out value);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Add(KeyValuePair<A, B> item)
        {
            _lock.EnterWriteLock();
            try
            {
                ((IDictionary<A, B>)_dict).Add(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _dict.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool Contains(KeyValuePair<A, B> item)
        {
            _lock.EnterReadLock();
            try
            {
                return _dict.Contains(item);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void CopyTo(KeyValuePair<A, B>[] array, int arrayIndex)
        {
            _lock.EnterReadLock();
            try
            {
                ((IDictionary<A, B>)_dict).CopyTo(array, arrayIndex);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool Remove(KeyValuePair<A, B> item)
        {
            _lock.EnterWriteLock();
            try
            {
                return ((IDictionary<A, B>)_dict).Remove(item);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public IEnumerator<KeyValuePair<A, B>> GetEnumerator()
        {
            throw new NotImplementedException("Cannot enumerate over concurrent dictionaries");
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException("Cannot enumerate over concurrent dictionaries");
        }
    }
}
