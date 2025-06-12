using Durandal.Common.MathExt;
using Durandal.Common.Test.FVT;
using Durandal.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Durandal.Common.Collections.Interning.Impl
{
    /// <summary>
    /// Read-only, high performance internalizer for arbitrary strings.
    /// If the string is known, this will return a unique ID that represents
    /// that string within some known scope.
    /// 
    /// This variant uses a linear table where index corresponds to input length,
    /// so is not suitable for sparse entries with long data lengths.
    /// </summary>
    internal class SealedInternalizer_StringIgnoreCase_Linear : ISealedStringInternalizer
    {
        private readonly BinaryTreeNode[] _table;
        private readonly int _longestEntryLength;
        private readonly string[] _stringTable;

        public StatisticalSet PivotJumps { get; private set; }
        public StatisticalSet TreeBalance { get; private set; }

        /// <summary>
        /// Creates a new <see cref="SealedInternalizer_CharIgnoreCase_Linear"/> from a collection of already established
        /// ordinals (presumably from a dictionary of some kind).
        /// The cost of creating the internal binary tree is non-trivial as it tries to do as much work
        /// as possible beforehand to make sure that lookups are as performant as possible with minimal
        /// memory footprint.
        /// </summary>
        /// <param name="entries">The list of entries to build this internalizer from.</param>
        public SealedInternalizer_StringIgnoreCase_Linear(IEnumerable<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> entries)
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
            char[] charTable;
            bool allKeysUnique;
            StatisticalSet pivotJumps;
            StatisticalSet balance;
            BinaryTreeCommon.CreateInitialTable<char>(
                allEntries,
                _longestEntryLength + 1,
                out _table,
                out charTable,
                out expectedLengths,
                out allKeysUnique,
                InterpretCharInvariant,
                out pivotJumps,
                out balance);

            PivotJumps = pivotJumps;
            TreeBalance = balance;

            // Now convert the char table to a string table
            List<string> stringTable = new List<string>();
            stringTable.Add(null);
            for (int c = 0; c < _table.Length; c++)
            {
                BinaryTreeNode node = _table[c];
                if (node.IsLeafNode && node.DataStart > 0)
                {
                    stringTable.Add(new string(charTable, node.DataStart, node.DataLength));
                    node.DataStart = stringTable.Count - 1;
                    _table[c] = node;
                }
            }

            _stringTable = stringTable.ToArray();

            Features = InternalizerFeature.CaseInsensitive;
            if (allKeysUnique)
            {
                Features |= InternalizerFeature.CanDoReverseLookup;
            }

            EstimatedMemoryUse = (_table.Length * 12) +
                (_stringTable.Length * BinaryHelpers.SizeOfIntPtr) +
                (_stringTable.Sum((a) => string.IsNullOrEmpty(a) ? 0 : a.Length) * sizeof(char));
        }

        public SealedInternalizer_StringIgnoreCase_Linear(IEnumerable<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            KeyValuePair<InternedKey<ReadOnlyMemory<char>>, string>[] allEntries = entries.ToArray();

            // The longest table entry defines the initial table length, so just use it.
            // No fancy wraparound, power-of-two, or masking.
            // This corresponds with _initialTableLength in other implementations.
            _longestEntryLength = BinaryTreeCommon.GetLongestEntryLength(allEntries);

            // Create tables
            int[] expectedLengths;
            bool allKeysUnique;
            StatisticalSet pivotJumps;
            StatisticalSet balance;
            BinaryTreeCommon.CreateInitialTable(
                allEntries,
                _longestEntryLength + 1,
                out _table,
                out _stringTable,
                out expectedLengths,
                out allKeysUnique,
                InterpretCharInvariant,
                out pivotJumps,
                out balance);

            PivotJumps = pivotJumps;
            TreeBalance = balance;
            Features = InternalizerFeature.CaseInsensitive;
            if (allKeysUnique)
            {
                Features |= InternalizerFeature.CanDoReverseLookup;
            }

            EstimatedMemoryUse = (_table.Length * 12) +
                (_stringTable.Length * BinaryHelpers.SizeOfIntPtr) +
                (_stringTable.Sum((a) => string.IsNullOrEmpty(a) ? 0 : a.Length) * sizeof(char));
        }

        public InternalizerFeature Features { get; private set; }

        public long EstimatedMemoryUse { get; private set; }

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
                    return curNode.DataStart != 0 && data.Equals(_stringTable[curNode.DataStart].AsSpan(), StringComparison.OrdinalIgnoreCase);
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
                    internedValue = _stringTable[curNode.DataStart].AsSpan();
                    return curNode.DataStart != 0 && data.Equals(internedValue, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        
        /// <inheritdoc />
        public bool TryGetInternalizedValue(
            ReadOnlySpan<char> data,
            out string internedValue,
            out InternedKey<ReadOnlyMemory<char>> returnVal)
        {
            if (data.Length > _longestEntryLength)
            {
                internedValue = default(string);
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
                    internedValue = _stringTable[curNode.DataStart];
                    return curNode.DataStart != 0 && data.Equals(internedValue.AsSpan(), StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public IEnumerator<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> GetEnumerator()
        {
            return new StringBinaryTreeEnumerator(_table, _stringTable);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new StringBinaryTreeEnumerator(_table, _stringTable);
        }

        private static int InterpretCharInvariant(char c)
        {
            return (int)(char.ToUpperInvariant(c));
        }
    }
}
