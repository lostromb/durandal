using Durandal.Common.MathExt;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Durandal.Common.Collections.Interning.Impl
{
    /// <summary>
    /// Read-only, high performance internalizer for arbitrary spans of chars.
    /// If the span is known, this will return a unique ID that represents
    /// that span within some known scope.
    /// 
    /// This variant uses a linear table where index corresponds to input length,
    /// so is not suitable for sparse entries with long data lengths.
    /// </summary>
    internal class SealedInternalizer_Char_Linear : ISealedPrimitiveInternalizer<char>
    {
        private readonly BinaryTreeNode[] _table;
        private readonly int _longestEntryLength;
        private readonly char[] _charTable;

        /// <summary>
        /// Creates a new <see cref="SealedInternalizer_Char_Linear"/> from a collection of already established
        /// ordinals (presumably from a dictionary of some kind).
        /// The cost of creating the internal binary tree is non-trivial as it tries to do as much work
        /// as possible beforehand to make sure that lookups are as performant as possible with minimal
        /// memory footprint.
        /// </summary>
        /// <param name="entries">The list of entries to build this internalizer from.</param>
        public SealedInternalizer_Char_Linear(IEnumerable<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>[] allEntries = entries.ToArray();

            // The longest table entry defines the initial table length, so just use it.
            // No fancy wraparound, power-of-two, or masking.
            // This corresponds with _initialTableLength in other implementations.
            _longestEntryLength = BinaryTreeCommon.GetLongestEntryLength(allEntries);

            // Create tables
            int[] expectedLengths;
            bool allKeysUnique;
            BinaryTreeCommon.CreateInitialTable<char>(
                allEntries,
                _longestEntryLength + 1,
                out _table,
                out _charTable,
                out expectedLengths,
                out allKeysUnique,
                InterpretCharOrdinal);
        }

        public InternalizerFeature Features => InternalizerFeature.None;

        public long EstimatedMemoryUse
        {
            get
            {
                return (_charTable.Length * sizeof(byte)) +
                    (_table.Length * 12 /* sizeof(BinaryTreeNode) */);
            }
        }

        /// <inheritdoc />
        public bool TryGetInternalizedKey(ReadOnlySpan<char> data, out InternedKey<ReadOnlyMemory<char>> returnVal)
        {
            if (data.Length > _longestEntryLength)
            {
                returnVal = default(InternedKey<ReadOnlyMemory<char>>);
                return false;
            }

            int nodeIdx = data.Length;

            while (true)
            {
                ref BinaryTreeNode curNode = ref _table[nodeIdx];
                if (curNode.IsBranchNode)
                {
                    // Theoretically in .net 8, CMOV logic in the JIT will make this evaluation branchless
                    // https://github.com/dotnet/runtime/pull/81267
                    nodeIdx = curNode.SubtableIndex + 1;
                    if (InterpretCharOrdinal(data[curNode.PivotIndex]) <= curNode.PivotValue)
                    {
                        nodeIdx = curNode.SubtableIndex;
                    }
                }
                else
                {
                    // could be a leaf node or null node
                    // If it's a leaf node, dataStart will be positive and we
                    // just do the regular value comparison
                    // If it's a null node, returnVal will be zero
                    // use dataStart == 0 to detect the null node
                    returnVal = new InternedKey<ReadOnlyMemory<char>>(curNode.ValueOrdinal);
                    return curNode.DataStart != 0 && data.Equals(_charTable.AsSpan(curNode.DataStart, curNode.DataLength), StringComparison.Ordinal);
                }
            }
        }

#if NET7_0_OR_GREATER
        // todo implement this
        public IEnumerable<InternedKey<ReadOnlyMemory<char>>?> TryGetInternalizedKeys(IEnumerable<ReadOnlyMemory<char>> inputs)
        {
            Span<int> subtableIdxs = stackalloc int[Vector<int>.Count];
            Span<int> pivotCompares = stackalloc int[Vector<int>.Count];
            Span<int> pivotVals = stackalloc int[Vector<int>.Count];
            ReadOnlyMemory<char>[] currentInputs = new ReadOnlyMemory<char>[Vector<int>.Count];

            IEnumerator<ReadOnlyMemory<char>> inputEnumerator = inputs.GetEnumerator();
            int inputsQueued = 0;
            int inputsProcessed = 0;
            bool moreInputs = false;
            while (moreInputs && inputsQueued < Vector<int>.Count)
            {
                moreInputs = inputEnumerator.MoveNext();
                if (moreInputs)
                { 
                    ReadOnlyMemory<char> current = inputEnumerator.Current;
                    if (current.Length <= _longestEntryLength)
                    {
                        subtableIdxs[inputsQueued++] = current.Length;
                    }
                }
            }

            do
            {
                // Fill vectors with values
                for (int c = 0; c < Vector<int>.Count; c++)
                {
                    if (subtableIdxs[c] <= 0)
                    {
                        // This input lane has finished. Return a result and prepare next input.
                        InternedKey<ReadOnlyMemory<char>> returnVal = new InternedKey<ReadOnlyMemory<char>>(0 - subtableIdxs[c]);
                        //yield return curNode.DataStart != 0 && data.Equals(_charTable.AsSpan(curNode.DataStart, curNode.DataLength), StringComparison.Ordinal);
                        inputsProcessed++;

                        moreInputs = inputEnumerator.MoveNext();
                        while (moreInputs)
                        {
                            ReadOnlyMemory<char> current = inputEnumerator.Current;
                            if (current.Length <= _longestEntryLength)
                            {
                                currentInputs[c] = current;
                                subtableIdxs[c] = current.Length;
                                BinaryTreeNode node = _table[current.Length];
                                pivotCompares[c] = InterpretCharOrdinal(currentInputs[c].Span[node.PivotIndex]);
                                pivotVals[c] = node.PivotValue;
                                inputsQueued++;
                            }
                            else
                            {
                                moreInputs = inputEnumerator.MoveNext();
                            }
                        }
                    }
                    else
                    {
                        BinaryTreeNode node = _table[subtableIdxs[c]];
                        pivotCompares[c] = InterpretCharOrdinal(currentInputs[c].Span[node.PivotIndex]);
                        pivotVals[c] = node.PivotValue;
                    }
                }

                // Compare pivots in parallel and alter subtable idxs based on branch result
                Vector.Subtract(
                    new Vector<int>(subtableIdxs),
                    Vector.GreaterThan(
                        new Vector<int>(pivotVals),
                        new Vector<int>(pivotCompares))).CopyTo(subtableIdxs);

            } while (inputsProcessed < inputsQueued);

            yield return null;
        }
#endif

        /// <inheritdoc />
        public bool TryGetInternalizedValue(
            ReadOnlySpan<char> data,
            out ReadOnlySpan<char> internedValue,
            out InternedKey<ReadOnlyMemory<char>> returnVal)
        {
            if (data.Length > _longestEntryLength)
            {
                internedValue = ReadOnlySpan<char>.Empty;
                returnVal = default(InternedKey<ReadOnlyMemory<char>>);
                return false;
            }

            int nodeIdx = data.Length;

            while (true)
            {
                ref BinaryTreeNode curNode = ref _table[nodeIdx];
                if (curNode.IsBranchNode)
                {
                    nodeIdx = curNode.SubtableIndex + 1;
                    if (InterpretCharOrdinal(data[curNode.PivotIndex]) <= curNode.PivotValue)
                    {
                        nodeIdx = curNode.SubtableIndex;
                    }
                }
                else
                {
                    // could be a leaf node or null node
                    // If it's a leaf node, dataLength will be positive and we
                    // just do the regular value comparison
                    // If it's a null node, returnVal will be zero
                    // use dataStart == 0 to detect the null node
                    returnVal = new InternedKey<ReadOnlyMemory<char>>(curNode.ValueOrdinal);
                    internedValue = _charTable.AsSpan(curNode.DataStart, curNode.DataLength);
                    return curNode.DataStart != 0 && data.Equals(internedValue, StringComparison.Ordinal);
                }
            }
        }

        public IEnumerator<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> GetEnumerator()
        {
            return new PrimitiveBinaryTreeEnumerator<char>(_table, _charTable);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new PrimitiveBinaryTreeEnumerator<char>(_table, _charTable);
        }

        private static int InterpretCharOrdinal(char c)
        {
            return (int)c;
        }
    }
}
