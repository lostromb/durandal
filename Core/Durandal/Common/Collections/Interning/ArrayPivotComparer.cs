using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections.Interning
{
    /// <summary>
    /// A comparer used to sort arrays based on the value of the element
    /// at a fixed index.
    /// The two arrays being compared will need to be the same length for this to work, of course.
    /// </summary>
    /// <typeparam name="T">The type of arrays being compared</typeparam>
    internal struct ArrayPivotComparer<T> : IComparer<KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>>>
    {
        private readonly int _pivotIdx;
        private readonly Func<T, int> _valueInterpreter;

        /// <summary>
        /// Constructs a new <see cref="ArrayPivotComparer{T}"/> for sorting arrays.
        /// </summary>
        /// <param name="pivotIdx">The index of the value to pivot on</param>
        /// <param name="valueInterpreter">A delegate for converting the array values
        /// (presumably some primitive such as char) to an integer.</param>
        public ArrayPivotComparer(int pivotIdx, Func<T, int> valueInterpreter)
        {
            _pivotIdx = pivotIdx;
            _valueInterpreter = valueInterpreter;
        }

        /// <inheritdoc/>
        public int Compare(KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>> x, KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>> y)
        {
            return _valueInterpreter(x.Value.Span[_pivotIdx]) - _valueInterpreter(y.Value.Span[_pivotIdx]);
        }
    }
}
