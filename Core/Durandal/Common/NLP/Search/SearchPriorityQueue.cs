﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Search
{
    public class SearchPriorityQueue<T>
    {
        IComparer<T> comparer;
        T[] heap;
        public int Count { get; private set; }
        public SearchPriorityQueue() : this(null) { }
        public SearchPriorityQueue(int capacity) : this(capacity, null) { }
        public SearchPriorityQueue(IComparer<T> comparer) : this(16, comparer) { }
        public SearchPriorityQueue(int capacity, IComparer<T> comparer)
        {
            this.comparer = (comparer == null) ? Comparer<T>.Default : comparer;
            this.heap = new T[capacity];
        }
        public void push(T v)
        {
            if (Count >= heap.Length) Array.Resize(ref heap, Count * 2);
            heap[Count] = v;
            SiftUp(Count++);
        }
        public T pop()
        {
            var v = top();
            heap[0] = heap[--Count];
            if (Count > 0) SiftDown(0);
            return v;
        }
        public T top()
        {
            if (Count > 0) return heap[0];
            throw new InvalidOperationException("Cannot get the top of an empty queue");
        }
        void SiftUp(int n)
        {
            var v = heap[n];
            for (var n2 = n / 2; n > 0 && comparer.Compare(v, heap[n2]) > 0; n = n2, n2 /= 2) heap[n] = heap[n2];
            heap[n] = v;
        }
        void SiftDown(int n)
        {
            var v = heap[n];
            for (var n2 = n * 2; n2 < Count; n = n2, n2 *= 2)
            {
                if (n2 + 1 < Count && comparer.Compare(heap[n2 + 1], heap[n2]) > 0) n2++;
                if (comparer.Compare(v, heap[n2]) >= 0) break;
                heap[n] = heap[n2];
            }
            heap[n] = v;
        }
    }
}
