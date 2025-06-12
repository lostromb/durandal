using Durandal.Common.Cache;
using Durandal.Common.MathExt;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Durandal.Common.Collections
{
    /// <summary>
    /// Implements a concurrent set with "loose" concurrency rules. Specifically,
    /// any operation (including enumeration) can be performed without external locking, however,
    /// results of conflicting operations are approximate. For example, if you access
    /// Count while an insertion is taking place, the count may not be fully accurate.
    /// </summary>
    /// <typeparam name="K">The type of items to be stored in this set.</typeparam>
    public class FastConcurrentHashSet<K> : IEnumerable<K>, IReadOnlySet<K>
    {
        // Recycle objects to reduce memory allocations
        private static readonly LockFreeCache<HashSetLinkedListNode<K>> RECLAIMED_NODES = new LockFreeCache<HashSetLinkedListNode<K>>(65536);

        private const int BIN_LOAD_RATIO = 3; // average number of entries per bin until we consider increasing the table size
        private const int DEFAULT_INITIAL_CAPACITY = 64;
        private const int NUM_LOCKS = 16;
        private const int MAX_TABLE_SIZE = 0x3FF0000; // A little less than half of int.MaxValue. Above this amount, we stop allocating new tables

        private readonly object[] _binLocks;
        private HashSetLinkedListNode<K>[] _bins;
        private volatile int _numItemsInSet;

        /// <summary>
        /// Creates a fast concurrent hashset with a default initial capacity
        /// </summary>
        public FastConcurrentHashSet() : this(DEFAULT_INITIAL_CAPACITY)
        {
        }

        /// <summary>
        /// Creates a fast concurrent hashset with the specified initial capacity
        /// </summary>
        /// <param name="initialCapacity"></param>
        public FastConcurrentHashSet(int initialCapacity)
        {
            if (initialCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            if (initialCapacity > MAX_TABLE_SIZE)
            {
                initialCapacity = MAX_TABLE_SIZE;
            }

            _binLocks = new object[NUM_LOCKS];
            for (int c = 0; c < NUM_LOCKS; c++)
            {
                _binLocks[c] = new object();
            }

            _numItemsInSet = 0;
            _bins = new HashSetLinkedListNode<K>[initialCapacity];
        }

        /// <summary>
        /// Returns the approximate count of the number of items in the dictionary.
        /// </summary>
        public int Count => _numItemsInSet;

        public bool IsReadOnly => false;

        /// <summary>
        /// Adds an item to the set.
        /// </summary>
        /// <param name="item">The item to add.</param>
        /// <returns>True if the item was not previously found in the set.</returns>
        public bool Add(K item)
        {
            ExpandTableIfNeeded();

            HashSetLinkedListNode<K>[] bins;
            uint bin;
            uint keyHash = (uint)item.GetHashCode();
            object binLock = AcquireLockToStableHashBin(keyHash, out bins, out bin);
            try
            {
                if (bins[bin] == null)
                {
                    // Bin is empty; fill bin with new entry
                    bins[bin] = AllocateNode(item, keyHash);
                    Interlocked.Increment(ref _numItemsInSet);
                    return true;
                }
                else
                {
                    // Bin is full; see if a value already exists
                    HashSetLinkedListNode<K> iter = bins[bin];
                    HashSetLinkedListNode<K> endOfBin = iter;
                    while (iter != null)
                    {
                        // Does an entry already exist with this same hash?
                        if (item.Equals(iter.Item))
                        {
                            // Nothing to do here.
                            return false;
                        }

                        iter = iter.Next;
                        if (iter != null)
                        {
                            endOfBin = iter;
                        }
                    }

                    // Key was not found after iterating the bin. Append a new entry to the end of the bin
                    endOfBin.Next = AllocateNode(item, keyHash);
                    Interlocked.Increment(ref _numItemsInSet);
                    return true;
                }
            }
            finally
            {
                Monitor.Exit(binLock);
            }
        }

        /// <summary>
        /// Safely clears all values from the set.
        /// </summary>
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
                    HashSetLinkedListNode<K> node = _bins[bin];
                    if (node != null)
                    {
                        DeallocateNode(node);
                    }

                    _bins[bin] = null;
                }

                _numItemsInSet = 0;
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

        /// <summary>
        /// Returns true if the set contains the specified item
        /// </summary>
        /// <param name="item">The item to check for.</param>
        /// <returns>True if the specified item is in the dictionary.</returns>
        public bool Contains(K item)
        {
            HashSetLinkedListNode<K>[] bins;
            uint bin;
            uint keyHash = (uint)item.GetHashCode();
            object binLock = AcquireLockToStableHashBin(keyHash, out bins, out bin);
            try
            {
                if (bins[bin] == null)
                {
                    // Bin is empty.
                    return false;
                }
                else
                {
                    // Bin is full; see if a value already exists
                    HashSetLinkedListNode<K> iter = bins[bin];
                    while (iter != null)
                    {
                        if (item.Equals(iter.Item))
                        {
                            // Found it!
                            return true;
                        }

                        iter = iter.Next;
                    }

                    return false;
                }
            }
            finally
            {
                Monitor.Exit(binLock);
            }
        }

        /// <summary>
        /// Copies all key/values from this set to another collection.
        /// The copy may be incomplete if the set is being concurrently modified.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(K[] array, int arrayIndex)
        {
            // Enumerate and copy
            using (IEnumerator<K> enumerator = GetEnumerator())
            {
                int idx = 0;
                while (enumerator.MoveNext())
                {
                    array[arrayIndex + idx] = enumerator.Current;
                    idx++;
                }
            }
        }

        /// <summary>
        /// Enumerates the items in this set.
        /// The only guarantee provided by enumeration while other threads are modifying the collection are:
        /// <list type="number">
        /// <item>The enumerator will not throw an exception</item>
        /// <item>The enumerator will not enumerate the same item more than once</item>
        /// </list>
        /// If no concurrent access is going on then you can make the same assumptions as with a regular set.
        /// </summary>
        /// <returns>An enumerator for this set</returns>
        public IEnumerator<K> GetEnumerator()
        {
            return new FastConcurrentSetEnumerator(this);
        }

        /// <summary>
        /// Returns an enumerator that begins in a random point of the set and enumerates all values.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<K> GetRandomEnumerator(IRandom rand)
        {
            return new FastConcurrentSetEnumerator(this, rand);
        }

        /// <summary>
        /// Gets a special ref struct which will enumerate all values in this set concurrently without allocating an <see cref="IEnumerator"/>.
        /// </summary>
        /// <returns>A value enumerator over this set.</returns>
        public FastConcurrentSetValueEnumerator GetValueEnumerator()
        {
            return new FastConcurrentSetValueEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new FastConcurrentSetEnumerator(this);
        }

        /// <summary>
        /// Attempts to remove the specified item from the set.
        /// </summary>
        /// <param name="item">The item to try and remove.</param>
        /// <returns>True if a value was removed.</returns>
        public bool Remove(K item)
        {
            HashSetLinkedListNode<K>[] bins;
            uint bin;
            uint keyHash = (uint)item.GetHashCode();
            object binLock = AcquireLockToStableHashBin(keyHash, out bins, out bin);
            try
            {
                if (bins[bin] == null)
                {
                    // Bin is empty.
                    return false;
                }
                else
                {
                    // Bin is full; see if a value already exists
                    HashSetLinkedListNode<K> iter = bins[bin];
                    HashSetLinkedListNode<K> prevNode = null;
                    while (iter != null)
                    {
                        if (item.Equals(iter.Item))
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

                            DeallocateNode(iter);
                            Interlocked.Decrement(ref _numItemsInSet);
                            return true;
                        }

                        prevNode = iter;
                        iter = iter.Next;
                    }

                    return false;
                }
            }
            finally
            {
                Monitor.Exit(binLock);
            }
        }
        
        private void ExpandTableIfNeeded()
        {
            if (_numItemsInSet < _bins.Length * BIN_LOAD_RATIO ||
                _numItemsInSet >= MAX_TABLE_SIZE)
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
                uint moduloFactor = (uint)_bins.Length;
                uint newTableLength = moduloFactor * 2;
                HashSetLinkedListNode<K>[] newTable = new HashSetLinkedListNode<K>[newTableLength];

                // Since we expand the table by exactly double each time, we can take advantage of modulo artihmetic
                // and the fact that each bin on the old table corresponds to at most 2 bins on the new table.
                // So all we have to do is iterate through each source bin and "unzip" it into two new bins.
                // This prevents having to reallocate all of the nodes again, but we have to be careful about dangling pointers.
                foreach (HashSetLinkedListNode<K> sourceBin in _bins)
                {
                    HashSetLinkedListNode<K> sourceIter = sourceBin;
                    HashSetLinkedListNode<K> sourceIterNext;
                    HashSetLinkedListNode<K> targetLow = null;
                    HashSetLinkedListNode<K> targetHigh = null;

                    while (sourceIter != null)
                    {
                        sourceIterNext = sourceIter.Next;
                        sourceIter.Next = null;

                        uint keyHash = sourceIter.CachedHashValue;
                        uint targetBin = keyHash % newTableLength;
                        if (targetBin < moduloFactor)
                        {
                            // Sort to low bin
                            if (targetLow == null)
                            {
                                // Bin is empty
                                newTable[targetBin] = sourceIter;
                            }
                            else
                            {
                                targetLow.Next = sourceIter;
                            }

                            targetLow = sourceIter;
                        }
                        else
                        {
                            // Sort to high bin
                            if (targetHigh == null)
                            {
                                // Bin is empty
                                newTable[targetBin] = sourceIter;
                            }
                            else
                            {
                                targetHigh.Next = sourceIter;
                            }

                            targetHigh = sourceIter;
                        }

                        sourceIter = sourceIterNext;
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

        /// <summary>
        /// 1. Figures out which bin lock to acquire
        /// 2. Detects concurrent modification of the table and resolves automatically
        /// 3. Acquires the bin lock
        /// 4. Returns the held lock object, the stable bin array, and bin index for the given key hash.
        /// </summary>
        /// <param name="keyHash">The key hash to look up.</param>
        /// <param name="hashTable">(out) The stable array of bins that was can safely reference</param>
        /// <param name="binIndex">(out) The index of the bin in the hash table that corresponds to the key hash.</param>
        /// <returns>An object representing the held mutex for the hash table bin we want.</returns>
        private object AcquireLockToStableHashBin(uint keyHash, out HashSetLinkedListNode<K>[] hashTable, out uint binIndex)
        {
            // Copy a local reference of the table in case another thread tries to resize it while we are accessing
            hashTable = _bins;
            binIndex = keyHash % (uint)hashTable.Length;
            uint binLock = binIndex % NUM_LOCKS;

            // Acquire the lock to the bin we want
            Monitor.Enter(_binLocks[binLock]);

            // Detect if the table was resized while we were getting the lock
            while (_bins != hashTable)
            {
                // If so, we need to recalculate the bin # based on the new table size and then try getting the lock again
                Monitor.Exit(_binLocks[binLock]);
                hashTable = _bins;
                binIndex = keyHash % (uint)hashTable.Length;
                binLock = binIndex % NUM_LOCKS;
                Monitor.Enter(_binLocks[binLock]);
            }

            return _binLocks[binLock];
        }

        private static HashSetLinkedListNode<K> AllocateNode(K item, uint keyHash)
        {
            HashSetLinkedListNode<K> allocatedBin = RECLAIMED_NODES.TryDequeue();
            if (allocatedBin == null)
            {
                allocatedBin = new HashSetLinkedListNode<K>(item, keyHash);
            }
            else
            {
                allocatedBin.Item = item;
                allocatedBin.CachedHashValue = keyHash;
            }

            return allocatedBin;
        }

        private static void DeallocateNode(HashSetLinkedListNode<K> node)
        {
            node.Next = null;
            node.Item = default(K);
            RECLAIMED_NODES.TryEnqueue(node);
        }

        private class FastConcurrentSetEnumerator : IEnumerator<K>
        {
            private readonly HashSetLinkedListNode<K>[] _localTableReference;
            private readonly FastConcurrentHashSet<K> _owner;
            private readonly object[] _binLocks;
            private bool _finished;
            private uint _currentBinIdx;
            private uint _currentBinListIdx;
            private uint _beginOffset;
            private K _current;

            public FastConcurrentSetEnumerator(FastConcurrentHashSet<K> owner)
            {
                _owner = owner;
                _localTableReference = _owner._bins;
                _binLocks = _owner._binLocks;
                Reset();
                _beginOffset = 0;
            }

            public FastConcurrentSetEnumerator(FastConcurrentHashSet<K> owner, IRandom rand)
            {
                _owner = owner;
                _localTableReference = _owner._bins;
                _binLocks = _owner._binLocks;
                Reset();
                _beginOffset = (uint)rand.NextInt(0, _localTableReference.Length);
            }

            public K Current => _current;

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
                    uint currentBinIdxWithOffset = (_currentBinIdx + _beginOffset) % (uint)_localTableReference.Length;

                    // Get the bin lock
                    uint binLock = currentBinIdxWithOffset % NUM_LOCKS;

                    // Acquire the lock to the bin we want
                    Monitor.Enter(_binLocks[binLock]);
                    try
                    {
                        // Was the table resized during enumeration?
                        if (_owner._bins != _localTableReference)
                        {
                            // If so, just abort enumeration
                            _finished = true;
                            _current = default(K);
                            return false;
                        }

                        HashSetLinkedListNode<K> iter = _localTableReference[currentBinIdxWithOffset];
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
                                _current = iter.Item;
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
                _current = default(K);
                return false;
            }

            public void Reset()
            {
                _currentBinIdx = 0;
                _currentBinListIdx = 0;
                _current = default(K);
                _finished = false;
            }
        }

        public ref struct FastConcurrentSetValueEnumerator
        {
            private readonly HashSetLinkedListNode<K>[] _localTableReference;
            private readonly FastConcurrentHashSet<K> _owner;
            private readonly object[] _binLocks;
            private bool _finished;
            private uint _currentBinIdx;
            private uint _currentBinListIdx;
            private uint _beginOffset;
            private K _current;

            public FastConcurrentSetValueEnumerator(FastConcurrentHashSet<K> owner)
            {
                _owner = owner;
                _localTableReference = _owner._bins;
                _binLocks = _owner._binLocks;
                _currentBinIdx = 0;
                _currentBinListIdx = 0;
                _current = default(K);
                _finished = false;
                _beginOffset = 0;
            }

            public FastConcurrentSetValueEnumerator(FastConcurrentHashSet<K> owner, IRandom rand)
            {
                _owner = owner;
                _localTableReference = _owner._bins;
                _binLocks = _owner._binLocks;
                _currentBinIdx = 0;
                _currentBinListIdx = 0;
                _current = default(K);
                _finished = false;
                _beginOffset = (uint)rand.NextInt(0, _localTableReference.Length);
            }

            public K Current => _current;

            public bool MoveNext()
            {
                if (_finished)
                {
                    return false;
                }

                while (_currentBinIdx < _localTableReference.Length)
                {
                    uint currentBinIdxWithOffset = (_currentBinIdx + _beginOffset) % (uint)_localTableReference.Length;

                    // Get the bin lock
                    uint binLock = currentBinIdxWithOffset % NUM_LOCKS;

                    // Acquire the lock to the bin we want
                    Monitor.Enter(_binLocks[binLock]);
                    try
                    {
                        // Was the table resized during enumeration?
                        if (_owner._bins != _localTableReference)
                        {
                            // If so, just abort enumeration
                            _finished = true;
                            _current = default(K);
                            return false;
                        }

                        HashSetLinkedListNode<K> iter = _localTableReference[currentBinIdxWithOffset];
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
                                _current = iter.Item;
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
                _current = default(K);
                return false;
            }

            public void Reset()
            {
                _currentBinIdx = 0;
                _currentBinListIdx = 0;
                _current = default(K);
                _finished = false;
            }
        }

        private class HashSetLinkedListNode<KNode>
        {
            public HashSetLinkedListNode(KNode item, int keyHash)
            {
                Item = item;
                CachedHashValue = unchecked((uint)keyHash);
            }

            public HashSetLinkedListNode(KNode item, uint keyHash)
            {
                Item = item;
                CachedHashValue = keyHash;
            }

            public uint CachedHashValue;
            public KNode Item;
            public HashSetLinkedListNode<KNode> Next;
        }
    }
}
