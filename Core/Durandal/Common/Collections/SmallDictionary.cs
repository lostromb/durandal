using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using Durandal.Common.Utils;
using System.Linq;
using Durandal.Common.MathExt;

namespace Durandal.Common.Collections
{
    /// <summary>
    /// A class for storing a small set of key-value pairs in a memory-compact way. Ideal for dictionaries where the total count is less than 10.
    /// </summary>
    /// <typeparam name="K">The type of keys in the dictionary</typeparam>
    /// <typeparam name="V">The type of values in the dictionary</typeparam>
    public class SmallDictionary<K, V> : IDictionary<K, V>, IReadOnlyDictionary<K, V>
    {
        private static readonly KeyValuePair<K, V> NULL_ENTRY = default(KeyValuePair<K, V>);
        internal readonly IEqualityComparer<K> _comparer;
        private KeyValuePair<K, V>[] _nodes;

        public SmallDictionary(int initialCapacity = 2)
            : this(EqualityComparer<K>.Default, initialCapacity, null)
        {
        }

        public SmallDictionary(IEqualityComparer<K> comparer, int initialCapacity = 2)
            : this(comparer, initialCapacity, null)
        {
        }

        // not needed since this is covered by IReadOnlyCollection below
        //public SmallDictionary(IDictionary<K, V> dict) : this(EqualityComparer<K>.Default, dict.Count, dict)
        //{
        //    dict.AssertNonNull(nameof(dict));
        //}

        public SmallDictionary(IReadOnlyCollection<KeyValuePair<K, V>> list)
            : this(EqualityComparer<K>.Default, list == null ? 0 : list.Count, list)
        {
            list.AssertNonNull(nameof(list));
        }

        // just have to guess capacity here
        public SmallDictionary(IEnumerable<KeyValuePair<K, V>> enumerable, int initialCapacity = 4)
            : this(EqualityComparer<K>.Default, initialCapacity, enumerable)
        {
            enumerable.AssertNonNull(nameof(enumerable));
        }

        // not needed since this is covered by IReadOnlyCollection below
        //public SmallDictionary(IEqualityComparer<K> comparer, IDictionary<K, V> dict) : this(comparer, dict.Count, dict)
        //{
        //    dict.AssertNonNull(nameof(dict));
        //}

        public SmallDictionary(IEqualityComparer<K> comparer, IReadOnlyCollection<KeyValuePair<K, V>> list)
            : this(comparer, list == null ? 0 : list.Count, list)
        {
            list.AssertNonNull(nameof(list));
        }

        // just have to guess capacity here
        public SmallDictionary(IEqualityComparer<K> comparer, IEnumerable<KeyValuePair<K, V>> enumerable, int initialCapacity = 4)
            : this(comparer, initialCapacity, enumerable)
        {
            enumerable.AssertNonNull(nameof(enumerable));
        }

        private SmallDictionary(IEqualityComparer<K> comparer, int initialCapacity, IEnumerable<KeyValuePair<K, V>> initialData)
        {
            comparer.AssertNonNull(nameof(comparer));
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException("Capacity must be a non-negative integer");
            }

            _nodes = new KeyValuePair<K, V>[initialCapacity];
            _comparer = comparer;

            if (initialData != null)
            {
                foreach (var kvp in initialData)
                {
                    Add(kvp);
                }
            }
        }

        /// <inheritdoc />
        public V this[K key]
        {
            get
            {
                KeyValuePair<K, V> kv;
                int idx = 0;
                while (idx < Capacity)
                {
                    kv = _nodes[idx];
                    if (IsNullEntry(kv))
                    {
                        throw new KeyNotFoundException("Dictionary key not found: " + key);
                    }
                    else if (_comparer.Equals(key, _nodes[idx].Key))
                    {
                        return kv.Value;
                    }

                    idx++;
                }

                throw new KeyNotFoundException("Dictionary key not found: " + key);
            }

            set
            {
                // This code looks a lot like Add() except we allow overwriting if the value already exists.
                if (key == null)
                {
                    throw new ArgumentNullException("Cannot use a null key in a dictionary");
                }

                // Seek the list, see if the item already exists
                int idx;
                for (idx = 0; idx < Capacity && !IsNullEntry(_nodes[idx]) && !_comparer.Equals(key, _nodes[idx].Key); idx++) ;

                // Search ran off edge of table. Expand the table
                if (idx >= Capacity)
                {
                    // capacity can be zero, so make sure that capacity * 2 still works here
                    KeyValuePair<K, V>[] newTable = new KeyValuePair<K, V>[FastMath.Max(2, Capacity * 2)];
                    _nodes.CopyTo(newTable, 0);
                    _nodes = newTable;
                }
                
                // Append or overwrite the entry in the table
                _nodes[idx] = new KeyValuePair<K, V>(key, value);
            }
        }

        /// <inheritdoc />
        private int Capacity => _nodes.Length;

        /// <inheritdoc />
        public int Count
        {
            get
            {
                int idx;
                for (idx = 0; idx < Capacity && !IsNullEntry(_nodes[idx]); idx++) ;
                return idx;
            }
        }

        /// <inheritdoc />
        public bool IsReadOnly => false;

        /// <inheritdoc />
        public ICollection<K> Keys => new KeyEnumerable<K, V>(this);

        /// <inheritdoc />
        public ICollection<V> Values => new ValueEnumerable<K, V>(this);

        /// <inheritdoc />
        IEnumerable<K> IReadOnlyDictionary<K, V>.Keys => new KeyEnumerable<K, V>(this);

        /// <inheritdoc />
        IEnumerable<V> IReadOnlyDictionary<K, V>.Values => new ValueEnumerable<K, V>(this);

        /// <inheritdoc />
        public void Add(KeyValuePair<K, V> item)
        {
            if (item.Key == null)
            {
                throw new ArgumentNullException("Cannot use a null key in a dictionary");
            }

            // Seek the list, see if the item already exists
            int idx;
            for (idx = 0; idx < Capacity && !IsNullEntry(_nodes[idx]) && !_comparer.Equals(item.Key, _nodes[idx].Key); idx++) ;

            if (idx < Capacity && !IsNullEntry(_nodes[idx]))
            {
                throw new ArgumentException("Key \"" + item.Key.ToString() + "\" already exists in dictionary");
            }

            // Search ran off edge of table. Expand the table
            if (idx >= Capacity)
            {
                // capacity can be zero, so make sure that capacity * 2 still works here
                KeyValuePair<K, V>[] newTable = new KeyValuePair<K, V>[FastMath.Max(2, Capacity * 2)];
                _nodes.CopyTo(newTable, 0);
                _nodes = newTable;
            }

            // Append or overwrite the entry in the table
            _nodes[idx] = item;
        }

        /// <inheritdoc />
        public void Add(K key, V value)
        {
            Add(new KeyValuePair<K, V>(key, value));
        }

        /// <inheritdoc />
        public void Clear()
        {
            for (int idx = 0; idx < Capacity; idx++)
            {
                _nodes[idx] = NULL_ENTRY;
            }
        }

        /// <inheritdoc />
        public bool Contains(KeyValuePair<K, V> item)
        {
            return ContainsKey(item.Key);
        }

        /// <inheritdoc />
        public bool ContainsKey(K key)
        {
            // Find the item in the list
            int idx;
            for (idx = 0; idx < Capacity && !IsNullEntry(_nodes[idx]) && !_comparer.Equals(key, _nodes[idx].Key); idx++) ;

            return idx < Capacity && !IsNullEntry(_nodes[idx]);
        }

        /// <inheritdoc />
        public bool ContainsValue(V value)
        {
            // Find the item in the list
            int idx;
            for (idx = 0; idx < Capacity && !IsNullEntry(_nodes[idx]) && !Equals(value, _nodes[idx].Value); idx++) ;
            return idx < Capacity && !IsNullEntry(_nodes[idx]);
        }

        /// <inheritdoc />
        public bool Remove(KeyValuePair<K, V> item)
        {
            return Remove(item.Key);
        }

        /// <inheritdoc />
        public bool Remove(K key)
        {
            // Find the item in the list
            int idx;
            for (idx = 0; idx < Capacity && !IsNullEntry(_nodes[idx]) && !_comparer.Equals(key, _nodes[idx].Key); idx++) ;

            if (idx < Capacity && !IsNullEntry(_nodes[idx]))
            {
                // Found it.
                if (idx == Capacity - 1 || IsNullEntry(_nodes[idx + 1]))
                {
                    // If it's the last (or only) item in the list, just clear it
                    _nodes[idx] = NULL_ENTRY;
                }
                else
                {
                    // Find the last item in the list and move it to plug in the hole
                    int lastListItem;
                    for (lastListItem = Capacity - 1; lastListItem >= 0 && IsNullEntry(_nodes[lastListItem]); lastListItem--) ;
                    _nodes[idx] = _nodes[lastListItem];
                    _nodes[lastListItem] = NULL_ENTRY;
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <inheritdoc />
        public bool TryGetValue(K key, out V value)
        {
            KeyValuePair<K, V> kv;
            int idx = 0;
            while (idx < Capacity)
            {
                kv = _nodes[idx];
                if (IsNullEntry(kv))
                {
                    value = default(V);
                    return false;
                }
                else if (_comparer.Equals(key, _nodes[idx].Key))
                {
                    value = kv.Value;
                    return true;
                }

                idx++;
            }

            value = default(V);
            return false;
        }

        /// <inheritdoc />
        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            for (int idx = 0; idx < Capacity && !IsNullEntry(_nodes[idx]); idx++)
            {
                array[idx + arrayIndex] = _nodes[idx];
            }
        }

        // Avoid costly comparison of Equals<KeyValuePair> which uses reflection for value types and is slooooow
        private static bool IsNullEntry<A, B>(KeyValuePair<A, B> entry)
        {
            return Equals(entry.Key, NULL_ENTRY.Key) &&
                Equals(entry.Value, NULL_ENTRY.Value);
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return new SmallDictionaryEnumerator<K, V>(this);
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new SmallDictionaryEnumerator<K, V>(this);
        }

        private class SmallDictionaryEnumerator<TKey, TValue> : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            private readonly SmallDictionary<TKey, TValue> _dict;
            private int _idx = -1;

            public SmallDictionaryEnumerator(SmallDictionary<TKey, TValue> dict)
            {
                _dict = dict;
            }

            /// <inheritdoc />
            public KeyValuePair<TKey, TValue> Current
            {
                get
                {
                    if (_idx < 0 || _idx >= _dict.Capacity)
                    {
                        throw new IndexOutOfRangeException("Enumerated out of range");
                    }

                    return _dict._nodes[_idx];
                }
            }

            /// <inheritdoc />
            object IEnumerator.Current
            {
                get
                {
                    if (_idx < 0 || _idx >= _dict.Capacity)
                    {
                        throw new IndexOutOfRangeException("Enumerated out of range");
                    }

                    return _dict._nodes[_idx];
                }
            }

            /// <inheritdoc />
            public void Dispose()
            {
            }

            /// <inheritdoc />
            public bool MoveNext()
            {
                _idx++;
                return _idx < _dict.Capacity && !IsNullEntry(_dict._nodes[_idx]);
            }

            /// <inheritdoc />
            public void Reset()
            {
                _idx = -1;
            }
        }

        private struct KeyEnumerable<TKey, TValue> : ICollection<TKey>
        {
            private readonly SmallDictionary<TKey, TValue> _dict;

            public KeyEnumerable(SmallDictionary<TKey, TValue> dict)
            {
                _dict = dict;
            }

            /// <inheritdoc />
            public int Count => _dict.Count;

            /// <inheritdoc />
            public bool IsReadOnly => true;

            /// <inheritdoc />
            public bool Contains(TKey item)
            {
                return _dict.ContainsKey(item);
            }

            /// <inheritdoc />
            public void CopyTo(TKey[] array, int arrayIndex)
            {
                IEnumerator<TKey> enumerator = GetEnumerator();
                while (enumerator.MoveNext())
                {
                    array[arrayIndex++] = enumerator.Current;
                }
            }

            /// <inheritdoc />
            public IEnumerator<TKey> GetEnumerator()
            {
                return new SmallDictionaryKeyEnumerator<TKey, TValue>(_dict);
            }

            /// <inheritdoc />
            IEnumerator IEnumerable.GetEnumerator()
            {
                return new SmallDictionaryKeyEnumerator<TKey, TValue>(_dict);
            }

            /// <inheritdoc />
            public void Add(TKey item)
            {
                throw new NotSupportedException();
            }

            /// <inheritdoc />
            public void Clear()
            {
                throw new NotSupportedException();
            }

            /// <inheritdoc />
            public bool Remove(TKey item)
            {
                throw new NotSupportedException();
            }
        }

        private class SmallDictionaryKeyEnumerator<TKey, TValue> : IEnumerator<TKey>
        {
            private readonly SmallDictionary<TKey, TValue> _dict;
            private int _idx = -1;

            public SmallDictionaryKeyEnumerator(SmallDictionary<TKey, TValue> dict)
            {
                _dict = dict;
            }

            /// <inheritdoc />
            public TKey Current
            {
                get
                {
                    if (_idx < 0 || _idx >= _dict.Capacity)
                    {
                        throw new IndexOutOfRangeException("Enumerated out of range");
                    }

                    return _dict._nodes[_idx].Key;
                }
            }

            /// <inheritdoc />
            object IEnumerator.Current
            {
                get
                {
                    if (_idx < 0 || _idx >= _dict.Capacity)
                    {
                        throw new IndexOutOfRangeException("Enumerated out of range");
                    }

                    return _dict._nodes[_idx].Key;
                }
            }

            /// <inheritdoc />
            public void Dispose()
            {
            }

            /// <inheritdoc />
            public bool MoveNext()
            {
                _idx++;
                return _idx < _dict.Capacity && !IsNullEntry(_dict._nodes[_idx]);
            }

            /// <inheritdoc />
            public void Reset()
            {
                _idx = -1;
            }
        }

        private struct ValueEnumerable<TKey, TValue> : ICollection<TValue>
        {
            private readonly SmallDictionary<TKey, TValue> _dict;

            public ValueEnumerable(SmallDictionary<TKey, TValue> dict)
            {
                _dict = dict;
            }

            /// <inheritdoc />
            public int Count => _dict.Count;

            /// <inheritdoc />
            public bool IsReadOnly => true;

            /// <inheritdoc />
            public bool Contains(TValue item)
            {
                return _dict.ContainsValue(item);
            }

            /// <inheritdoc />
            public void CopyTo(TValue[] array, int arrayIndex)
            {
                IEnumerator<TValue> enumerator = GetEnumerator();
                while (enumerator.MoveNext())
                {
                    array[arrayIndex++] = enumerator.Current;
                }
            }

            /// <inheritdoc />
            public IEnumerator<TValue> GetEnumerator()
            {
                return new SmallDictionaryValueEnumerator<TKey, TValue>(_dict);
            }

            /// <inheritdoc />
            IEnumerator IEnumerable.GetEnumerator()
            {
                return new SmallDictionaryValueEnumerator<TKey, TValue>(_dict);
            }

            /// <inheritdoc />
            public void Add(TValue item)
            {
                throw new NotSupportedException();
            }

            /// <inheritdoc />
            public void Clear()
            {
                throw new NotSupportedException();
            }

            /// <inheritdoc />
            public bool Remove(TValue item)
            {
                throw new NotSupportedException();
            }
        }

        private class SmallDictionaryValueEnumerator<TKey, TValue> : IEnumerator<TValue>
        {
            private readonly SmallDictionary<TKey, TValue> _dict;
            private int _idx = -1;

            public SmallDictionaryValueEnumerator(SmallDictionary<TKey, TValue> dict)
            {
                _dict = dict;
            }

            /// <inheritdoc />
            public TValue Current
            {
                get
                {
                    if (_idx < 0 || _idx >= _dict.Capacity)
                    {
                        throw new IndexOutOfRangeException("Enumerated out of range");
                    }

                    return _dict._nodes[_idx].Value;
                }
            }

            /// <inheritdoc />
            object IEnumerator.Current
            {
                get
                {
                    if (_idx < 0 || _idx >= _dict.Capacity)
                    {
                        throw new IndexOutOfRangeException("Enumerated out of range");
                    }

                    return _dict._nodes[_idx].Value;
                }
            }

            /// <inheritdoc />
            public void Dispose()
            {
            }

            /// <inheritdoc />
            public bool MoveNext()
            {
                _idx++;
                return _idx < _dict.Capacity && !IsNullEntry(_dict._nodes[_idx]);
            }

            /// <inheritdoc />
            public void Reset()
            {
                _idx = -1;
            }
        }
    }
}