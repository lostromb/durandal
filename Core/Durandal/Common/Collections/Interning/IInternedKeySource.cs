using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections.Interning
{
    /// <summary>
    /// Represents a source of unique interned value identifiers.
    /// All values generated from the same <see cref="IInternedKeySource{T}"/> are guaranteed
    /// to be unique within the 32-bit address space.
    /// </summary>
    /// <typeparam name="T">The type of value that is being interned.</typeparam>
    public interface IInternedKeySource<T>
    {
        /// <summary>
        /// Generates a new unique interned value which can be used as a key when
        /// adding a new value to an <see cref="IPrimitiveInternalizer{T}"/>.
        /// </summary>
        /// <returns>A new unique internal ID.</returns>
        InternedKey<T> GenerateNewUniqueValue();
    }
}
