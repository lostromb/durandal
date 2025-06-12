
namespace Durandal.Common.Collections
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Basic lock-free implementation of a concurrent FIFO queue.
    /// Manually implemented in PCL library, otherwise this just aliases <see cref="System.Collections.Concurrent.ConcurrentQueue{T}"/>.
    /// </summary>
    public class ConcurrentQueue<TQueue> : IEnumerable<TQueue>
    {
#if PCL
        //private readonly EqualityComparer<TQueue> _comparer;

        // Head and tail node must always be non-null because it reduces
        // the number of edge cases. So an empty list will be
        // a single Dequeued=1 node that is both head and tail.
        private QueueNode<TQueue> _head;
        private QueueNode<TQueue> _tail;
        private int _approximateCount = 0;

        public ConcurrentQueue()
        {
            //_comparer = EqualityComparer<TQueue>.Default;
            _head = new QueueNode<TQueue>();
            _head.Dequeued = 1;
            _tail = _head;
        }

        /// <summary>
        /// The approximate number of items that are in the queue.
        /// </summary>
        public int ApproximateCount
        {
            get
            {
                return _approximateCount;
            }
        }

        /// <summary>
        /// Indicates whether this queue is probably empty.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return _head == _tail;
            }
        }

        /// <summary>
        /// Pushes an item onto the queue.
        /// </summary>
        /// <param name="item"></param>
        public void Enqueue(TQueue item)
        {
            // I tried to do a design where we could reclaim nodes and avoid allocation, but it doesn't quite work in a
            // concurrent environment because we can't guarantee when a node is completely unreferenced.
            QueueNode<TQueue> newNode = new QueueNode<TQueue>();

            newNode.Item = item;
            QueueNode<TQueue> actualTail = _tail;

            // Start at the tail, and keep attempting to append
            // until we are sure we replaced the real null value at the actual tail
            while (actualTail != null)
            {
                actualTail = Interlocked.CompareExchange(ref _tail.Next, newNode, null);
            }

            _tail = newNode;
            Interlocked.Increment(ref _approximateCount);
        }

        /// <summary>
        /// Attempts to remove an item from the queue.
        /// </summary>
        /// <param name="returnVal">The place for the dequeued value, if found</param>
        /// <returns>True if a value was removed from the queue</returns>
        public bool TryDequeue(out TQueue returnVal)
        {
            // We know head must already have dequeued = 1 so start at head.Next
            QueueNode<TQueue> dequeuedNode = _head.Next;
            while (dequeuedNode != null)
            {
                // Try and set the dequeued flag on the next node in the list
                if (Interlocked.CompareExchange(ref dequeuedNode.Dequeued, 1, 0) == 0)
                {
                    // If another thread tries to run this code at the same time,
                    // the interlocked Dequeued flag ensures that every node at
                    // or before dequeuedNode in the list has been dequeued,
                    // so we can safely update head non-atomically here
                    Interlocked.Decrement(ref _approximateCount);
                    _head = dequeuedNode;
                    returnVal = dequeuedNode.Item;
                    return true;
                }
                else
                {
                    // incrementally updating head is not strictly necessary but
                    // could potentially affect performance when dequeueing in parallel
                    // _head = dequeuedNode;
                    dequeuedNode = dequeuedNode.Next;
                }
            }

            returnVal = default(TQueue);
            return false;
        }

        /// <summary>
        /// Clears the queue.
        /// </summary>
        public void Clear()
        {
            // We can't do a truly atomic clear operation without iterating through and dequeueing each individual node.
            // So the approximate count could potentially go negative if another operation is dequeueing while we clear.
            _head = _tail;
            _head.Dequeued = 1;
            _approximateCount = 0;
        }

        /// <summary>
        /// Attempts to remove the specified item from the list, if it is present
        /// </summary>
        /// <returns></returns>
        //public bool Remove(TQueue toRemove)
        //{
        //    QueueNode<TQueue> curNode = _head;
        //    while (curNode != null)
        //    {
        //        if (_comparer.Equals(toRemove, curNode.Item))
        //        {
        //            if (Interlocked.CompareExchange(ref curNode.Dequeued, 1, 0) == 0)
        //            {
        //                Interlocked.Decrement(ref _approximateCount);
        //                return true;
        //            }
        //            else
        //            {
        //                return false;
        //            }
        //        }

        //        curNode = curNode.Next;
        //    }

        //    // Didn't find it
        //    return false;
        //}

        public IEnumerator<TQueue> GetEnumerator()
        {
            return new ConcurrentQueueEnumerator<TQueue>(_head);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new ConcurrentQueueEnumerator<TQueue>(_head);
        }

        private class QueueNode<TNode>
        {
            public TNode Item;
            public QueueNode<TNode> Next;
            public int Dequeued = 0;
        }

        /// <summary>
        /// Implements approximate thread-safe enumeration of the queue by starting at head and returning all linked nodes that haven't been dequeued yet.
        /// </summary>
        /// <typeparam name="TIter"></typeparam>
        private class ConcurrentQueueEnumerator<TIter> : IEnumerator<TIter>
        {
            private readonly QueueNode<TIter> _head;
            private QueueNode<TIter> _curNode;

            public ConcurrentQueueEnumerator(QueueNode<TIter> headNode)
            {
                _head = headNode;
                _curNode = headNode;
            }

            public TIter Current
            {
                get
                {
                    if (_curNode == null)
                    {
                        throw new IndexOutOfRangeException("Enumerated out of range");
                    }

                    return _curNode.Item;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    if (_curNode == null)
                    {
                        throw new IndexOutOfRangeException("Enumerated out of range");
                    }

                    return _curNode.Item;
                }
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                do
                {
                    _curNode = _curNode.Next;
                }
                while (_curNode != null && _curNode.Dequeued != 1);
                return _curNode != null;
            }

            public void Reset()
            {
                _curNode = _head;
            }
        }
#else
        private System.Collections.Concurrent.ConcurrentQueue<TQueue> _inner;

        public ConcurrentQueue()
        {
            _inner = new System.Collections.Concurrent.ConcurrentQueue<TQueue>();
        }

        /// <summary>
        /// The approximate number of items that are in the queue.
        /// </summary>
        public int ApproximateCount => _inner.Count;
        /// <summary>
        /// Indicates whether this queue is probably empty.
        /// </summary>
        public bool IsEmpty => _inner.IsEmpty;

        /// <summary>
        /// Pushes an item onto the queue.
        /// </summary>
        /// <param name="item"></param>
        public void Enqueue(TQueue item)
        {
            _inner.Enqueue(item);
        }

        /// <summary>
        /// Attempts to remove an item from the queue.
        /// </summary>
        /// <param name="returnVal">The place for the dequeued value, if found</param>
        /// <returns>True if a value was removed from the queue</returns>
        public bool TryDequeue(out TQueue returnVal)
        {
            return _inner.TryDequeue(out returnVal);
        }

        /// <summary>
        /// Clears the queue.
        /// </summary>
        public void Clear()
        {
            _inner = new System.Collections.Concurrent.ConcurrentQueue<TQueue>();
        }

        public IEnumerator<TQueue> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_inner).GetEnumerator();
        }
#endif
    }
}
