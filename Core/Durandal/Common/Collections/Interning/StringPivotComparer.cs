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
    internal class StringPivotComparer<T> : IComparer<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>>
    {
        private readonly Func<char, int> _valueInterpreter;
        private int _pivotIdx;

        /// <summary>
        /// Constructs a new <see cref="ArrayPivotComparer{T}"/> for sorting arrays.
        /// </summary>
        /// <param name="pivotIdx">The index of the value to pivot on</param>
        /// <param name="valueInterpreter">A delegate for converting the array values
        /// (presumably some primitive such as char) to an integer.</param>
        public StringPivotComparer(int pivotIdx, Func<char, int> valueInterpreter)
        {
            _pivotIdx = pivotIdx;
            _valueInterpreter = valueInterpreter;
        }

        public int PivotIndex
        {
            get
            {
                return _pivotIdx;
            }
            set
            {
                _pivotIdx = value;
            }
        }

        /// <inheritdoc/>
        public int Compare(KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string> x, KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string> y)
        {
            return _valueInterpreter(x.Value[_pivotIdx]) - _valueInterpreter(y.Value[_pivotIdx]);
        }
    }
}
