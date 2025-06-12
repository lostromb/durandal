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
    /// </summary>
    public class BasicCompactIndex<T> : ICompactIndex<T> where T : class
    {
        public readonly Compact<T> NULL_INDEX = new Compact<T>(0xFFFFFFFF);
        
        private long _memoryUse;
        private uint _nextToken;
        private readonly IDictionary<int, Compact<T>> _forwardLookup;
        private readonly List<byte[]> _reverseLookup;
        private readonly IByteConverter<T> _byteConverter;
        private int _disposed = 0;

        public BasicCompactIndex(IByteConverter<T> contentEncoder)
        {
            _nextToken = 0;
            _forwardLookup = new Dictionary<int, Compact<T>>();
            _reverseLookup = new List<byte[]>();
            _byteConverter = contentEncoder;
            _memoryUse = 0;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~BasicCompactIndex()
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
                // 3.2 is just a correction constant for "dark memory" that the runtime uses
                return (long)(_memoryUse * 3.2);
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
                return NULL_INDEX;

            int hash = value.GetHashCode();

            // Does it already exist?
            if (_forwardLookup.ContainsKey(hash))
            {
                return _forwardLookup[hash];
            }

            byte[] encodedData = _byteConverter.Encode(value);
            _memoryUse += 8L + encodedData.Length;

            // Create a new entry
            _forwardLookup[hash] = new Compact<T>(_nextToken);
            _reverseLookup.Add(encodedData);
            return new Compact<T>(_nextToken++);
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
            if (value != null && _forwardLookup.TryGetValue(value.GetHashCode(), out returnVal))
                return returnVal;
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
                return true;
            return key.Addr >= 0 && key.Addr < _reverseLookup.Count;
        }

        /// <summary>
        /// Tests to see if a given object exists in the index
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool Contains(T value)
        {
            return _forwardLookup.ContainsKey(value.GetHashCode());
        }

        /// <summary>
        /// Returns the number of items stored in the index
        /// </summary>
        public int GetCount()
        {
            return _reverseLookup.Count;
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
            if (Contains(key))
            {
                byte[] t = _reverseLookup[(int)key.Addr];
                return _byteConverter.Decode(t, 0, t.Length);
            }
            throw new ArgumentException("Attempted to retrieve nonexistent key " + key + " from compact index");
        }

        /// <summary>
        /// Removes all items from this store.
        /// </summary>
        public void Clear()
        {
            _nextToken = 0;
            _forwardLookup.Clear();
            _reverseLookup.Clear();
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
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new EnumeratorImpl<T>(_reverseLookup.GetEnumerator(), _byteConverter);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new EnumeratorImpl<T>(_reverseLookup.GetEnumerator(), _byteConverter);
        }

        private class EnumeratorImpl<E> : IEnumerator<E> where E : class
        {
            private IEnumerator<byte[]> _rawEnum;
            private IByteConverter<E> _decoder;
            private int _disposed = 0;

            public EnumeratorImpl(IEnumerator<byte[]> rawEnum, IByteConverter<E> decoder)
            {
                _rawEnum = rawEnum;
                _decoder = decoder;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~EnumeratorImpl()
            {
                Dispose(false);
            }
#endif

            public E Current
            {
                get
                {
                    return _decoder.Decode(_rawEnum.Current, 0, _rawEnum.Current.Length);
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
                    _rawEnum.Dispose();
                }
            }

            object System.Collections.IEnumerator.Current
            {
                get
                {
                    return _decoder.Decode(_rawEnum.Current, 0, _rawEnum.Current.Length);
                }
            }

            public bool MoveNext()
            {
                return _rawEnum.MoveNext();
            }

            public void Reset()
            {
                _rawEnum.Reset();
            }
        }

        public static BasicCompactIndex<string> BuildStringIndex()
        {
            return new BasicCompactIndex<string>(new StringByteConverter());
        }
    }
}
