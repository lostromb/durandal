using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// Implementation of Most-Frequently-Used memory cache which can choose to either allocate new strings from
    /// byte array segments, or return previously allocated strings from the same segment data.
    /// </summary>
    public class MFUStringCache : IReadThroughCache<ByteArraySegment, string>
    {
        // Recycle objects to reduce memory allocations
        private static readonly ConcurrentQueue<HashTableLinkedListNode<ByteArraySegment, string>> RECLAIMED_NODES =
            new ConcurrentQueue<HashTableLinkedListNode<ByteArraySegment, string>>();

        private const int NUM_LOCKS = 16;
        private const int MAX_TABLE_SIZE = 100000;
        private readonly MovingAverage _hitCountAverage;
        private readonly object[] _binLocks;
        private readonly HashTableLinkedListNode<ByteArraySegment, string>[] _bins;
        private readonly Encoding _sourceEncoding;

        private volatile int _numItemsInDictionary;
        private int _cacheCapacity;
        private uint _currentPruneThreshold = 0;

        /// <summary>
        /// Creates a most-frequently-used memory cache for strings
        /// </summary>
        /// <param name="sourceEncoding">The encoding to use when interpreting byte segments</param>
        /// <param name="cacheCapacity">The maximum number of strings to store in the cache</param>
        public MFUStringCache(Encoding sourceEncoding, int cacheCapacity)
        {
            if (cacheCapacity <= 0)
            {
                throw new ArgumentException("Cache capacity must be greater than 0");
            }

            _sourceEncoding = sourceEncoding.AssertNonNull(nameof(sourceEncoding));
            _cacheCapacity = cacheCapacity;

            int numberOfBins = Math.Min(MAX_TABLE_SIZE, Math.Max(NUM_LOCKS, _cacheCapacity / 2));

            _binLocks = new object[NUM_LOCKS];
            for (int c = 0; c < NUM_LOCKS; c++)
            {
                _binLocks[c] = new object();
            }

            _numItemsInDictionary = 0;
            _bins = new HashTableLinkedListNode<ByteArraySegment, string>[numberOfBins];
            _hitCountAverage = new MovingAverage(50, 0.0);
        }

        public int CacheCapacity
        {
            get
            {
                return _cacheCapacity;
            }
        }

        public int ItemsCached
        {
            get
            {
                return _numItemsInDictionary;
            }
        }

        public Task<string> GetCacheAsync(ByteArraySegment key)
        {
            return Task.FromResult(GetCache(key));
        }

        public string GetCache(ByteArraySegment key)
        {
            uint keyHash = (uint)key.GetHashCode();
            uint bin = keyHash % (uint)_bins.Length;
            uint binLock = bin % NUM_LOCKS;
            string returnVal = default(string);

            // Acquire the lock to the bin we want
            Monitor.Enter(_binLocks[binLock]);
            try
            {
                HashTableLinkedListNode<ByteArraySegment, string> iter = _bins[bin];
                HashTableLinkedListNode<ByteArraySegment, string> prevNode = null;
                HashTableLinkedListNode<ByteArraySegment, string> next = null;
                bool found = false;
                bool doPruning = _numItemsInDictionary > _cacheCapacity;

                // Iterate through bins to find the key
                // While we're iterating here and hold the bin lock, might as well prune old entries
                while (iter != null)
                {
                    if (key.Equals(iter.Kvp.Key))
                    {
                        // Found it!
                        iter.HitCount++;
                        _hitCountAverage.Add(iter.HitCount);
                        returnVal = iter.Kvp.Value;
                        if (!doPruning)
                        {
                            return returnVal;
                        }

                        found = true;
                        prevNode = iter;
                        iter = iter.Next;
                    }
                    else if (doPruning && iter.HitCount <= _currentPruneThreshold)
                    {
                        // Prune old entries in the same bin while we're here
                        if (prevNode == null)
                        {
                            _bins[bin] = iter.Next;
                        }
                        else
                        {
                            prevNode.Next = iter.Next;
                        }

                        next = iter.Next;
                        iter.Next = null;

                        DeallocateNode(iter);
                        iter = next;
                        Interlocked.Decrement(ref _numItemsInDictionary);
                    }
                    else
                    {
                        _hitCountAverage.Add(iter.HitCount);
                        prevNode = iter;
                        iter = iter.Next;
                    }
                }

                if (!found)
                {
                    // Key was not found after iterating the bin, or the bin is empty (cache miss). Append a new entry to the end of the bin
                    // Need to create a new array when storing the key because we could have been passed a pointer to a transient array
                    // and we can't have the underlying data change on us.
                    byte[] persistentKeyData = new byte[key.Count];
                    ArrayExtensions.MemCopy(key.Array, key.Offset, persistentKeyData, 0, key.Count);
                    ByteArraySegment persistentKey = new ByteArraySegment(persistentKeyData, 0, key.Count);
                    returnVal = _sourceEncoding.GetString(persistentKeyData, 0, key.Count);
                    if (prevNode == null)
                    {
                        _bins[bin] = AllocateNode(persistentKey, returnVal, keyHash);
                    }
                    else
                    {
                        prevNode.Next = AllocateNode(persistentKey, returnVal, keyHash);
                    }

                    Interlocked.Increment(ref _numItemsInDictionary);
                    _currentPruneThreshold = (uint)_hitCountAverage.Average;
                }

                return returnVal;
            }
            finally
            {
                Monitor.Exit(_binLocks[binLock]);
            }
        }

        public void Dispose() { }

        private static HashTableLinkedListNode<ByteArraySegment, string> AllocateNode(ByteArraySegment key, string value, uint keyHash)
        {
            HashTableLinkedListNode<ByteArraySegment, string> allocatedBin;
            if (!RECLAIMED_NODES.TryDequeue(out allocatedBin))
            {
                allocatedBin = new HashTableLinkedListNode<ByteArraySegment, string>(new KeyValuePair<ByteArraySegment, string>(key, value), keyHash);
            }
            else
            {
                allocatedBin.Kvp = new KeyValuePair<ByteArraySegment, string>(key, value);
                allocatedBin.CachedHashValue = keyHash;
                allocatedBin.HitCount = 0;
            }

            return allocatedBin;
        }

        private static void DeallocateNode(HashTableLinkedListNode<ByteArraySegment, string> node)
        {
            RECLAIMED_NODES.Enqueue(node);
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
                _hitCountAverage.Average = 0;
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

        private class HashTableLinkedListNode<KNode, VNode>
        {
            public HashTableLinkedListNode(KeyValuePair<KNode, VNode> keyValuePair, int keyHash)
            {
                Kvp = keyValuePair;
                CachedHashValue = unchecked((uint)keyHash);
            }

            public HashTableLinkedListNode(KeyValuePair<KNode, VNode> keyValuePair, uint keyHash)
            {
                Kvp = keyValuePair;
                CachedHashValue = keyHash;
            }

            public uint CachedHashValue;
            public uint HitCount;
            public KeyValuePair<KNode, VNode> Kvp;
            public HashTableLinkedListNode<KNode, VNode> Next;
        }
    }
}
