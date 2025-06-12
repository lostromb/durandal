using System;
using System.Collections.Generic;

namespace Durandal.Common.Collections
{
    /// <summary>
    /// Defines an interface that enumerates a series of primitive read-only spans, which exists
    /// because you can't use a ref type as a parameter to a normal <see cref="IEnumerator{T}"/>
    /// </summary>
    /// <typeparam name="T">The data type of the spans being enumerated.</typeparam>
    public interface ISpanEnumerator<T>
    {
        /// <summary>
        /// Moves to the next item of enumeration, returning true if we haven't reached the end of the series yet.
        /// </summary>
        /// <returns>True if the enumerator advanced to a valid value, false if enumeration is over.</returns>
        bool MoveNext();

        /// <summary>
        /// Resets enumeration back to the beginning. Some kinds of enumeration may not support this.
        /// </summary>
        /// <returns>True if reset was successful, false if it's not supported.</returns>
        bool Reset();

        /// <summary>
        /// Gets a reference to the current span under enumeration.
        /// </summary>
        ReadOnlySpan<T> Current { get; }
    }
}