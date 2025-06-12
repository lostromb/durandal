using Durandal.Common.Collections;
using Durandal.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// Implements a thread-safe counter for counting keys or features.
    /// </summary>
    /// <typeparam name="T">The types of objects to be counted.</typeparam>
    public class Counter<T> : IEnumerable<KeyValuePair<T, float>>
    {
        private const int NUM_LOCKS = 20;

        private readonly object[] _binLocks;
        private readonly IEqualityComparer<T> _keyComparer;
        private CounterLinkedListNode[] _bins;
        private volatile int _numItemsInDictionary;

        public Counter(int initialCapacity = 32) : this(EqualityComparer<T>.Default, initialCapacity)
        {
        }

        public Counter(IEqualityComparer<T> comparer, int initialCapacity = 32)
        {
            if (initialCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            // Don't allow us to have fewer bins than we have locks - the allocation is so small as to be ineffecient
            if (initialCapacity < NUM_LOCKS)
            {
                initialCapacity = NUM_LOCKS;
            }

            _binLocks = new object[NUM_LOCKS];
            for (int c = 0; c < NUM_LOCKS; c++)
            {
                _binLocks[c] = new object();
            }

            _numItemsInDictionary = 0;
            _bins = new CounterLinkedListNode[initialCapacity];
            _keyComparer = comparer.AssertNonNull(nameof(comparer));
        }

        public int NumItems => _numItemsInDictionary;

        public float Increment(T key)
        {
            return Increment(key, 1f);
        }

        public float Decrement(T key)
        {
            return Increment(key, -1f);
        }

        public float Increment(T key, float count)
        {
            ExpandTableIfNeeded();

            // Copy a local reference of the table in case another thread tries to resize it while we are accessing
            CounterLinkedListNode[] bins = _bins;
            uint keyHash = (uint)_keyComparer.GetHashCode(key);
            uint bin = keyHash % (uint)bins.Length;
            uint binLock = bin % NUM_LOCKS;

            // Acquire the lock to the bin we want
            Monitor.Enter(_binLocks[binLock]);

            // Detect if the table was resized while we were getting the lock
            while (_bins != bins)
            {
                // If so, we need to recalculate the bin # based on the new table size and then try getting the lock again
                Monitor.Exit(_binLocks[binLock]);
                bins = _bins;
                bin = keyHash % (uint)bins.Length;
                binLock = bin % NUM_LOCKS;
                Monitor.Enter(_binLocks[binLock]);
            }

            try
            {
                if (bins[bin] == null)
                {
                    // Bin is empty; fill bin with new entry
                    bins[bin] = new CounterLinkedListNode()
                    {
                        Key = key,
                        Count = count,
                        Next = null
                    };

                    Interlocked.Increment(ref _numItemsInDictionary);
                    return count;
                }
                else
                {
                    // Bin is full; see if a value already exists
                    CounterLinkedListNode iter = bins[bin];
                    CounterLinkedListNode endOfBin = iter;
                    while (iter != null)
                    {
                        // Does an entry already exist with this same key?
                        if (_keyComparer.Equals(key, iter.Key))
                        {
                            // Update the value of the existing item
                            iter.Count = iter.Count + count;
                            if (float.IsNaN(iter.Count) || float.IsInfinity(iter.Count))
                            {
                                iter.Count = float.MaxValue;
                            }

                            return iter.Count;
                        }

                        iter = iter.Next;
                        if (iter != null)
                        {
                            endOfBin = iter;
                        }
                    }

                    // Key was not found after iterating the bin. Append a new entry to the end of the bin
                    endOfBin.Next = new CounterLinkedListNode()
                    {
                        Key = key,
                        Count = count,
                        Next = null
                    };

                    Interlocked.Increment(ref _numItemsInDictionary);
                    return count;
                }
            }
            finally
            {
                Monitor.Exit(_binLocks[binLock]);
            }
        }
        
        public void Set(T item, float value)
        {
            ExpandTableIfNeeded();

            // Copy a local reference of the table in case another thread tries to resize it while we are accessing
            CounterLinkedListNode[] bins = _bins;
            uint keyHash = (uint)_keyComparer.GetHashCode(item);
            uint bin = keyHash % (uint)bins.Length;
            uint binLock = bin % NUM_LOCKS;

            // Acquire the lock to the bin we want
            Monitor.Enter(_binLocks[binLock]);

            // Detect if the table was resized while we were getting the lock
            while (_bins != bins)
            {
                // If so, we need to recalculate the bin # based on the new table size and then try getting the lock again
                Monitor.Exit(_binLocks[binLock]);
                bins = _bins;
                bin = keyHash % (uint)bins.Length;
                binLock = bin % NUM_LOCKS;
                Monitor.Enter(_binLocks[binLock]);
            }

            try
            {
                if (bins[bin] == null)
                {
                    // Bin is empty; fill bin with new entry
                    bins[bin] = new CounterLinkedListNode()
                    {
                        Key = item,
                        Count = value,
                        Next = null
                    };

                    Interlocked.Increment(ref _numItemsInDictionary);
                }
                else
                {
                    // Bin is full; see if a value already exists
                    CounterLinkedListNode iter = bins[bin];
                    CounterLinkedListNode endOfBin = iter;
                    bool foundValue = false;
                    while (iter != null)
                    {
                        // Does an entry already exist with this same key?
                        if (_keyComparer.Equals(item, iter.Key))
                        {
                            // Update the value of the existing item
                            iter.Count = value;
                            foundValue = true;
                            break;
                        }

                        iter = iter.Next;
                        if (iter != null)
                        {
                            endOfBin = iter;
                        }
                    }

                    if (!foundValue)
                    {
                        // Key was not found after iterating the bin. Append a new entry to the end of the bin
                        endOfBin.Next = new CounterLinkedListNode()
                        {
                            Key = item,
                            Count = value,
                            Next = null
                        };

                        Interlocked.Increment(ref _numItemsInDictionary);
                    }
                }
            }
            finally
            {
                Monitor.Exit(_binLocks[binLock]);
            }
        }

        public void Remove(T item)
        {
            // Copy a local reference of the table in case another thread tries to resize it while we are accessing
            CounterLinkedListNode[] bins = _bins;
            uint keyHash = (uint)_keyComparer.GetHashCode(item);
            uint bin = keyHash % (uint)bins.Length;
            uint binLock = bin % NUM_LOCKS;

            // Acquire the lock to the bin we want
            Monitor.Enter(_binLocks[binLock]);

            // Detect if the table was resized while we were getting the lock
            while (_bins != bins)
            {
                // If so, we need to recalculate the bin # based on the new table size and then try getting the lock again
                Monitor.Exit(_binLocks[binLock]);
                bins = _bins;
                bin = keyHash % (uint)bins.Length;
                binLock = bin % NUM_LOCKS;
                Monitor.Enter(_binLocks[binLock]);
            }

            try
            {
                if (bins[bin] != null)
                {
                    // Bin is full; see if a value already exists
                    CounterLinkedListNode iter = bins[bin];
                    CounterLinkedListNode prevNode = null;
                    while (iter != null)
                    {
                        if (item.Equals(iter.Key))
                        {
                            // Found it! Update the linked list
                            if (prevNode == null)
                            {
                                bins[bin] = iter.Next;
                            }
                            else
                            {
                                prevNode.Next = iter.Next;
                            }

                            iter.Next = null;
                            Interlocked.Decrement(ref _numItemsInDictionary);
                            return;
                        }

                        prevNode = iter;
                        iter = iter.Next;
                    }
                }
            }
            finally
            {
                Monitor.Exit(_binLocks[binLock]);
            }
        }

        public float GetCount(T key)
        {
            // Copy a local reference of the table in case another thread tries to resize it while we are accessing
            CounterLinkedListNode[] bins = _bins;
            uint keyHash = (uint)_keyComparer.GetHashCode(key);
            uint bin = keyHash % (uint)bins.Length;
            uint binLock = bin % NUM_LOCKS;

            // Acquire the lock to the bin we want
            Monitor.Enter(_binLocks[binLock]);

            // Detect if the table was resized while we were getting the lock
            while (_bins != bins)
            {
                // If so, we need to recalculate the bin # based on the new table size and then try getting the lock again
                Monitor.Exit(_binLocks[binLock]);
                bins = _bins;
                bin = keyHash % (uint)bins.Length;
                binLock = bin % NUM_LOCKS;
                Monitor.Enter(_binLocks[binLock]);
            }

            try
            {
                if (bins[bin] == null)
                {
                    return 0;
                }
                else
                {
                    // Bin is full; see if a value already exists
                    CounterLinkedListNode iter = bins[bin];
                    while (iter != null)
                    {
                        if (key.Equals(iter.Key))
                        {
                            // Found it!
                            return iter.Count;
                        }

                        iter = iter.Next;
                    }

                    return 0;
                }
            }
            finally
            {
                Monitor.Exit(_binLocks[binLock]);
            }
        }

        /// <summary>
        /// Normalizes all counts so that they sum to 1.0. Though thread-safe, this operation should only be done after the counter is finished counting.
        /// </summary>
        public void Normalize()
        {
            // Acquire all locks
            for (int binLock = 0; binLock < NUM_LOCKS; binLock++)
            {
                Monitor.Enter(_binLocks[binLock]);
            }

            try
            {
                // Summation pass
                float sum = 0;
                foreach (CounterLinkedListNode binHead in _bins)
                {
                    CounterLinkedListNode iter = binHead;
                    while (iter != null)
                    {
                        sum += iter.Count;
                        iter = iter.Next;
                    }
                }

                // Normalize pass
                if (sum != 0 && !float.IsInfinity(sum) && !float.IsNaN(sum))
                {
                    foreach (CounterLinkedListNode binHead in _bins)
                    {
                        CounterLinkedListNode iter = binHead;
                        while (iter != null)
                        {
                            iter.Count = iter.Count / sum;
                            iter = iter.Next;
                        }
                    }
                }
            }
            finally
            {
                // Release all locks
                for (int binLock = 0; binLock < NUM_LOCKS; binLock++)
                {
                    Monitor.Exit(_binLocks[binLock]);
                }
            }
        }

        public void Clear()
        {
            // Acquire all locks
            for (int binLock = 0; binLock < NUM_LOCKS; binLock++)
            {
                Monitor.Enter(_binLocks[binLock]);
            }

            try
            {
                // Iterate through all bins and clean them
                for (int bin = 0; bin < _bins.Length; bin++)
                {
                    _bins[bin] = null;
                }

                _numItemsInDictionary = 0;
            }
            finally
            {
                // Release all locks
                for (int binLock = 0; binLock < NUM_LOCKS; binLock++)
                {
                    Monitor.Exit(_binLocks[binLock]);
                }
            }
        }

        private void ExpandTableIfNeeded()
        {
            if (_numItemsInDictionary < _bins.Length)
            {
                return;
            }

            // Acquire all bin locks in order
            for (int binLock = 0; binLock < NUM_LOCKS; binLock++)
            {
                Monitor.Enter(_binLocks[binLock]);
            }

            try
            {
                // Create a new table and copy all existing values to it
                CounterLinkedListNode[] newTable = new CounterLinkedListNode[_bins.Length * 2];

                foreach (CounterLinkedListNode sourceBin in _bins)
                {
                    CounterLinkedListNode sourceIter = sourceBin;
                    while (sourceIter != null)
                    {
                        uint keyHash = (uint)_keyComparer.GetHashCode(sourceIter.Key);
                        uint targetBin = keyHash % (uint)newTable.Length;

                        if (newTable[targetBin] == null)
                        {
                            // Bin is empty; fill bin with new entry
                            newTable[targetBin] = new CounterLinkedListNode()
                            {
                                Key = sourceIter.Key,
                                Count = sourceIter.Count,
                                Next = null
                            };
                        }
                        else
                        {
                            // Bin has entries; append to the end of the list
                            CounterLinkedListNode targetIter = newTable[targetBin];
                            CounterLinkedListNode targetEndOfBin = targetIter;
                            while (targetIter != null)
                            {
                                targetIter = targetIter.Next;
                                if (targetIter != null)
                                {
                                    targetEndOfBin = targetIter;
                                }
                            }

                            // Key was not found after iterating the bin. Append a new entry to the end of the bin
                            targetEndOfBin.Next = new CounterLinkedListNode()
                            {
                                Key = sourceIter.Key,
                                Count = sourceIter.Count,
                                Next = null
                            };
                        }

                        sourceIter = sourceIter.Next;
                    }
                }

                _bins = newTable;
            }
            finally
            {
                // Release all locks
                for (int binLock = 0; binLock < NUM_LOCKS; binLock++)
                {
                    Monitor.Exit(_binLocks[binLock]);
                }
            }
        }

        public IEnumerator<KeyValuePair<T, float>> GetEnumerator()
        {
            return new CounterEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new CounterEnumerator(this);
        }

        private class CounterEnumerator : IEnumerator<KeyValuePair<T, float>>
        {
            private readonly CounterLinkedListNode[] _localTableReference;
            private readonly Counter<T> _owner;
            private readonly object[] _binLocks;
            private bool _finished;
            private uint _currentBinIdx;
            private uint _currentBinListIdx;
            private KeyValuePair<T, float> _current;

            public CounterEnumerator(Counter<T> owner)
            {
                _owner = owner;
                _localTableReference = _owner._bins;
                _binLocks = _owner._binLocks;
                Reset();
            }

            public KeyValuePair<T, float> Current => _current;

            object IEnumerator.Current => _current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (_finished)
                {
                    return false;
                }

                while (_currentBinIdx < _localTableReference.Length)
                {
                    // Get the bin lock
                    uint binLock = _currentBinIdx % NUM_LOCKS;

                    // Acquire the lock to the bin we want
                    Monitor.Enter(_binLocks[binLock]);
                    try
                    {
                        // Was the table resized during enumeration?
                        if (_owner._bins != _localTableReference)
                        {
                            // If so, just abort enumeration
                            _finished = true;
                            _current = default(KeyValuePair<T, float>);
                            return false;
                        }

                        CounterLinkedListNode iter = _localTableReference[_currentBinIdx];
                        if (iter == null)
                        {
                            // Skip over empty bins
                            _currentBinIdx++;
                            _currentBinListIdx = 0;
                        }
                        else
                        {
                            uint iterIdx = 0;

                            // Iterate within a single bin
                            while (iter != null &&
                                iterIdx < _currentBinListIdx)
                            {
                                iter = iter.Next;
                                iterIdx++;
                            }

                            if (iter == null)
                            {
                                // Finished iterating this bin. Move on
                                _currentBinIdx++;
                                _currentBinListIdx = 0;
                            }
                            else
                            {
                                _current = new KeyValuePair<T, float>(iter.Key, iter.Count);
                                _currentBinListIdx = iterIdx + 1;
                                return true;
                            }
                        }
                    }
                    finally
                    {
                        Monitor.Exit(_binLocks[binLock]);
                    }
                }

                // Reached end of table
                _finished = true;
                _current = default(KeyValuePair<T, float>);
                return false;
            }

            public void Reset()
            {
                _currentBinIdx = 0;
                _currentBinListIdx = 0;
                _current = default(KeyValuePair<T, float>);
                _finished = false;
            }
        }

        private class CounterLinkedListNode
        {
            public T Key;
            public float Count;
            public CounterLinkedListNode Next;
        }
    }
}
