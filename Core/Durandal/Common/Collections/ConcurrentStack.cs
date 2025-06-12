using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Collections
{
    /// <summary>
    /// Very basic implementation of a concurrent stack.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConcurrentStack<T>
    {
        private readonly Stack<T> _instance = new Stack<T>();
        private readonly object _mutex = new object();
        
        public ConcurrentStack()
        {
        }

        public bool TryPop(out T returnVal)
        {
            returnVal = default(T);
            lock (_mutex)
            {
                if (_instance.Count == 0)
                {
                    return false;
                }

                returnVal = _instance.Pop();
                return true;
            }
        }

        public void Push(T item)
        {
            lock (_mutex)
            {
                _instance.Push(item);
            }
        }

        public int Count
        {
            get
            {
                lock (_mutex)
                {
                    return _instance.Count;
                }
            }
        }
    }
}
