using Durandal.Common.MathExt;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Durandal.Common.Collections.Interning.Impl
{
    /// <summary>
    /// Read-only, high performance internalizer for arbitrary spans of bytes.
    /// If the span is known, this will return a unique ID that represents
    /// that span within some known scope.
    /// 
    /// This variant uses a linear table where index corresponds to input length,
    /// so is not suitable for sparse entries with long data lengths.
    /// </summary>
    internal class SealedInternalizer_Byte_Linear : ISealedPrimitiveInternalizer<byte>
    {
        private readonly BinaryTreeNode[] _table;
        private readonly int _longestEntryLength;
        private readonly byte[] _byteTable;

        /// <summary>
        /// Creates a new <see cref="SealedInternalizer_Byte_Linear"/> from a collection of already established
        /// ordinals (presumably from a dictionary of some kind).
        /// The cost of creating the internal binary tree is non-trivial as it tries to do as much work
        /// as possible beforehand to make sure that lookups are as performant as possible with minimal
        /// memory footprint.
        /// </summary>
        /// <param name="entries">The list of entries to build this internalizer from.</param>
        public SealedInternalizer_Byte_Linear(IEnumerable<KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>[] allEntries = entries.ToArray();

            // The longest table entry defines the initial table length, so just use it.
            // No fancy wraparound, power-of-two, or masking.
            // This corresponds with _initialTableLength in other implementations.
            _longestEntryLength = BinaryTreeCommon.GetLongestEntryLength(allEntries);

            // Create tables
            int[] expectedLengths;
            bool allKeysUnique;
            BinaryTreeCommon.CreateInitialTable<byte>(
                allEntries,
                _longestEntryLength + 1,
                out _table,
                out _byteTable,
                out expectedLengths,
                out allKeysUnique,
                InterpretByte);
        }

        public InternalizerFeature Features => InternalizerFeature.None;

        public long EstimatedMemoryUse
        {
            get
            {
                return (_byteTable.Length * sizeof(byte)) +
                    (_table.Length * 12 /* sizeof(BinaryTreeNode) */);
            }
        }

        /// <inheritdoc />
        public bool TryGetInternalizedKey(ReadOnlySpan<byte> data, out InternedKey<ReadOnlyMemory<byte>> returnVal)
        {
            if (data.Length > _longestEntryLength)
            {
                returnVal = default(InternedKey<ReadOnlyMemory<byte>>);
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
                    if (InterpretByte(data[curNode.PivotIndex]) <= curNode.PivotValue)
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
                    returnVal = new InternedKey<ReadOnlyMemory<byte>>(curNode.ValueOrdinal);
                    return curNode.DataStart != 0 && data.SequenceEqual(_byteTable.AsSpan(curNode.DataStart, curNode.DataLength));
                }
            }
        }

        /// <inheritdoc />
        public bool TryGetInternalizedValue(
            ReadOnlySpan<byte> data,
            out ReadOnlySpan<byte> internedValue,
            out InternedKey<ReadOnlyMemory<byte>> returnVal)
        {
            if (data.Length > _longestEntryLength)
            {
                internedValue = ReadOnlySpan<byte>.Empty;
                returnVal = default(InternedKey<ReadOnlyMemory<byte>>);
                return false;
            }

            int nodeIdx = data.Length;

            while (true)
            {
                ref BinaryTreeNode curNode = ref _table[nodeIdx];
                if (curNode.IsBranchNode)
                {
                    nodeIdx = curNode.SubtableIndex + 1;
                    if (InterpretByte(data[curNode.PivotIndex]) <= curNode.PivotValue)
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
                    returnVal = new InternedKey<ReadOnlyMemory<byte>>(curNode.ValueOrdinal);
                    internedValue = _byteTable.AsSpan(curNode.DataStart, curNode.DataLength);
                    return curNode.DataStart != 0 && data.SequenceEqual(internedValue);
                }
            }
        }

        public IEnumerator<KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>> GetEnumerator()
        {
            return new PrimitiveBinaryTreeEnumerator<byte>(_table, _byteTable);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new PrimitiveBinaryTreeEnumerator<byte>(_table, _byteTable);
        }

        private static int InterpretByte(byte c)
        {
            return (int)c;
        }
    }
}
