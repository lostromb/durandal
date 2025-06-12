using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections.Interning
{
    /// <summary>
    /// Represents a collection of value -> ID mappings which can be used to assign unique identifiers
    /// to commonly encountered values and convert them to integers to simplify later processing.
    /// </summary>
    /// <typeparam name="T">The types of value to be internalized.</typeparam>
    public interface IPrimitiveInternalizer<T> :
        ISealedPrimitiveInternalizer<T>,
        IReadOnlyCollection<KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>>>
        where T : struct
    {
        /// <summary>
        /// Internalizes a value, either returning its interned ID if the value is already known, or generating
        /// a new unique ID for novel values. The internalizer may also perform some transformation
        /// of the input (most commonly, string case normalization), in which case the altered internalized
        /// form will be returned as well.
        /// </summary>
        /// <param name="input">The value to be internalized.</param>
        /// <param name="internalizedValue">The actual internalized value, which may have been modified from the original.</param>
        /// <returns>A unique ID which designates the input value.</returns>
        InternedKey<ReadOnlyMemory<T>> InternalizeValue(ReadOnlySpan<T> input, out ReadOnlySpan<T> internalizedValue);
    }
}
