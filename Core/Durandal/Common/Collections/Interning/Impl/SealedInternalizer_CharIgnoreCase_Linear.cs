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
    internal class SealedInternalizer_CharIgnoreCase_Linear : ISealedPrimitiveInternalizer<char>
    {
        private readonly BinaryTreeNode[] _table;
        private readonly int _longestEntryLength;
        private readonly char[] _charTable;

        /// <summary>
        /// Creates a new <see cref="SealedInternalizer_CharIgnoreCase_Linear"/> from a collection of already established
        /// ordinals (presumably from a dictionary of some kind).
        /// The cost of creating the internal binary tree is non-trivial as it tries to do as much work
        /// as possible beforehand to make sure that lookups are as performant as possible with minimal
        /// memory footprint.
        /// </summary>
        /// <param name="entries">The list of entries to build this internalizer from.</param>
        public SealedInternalizer_CharIgnoreCase_Linear(IEnumerable<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> entries)
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
                InterpretCharInvariant);
        }

        public InternalizerFeature Features => InternalizerFeature.CaseInsensitive;

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
                    // This "branchless" arrangement relies on CMOV support in .net8+
                    nodeIdx = curNode.SubtableIndex + 1;
                    if (InterpretCharInvariant(data[curNode.PivotIndex]) <= curNode.PivotValue)
                    {
                        nodeIdx = curNode.SubtableIndex;
                    }

                    // Alternate branchless form - benchmarks say this is slower
                    //nodeIdx = curNode.SubtableIndex - ((int)(curNode.PivotValue - InterpretCharInvariant(data[curNode.PivotIndex])) >> 31);
                }
                else
                {
                    // could be a leaf node or null node
                    // If it's a leaf node, dataStart will be positive and we
                    // just do the regular value comparison
                    // If it's a null node, returnVal will be zero
                    // use dataStart == 0 to detect the null node
                    returnVal = new InternedKey<ReadOnlyMemory<char>>(curNode.ValueOrdinal);
                    return curNode.DataStart != 0 && data.Equals(_charTable.AsSpan(curNode.DataStart, curNode.DataLength), StringComparison.OrdinalIgnoreCase);
                }
            }
        }

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
                    if (InterpretCharInvariant(data[curNode.PivotIndex]) <= curNode.PivotValue)
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
                    return curNode.DataStart != 0 && data.Equals(internedValue, StringComparison.OrdinalIgnoreCase);
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

        private static int InterpretCharInvariant(char c)
        {
            return (int)(char.ToUpperInvariant(c));
        }
    }
}
