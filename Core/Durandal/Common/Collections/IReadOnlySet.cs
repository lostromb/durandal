using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections
{
    /// <summary>
    /// An interface defining an immutable set of objects.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IReadOnlySet<T> : IEnumerable<T>
    {
        /// <summary>
        /// Checks whether this set contains the specified object.
        /// </summary>
        /// <param name="obj">The object to check for</param>
        /// <returns>True if this set contains that object.</returns>
        bool Contains(T obj);
    }
}
