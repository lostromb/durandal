using System.Collections.Generic;

namespace Durandal.Common.Collections
{
    /// <summary>
    /// This is a very specific class to perform the role of "A set of single-item concurrent queues, each queue with a unique key,
    /// which dequeues the most recent value for that key in order of original insertion". Uh, it's hard to explain....
    /// This class is used for the LU model loading, where we want to be able to schedule multiple models to load at once,
    /// but we only care about the most recent model definition for each locale, while also preserving relative order of enqueue -> dequeue.
    /// </summary>
    /// <typeparam name="K"></typeparam>
    /// <typeparam name="V"></typeparam>
    public class MultichannelQueue<K, V>
    {
        private Queue<K> _keyOrdering;
        private IDictionary<K, V> _values;
        private object _mutex;

        public MultichannelQueue()
        {
            _keyOrdering = new Queue<K>();
            _values = new Dictionary<K, V>();
            _mutex = new object();
        }

        /// <summary>
        /// Enqueues an item specified by key. If another item with the same key is already queued, the older one will be replaced
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Enqueue(K key, V value)
        {
            lock(_mutex)
            {
                if (_values.ContainsKey(key))
                {
                    _values.Remove(key);
                }

                _values[key] = value;
                _keyOrdering.Enqueue(key);
            }
        }

        /// <summary>
        /// Attempts to dequeue the oldest item from the queue, of any key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryDequeue(out K key, out V value)
        {
            lock(_mutex)
            {
                while (_values.Count > 0)
                {
                    // Increment through the keys in the order they were inserted, and see if those keys are still there in the dictionary
                    key = _keyOrdering.Dequeue();
                    
                    if (_values.ContainsKey(key))
                    {
                        // Found one; return it
                        value = _values[key];
                        _values.Remove(key);
                        return true;
                    }
                }

                // This path happens if multiple keys are enqueued at once and the first one was processed
                value = default(V);
                key = default(K);
                return false;
            }
        }

        /// <summary>
        /// Removes an item from the queue specified by a key, if it exists
        /// </summary>
        /// <param name="key"></param>
        public void Remove(K key)
        {
            lock(_mutex)
            {
                if (_values.ContainsKey(key))
                {
                    _values.Remove(key);
                }
            }
        }

        /// <summary>
        /// Gets the number of items in this queue, for all keys
        /// </summary>
        public int Count
        {
            get
            {
                lock(_mutex)
                {
                    return _values.Count;
                }
            }
        }
    }
}
