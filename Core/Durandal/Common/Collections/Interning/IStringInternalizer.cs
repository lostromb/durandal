using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections.Interning
{
    /// <summary>
    /// Represents a collection of value -> ID mappings which can be used to assign unique identifiers
    /// to commonly encountered values and convert them to integers to simplify later processing.
    /// </summary>
    public interface IStringInternalizer :
        ISealedStringInternalizer,
        IReadOnlyCollection<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>>
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
        InternedKey<ReadOnlyMemory<char>> InternalizeValue(ReadOnlySpan<char> input, out string internalizedValue);
    }
}
