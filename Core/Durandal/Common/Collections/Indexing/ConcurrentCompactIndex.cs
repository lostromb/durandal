using Durandal.Common.File;

namespace Durandal.Common.Collections.Indexing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Durandal.Common.IO;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Utils;

    /// <summary>
    /// A basic implementation of CompactIndex which stores values in a giant list.
    /// All operations including enumeration are thread-safe using a reader-writer lock
    /// </summary>
    public class ConcurrentCompactIndex<T> : ICompactIndex<T> where T : class
    {
        public readonly Compact<T> NULL_INDEX = new Compact<T>(0xFFFFFFFF);

        private long _memoryUse;
        private uint _nextToken;
        private readonly IDictionary<int, Compact<T>> _forwardLookup;
        private readonly IList<byte[]> _reverseLookup;
        private readonly IByteConverter<T> _byteConverter;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
        private int _disposed = 0;

        public ConcurrentCompactIndex(IByteConverter<T> contentEncoder)
        {
            _nextToken = 0;
            _forwardLookup = new Dictionary<int, Compact<T>>();
            _reverseLookup = new List<byte[]>();
            _byteConverter = contentEncoder;
            _memoryUse = 0;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ConcurrentCompactIndex()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Returns the number of bytes stored in this index
        /// </summary>
        public long MemoryUse
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return (long)(_memoryUse * 3.0);
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Returns the current compression ratio for pooled data
        /// </summary>
        public double CompressionRatio
        {
            get
            {
                return 1.0;
            }
        }

        /// <summary>
        /// Stores a new value in the index and return a key that can retrieve it later.
        /// </summary>
        /// <param name="value">The value to be stored</param>
        /// <returns>A key that can retrieve this object later.</returns>
        public Compact<T> Store(T value)
        {
            // Is it null?
            if (value == null)
            {
                return NULL_INDEX;
            }

            int hash = value.GetHashCode();

            _lock.EnterUpgradeableReadLock();
            try
            {
                // Does it already exist?
                if (_forwardLookup.ContainsKey(hash))
                {
                    return _forwardLookup[hash];
                }

                byte[] encodedData = _byteConverter.Encode(value);

                _lock.EnterWriteLock();
                try
                {
                    _memoryUse += 8L + encodedData.Length;
                    // Create a new entry
                    _forwardLookup[hash] = new Compact<T>(_nextToken);
                    _reverseLookup.Add(encodedData);
                    return new Compact<T>(_nextToken++);
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Returns the index of the specified object in the store.
        /// If the object does not exist, this returns NULL_INDEX
        /// </summary>
        /// <param name="value">The item to check</param>
        /// <returns>The key that will return this item if passed into Retrieve(key)</returns>
        public Compact<T> GetIndex(T value)
        {
            Compact<T> returnVal;
            if (value == null)
            {
                return NULL_INDEX;
            }

            _lock.EnterReadLock();
            try
            {
                if (_forwardLookup.TryGetValue(value.GetHashCode(), out returnVal))
                {
                    return returnVal;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            return NULL_INDEX;
        }

        /// <summary>
        /// Tests to see if a given key exists in the index
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Contains(Compact<T> key)
        {
            if (key.Addr == NULL_INDEX.Addr) // Null is a special value that is always in the set
            {
                return true;
            }

            _lock.EnterReadLock();
            try
            {
                return key.Addr >= 0 && key.Addr < _reverseLookup.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Tests to see if a given object exists in the index
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Contains(T value)
        {
            _lock.EnterReadLock();
            try
            {
                return _forwardLookup.ContainsKey(value.GetHashCode());
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Returns the number of items stored in the index
        /// </summary>
        public int GetCount()
        {
            _lock.EnterReadLock();
            try
            {
                return _reverseLookup.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Retrieve a value from the store based on a key.
        /// </summary>
        /// <param name="key">The key to use</param>
        /// <returns>The value that was originally stored under that key</returns>
        public T Retrieve(Compact<T> key)
        {
            if (key.Addr == NULL_INDEX.Addr)
                return null;

            _lock.EnterReadLock();
            try
            {
                if (key.Addr >= 0 && key.Addr < _reverseLookup.Count)
                {
                    byte[] t = _reverseLookup[(int)key.Addr];
                    return _byteConverter.Decode(t, 0, t.Length);
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            throw new ArgumentException("Attempted to retrieve nonexistent key " + key + " from compact index");
        }

        /// <summary>
        /// Removes all items from this store.
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _nextToken = 0;
                _forwardLookup.Clear();
                _reverseLookup.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public Compact<T> GetNullIndex()
        {
            return NULL_INDEX;
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
                _lock?.Dispose();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new EnumeratorImpl<T>(_reverseLookup, _lock, _byteConverter);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new EnumeratorImpl<T>(_reverseLookup, _lock, _byteConverter);
        }

        private class EnumeratorImpl<E> : IEnumerator<E> where E : class
        {
            private IByteConverter<E> _decoder;
            private int _curIndex;
            private IList<byte[]> _baseList;
            private ReaderWriterLockSlim _lock;
            private E _current;

            public EnumeratorImpl(IList<byte[]> list, ReaderWriterLockSlim mutex, IByteConverter<E> decoder)
            {
                _lock = mutex;
                _baseList = list;
                _decoder = decoder;
                Reset();
            }

            public E Current
            {
                get
                {
                    return _current;
                }
            }

            public void Dispose() {}

            object System.Collections.IEnumerator.Current
            {
                get
                {
                    return _current;
                }
            }

            public bool MoveNext()
            {
                _curIndex = _curIndex + 1;

                _lock.EnterReadLock();
                try
                {
                    if (_baseList.Count <= _curIndex)
                    {
                        //End of list
                        return false;
                    }
                    byte[] t = _baseList[_curIndex];
                    _current = _decoder.Decode(t, 0, t.Length);
                    return true;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }

            public void Reset()
            {
                _current = null;
                _curIndex = -1;
            }
        }

        public static ConcurrentCompactIndex<string> BuildStringIndex()
        {
            return new ConcurrentCompactIndex<string>(new StringByteConverter());
        }
    }
}
