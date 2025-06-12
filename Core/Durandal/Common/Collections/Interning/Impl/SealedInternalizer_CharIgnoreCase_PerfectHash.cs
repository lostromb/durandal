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
    /// This variant uses a perfect hash table for memory compactness (suitable for sparse inputs of any length),
    /// and includes extra safety checks for unknown inputs.
    /// </summary>
    internal class SealedInternalizer_CharIgnoreCase_PerfectHash : ISealedPrimitiveInternalizer<char>
    {
        private readonly BinaryTreeNode[] _table;
        private readonly int[] _expectedLengths;
        private int _initialTableLength;
        private readonly int _initialTableLengthMask;
        private readonly char[] _charTable;

        /// <summary>
        /// Creates a new <see cref="SealedInternalizer_Char_PerfectHash"/> from a collection of already established
        /// ordinals (presumably from a dictionary of some kind).
        /// The cost of creating the internal binary tree is non-trivial as it tries to do as much work
        /// as possible beforehand to make sure that lookups are as performant as possible with minimal
        /// memory footprint.
        /// </summary>
        /// <param name="entries">The list of entries to build this internalizer from.</param>
        public SealedInternalizer_CharIgnoreCase_PerfectHash(IEnumerable<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>[] allEntries = entries.ToArray();

            // Optimize the initial length table to find the smallest possible power of two size with no collisions
            _initialTableLength = BinaryTreeCommon.CalculateMinimumLengthTableSizePowerOfTwo<char>(allEntries);

            // Create a bit mask that will do the equivalent of (N % tableLength), since bitwise AND is faster
            _initialTableLengthMask = 1;
            while (_initialTableLengthMask < _initialTableLength)
            {
                _initialTableLengthMask = (_initialTableLengthMask << 1) | 0x1;
            }

            _initialTableLengthMask >>= 1;

            // Create tables
            bool allKeysUnique;
            BinaryTreeCommon.CreateInitialTable<char>(
                allEntries,
                _initialTableLength,
                out _table,
                out _charTable,
                out _expectedLengths,
                out allKeysUnique,
                InterpretCharInvariant);
        }

        public InternalizerFeature Features => InternalizerFeature.CaseInsensitive;

        public long EstimatedMemoryUse
        {
            get
            {
                return (_charTable.Length * sizeof(char)) +
                    (_table.Length * 12 /* sizeof(BinaryTreeNode) */) +
                    (_expectedLengths.Length * sizeof(int));
            }
        }

        /// <inheritdoc />
        public bool TryGetInternalizedKey(ReadOnlySpan<char> data, out InternedKey<ReadOnlyMemory<char>> returnVal)
        {
            //int nodeIdx = data.Length % _initialTableLength;
            int nodeIdx = data.Length & _initialTableLengthMask;

            if (data.Length != _expectedLengths[nodeIdx])
            {
                returnVal = default(InternedKey<ReadOnlyMemory<char>>);
                return false;
            }

            while (true)
            {
                ref BinaryTreeNode curNode = ref _table[nodeIdx];
                if (curNode.IsBranchNode)
                {
                    // Theoretically in .net 8, CMOV logic in the JIT will make this evaluation branchless
                    // https://github.com/dotnet/runtime/pull/81267
                    nodeIdx = curNode.SubtableIndex + 1;
                    if (InterpretCharInvariant(data[curNode.PivotIndex]) <= curNode.PivotValue)
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
            //int nodeIdx = data.Length % _initialTableLength;
            int nodeIdx = data.Length & _initialTableLengthMask;

            if (data.Length != _expectedLengths[nodeIdx])
            {
                internedValue = ReadOnlySpan<char>.Empty;
                returnVal = default(InternedKey<ReadOnlyMemory<char>>);
                return false;
            }

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
