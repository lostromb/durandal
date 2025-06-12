using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// Implementation of Most-Frequently-Used memory cache based on the internal code of FastConcurrentDictionary / Counter.
    /// This implementation is thread safe.
    /// </summary>
    /// <typeparam name="KCache">The key used to fetch values from this cache</typeparam>
    /// <typeparam name="VCache">The type of values to fetch from this cache</typeparam>
    public class MFUCache<KCache, VCache> : IReadThroughCache<KCache, VCache>
    {
        // Recycle objects to reduce memory allocations
        private static readonly ConcurrentQueue<HashTableLinkedListNode<KCache, VCache>> RECLAIMED_NODES = new ConcurrentQueue<HashTableLinkedListNode<KCache, VCache>>();

        private const int NUM_LOCKS = 16;
        private const int MAX_TABLE_SIZE = 100000;
        private readonly Func<KCache, VCache> _valueProducer;
        private readonly Action<VCache> _disposer;
        private readonly MovingAverage _hitCountAverage;
        private readonly object[] _binLocks;
        private readonly HashTableLinkedListNode<KCache, VCache>[] _bins;

        private volatile int _numItemsInDictionary;
        private int _cacheCapacity;
        private uint _currentPruneThreshold = 0;

        /// <summary>
        /// Creates a most-frequently-used memory cache
        /// </summary>
        /// <param name="source">A function which produces the values to be cached</param>
        /// <param name="cacheCapacity">The maximum number of items to store in the cache</param>
        /// <param name="disposer">An optional action that is invoked on each item when it is removed from the cache</param>
        public MFUCache(Func<KCache, VCache> source, int cacheCapacity, Action<VCache> disposer = null)
        {
            if (cacheCapacity <= 0)
            {
                throw new ArgumentException("Cache capacity must be greater than 0");
            }

            _valueProducer = source;
            _cacheCapacity = cacheCapacity;
            _disposer = disposer;

            int numberOfBins = Math.Min(MAX_TABLE_SIZE, Math.Max(NUM_LOCKS, _cacheCapacity / 2));

            _binLocks = new object[NUM_LOCKS];
            for (int c = 0; c < NUM_LOCKS; c++)
            {
                _binLocks[c] = new object();
            }

            _numItemsInDictionary = 0;
            _bins = new HashTableLinkedListNode<KCache, VCache>[numberOfBins];
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

        public Task<VCache> GetCacheAsync(KCache key)
        {
            return Task.FromResult(GetCache(key));
        }

        public VCache GetCache(KCache key)
        {
            uint keyHash = (uint)key.GetHashCode();
            uint bin = keyHash % (uint)_bins.Length;
            uint binLock = bin % NUM_LOCKS;
            VCache returnVal = default(VCache);

            // Acquire the lock to the bin we want
            Monitor.Enter(_binLocks[binLock]);
            try
            {
                HashTableLinkedListNode<KCache, VCache> iter = _bins[bin];
                HashTableLinkedListNode<KCache, VCache> prevNode = null;
                HashTableLinkedListNode<KCache, VCache> next = null;
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

                        if (_disposer != null)
                        {
                            _disposer(iter.Kvp.Value);
                        }

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
                    returnVal = _valueProducer(key);
                    if (prevNode == null)
                    {
                        _bins[bin] = AllocateNode(key, returnVal, keyHash);
                    }
                    else
                    {
                        prevNode.Next = AllocateNode(key, returnVal, keyHash);
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

        private static HashTableLinkedListNode<KCache, VCache> AllocateNode(KCache key, VCache value, uint keyHash)
        {
            HashTableLinkedListNode<KCache, VCache> allocatedBin;
            if (!RECLAIMED_NODES.TryDequeue(out allocatedBin))
            {
                allocatedBin = new HashTableLinkedListNode<KCache, VCache>(new KeyValuePair<KCache, VCache>(key, value), keyHash);
            }
            else
            {
                allocatedBin.Kvp = new KeyValuePair<KCache, VCache>(key, value);
                allocatedBin.CachedHashValue = keyHash;
                allocatedBin.HitCount = 0;
            }

            return allocatedBin;
        }

        private static void DeallocateNode(HashTableLinkedListNode<KCache, VCache> node)
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
