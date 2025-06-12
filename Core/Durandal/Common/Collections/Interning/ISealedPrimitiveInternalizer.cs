using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections.Interning
{
    /// <summary>
    /// Represents a fixed, readonly collection of value -> ID mappings which can be used to
    /// uniquely identify known values and reduce them to integral IDs for easier manipulation
    /// down the line (for example, it might store a bunch of string -> feature ID mappings
    /// to be used in a language model trainer). Objects of this type are immutable and thread-safe.
    /// </summary>
    /// <typeparam name="T">The types of value to be internalized.</typeparam>
    public interface ISealedPrimitiveInternalizer<T>
        : IEnumerable<KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>>>
            where T : struct
    {
        /// <summary>
        /// Gets the features of this internalizer, which provide some insight
        /// into its implementation. The most common feature is case insensitive comparison
        /// for char buffers.
        /// </summary>
        InternalizerFeature Features { get; }

        /// <summary>
        /// Tried to get the internalized ID for a given value, or returns false if not found.
        /// </summary>
        /// <param name="input">The value to be internalized.</param>
        /// <param name="internalizedId">If found, this is the unique ID which designates the input value.</param>
        /// <returns>True if the value was found in the internalizer.</returns>
        bool TryGetInternalizedKey(ReadOnlySpan<T> input, out InternedKey<ReadOnlyMemory<T>> internalizedId);

        /// <summary>
        /// Tried to get the internalized ID for a given value, or returns false if not found.
        /// The internalizer may also perform some transformation
        /// of the input (most commonly, string case normalization), in which case the altered internalized
        /// form will be returned as well.
        /// </summary>
        /// <param name="input">The value to be internalized.</param>
        /// <param name="internalizedValue">If found, this is the actual internalized value, which may have been modified from the original.</param>
        /// <param name="internalizedId">If found, this is the unique ID which designates the input value.</param>
        /// <returns>True if the value was found in the internalizer.</returns>
        bool TryGetInternalizedValue(
            ReadOnlySpan<T> input,
            out ReadOnlySpan<T> internalizedValue,
            out InternedKey<ReadOnlyMemory<T>> internalizedId);
    }
}
