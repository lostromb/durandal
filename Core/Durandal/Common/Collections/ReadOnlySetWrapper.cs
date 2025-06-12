using Durandal.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections
{
    /// <summary>
    /// Since, for some reason, the built in C# set does not implement any sort of "read-only set", we have to make a wrapper for it here.
    /// </summary>
    /// <typeparam name="T">The type of objects contained in this set.</typeparam>
    public class ReadOnlySetWrapper<T> : IReadOnlySet<T>
    {
        private ISet<T> _inner;

        public ReadOnlySetWrapper(ISet<T> inner)
        {
            _inner = inner.AssertNonNull(nameof(inner));
        }

        public bool Contains(T obj)
        {
            return _inner.Contains(obj);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _inner.GetEnumerator();
        }
    }
}
