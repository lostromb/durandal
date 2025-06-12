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
    /// This variant uses a perfect hash table for memory compactness (suitable for sparse inputs of any length),
    /// and includes extra safety checks for unknown inputs.
    /// </summary>
    internal class SealedInternalizer_Byte_PerfectHash : ISealedPrimitiveInternalizer<byte>
    {
        private readonly BinaryTreeNode[] _table;
        private readonly int[] _expectedLengths;
        private int _initialTableLength;
        private readonly int _initialTableLengthMask;
        private readonly byte[] _byteTable;

        /// <summary>
        /// Creates a new <see cref="SealedInternalizer_Byte_PerfectHash"/> from a collection of already established
        /// ordinals (presumably from a dictionary of some kind).
        /// The cost of creating the internal binary tree is non-trivial as it tries to do as much work
        /// as possible beforehand to make sure that lookups are as performant as possible with minimal
        /// memory footprint.
        /// </summary>
        /// <param name="entries">The list of entries to build this internalizer from.</param>
        public SealedInternalizer_Byte_PerfectHash(IEnumerable<KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>> entries)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            KeyValuePair<InternedKey<ReadOnlyMemory<byte>>, ReadOnlyMemory<byte>>[] allEntries = entries.ToArray();

            // Optimize the initial length table to find the smallest possible power of two size with no collisions
            _initialTableLength = BinaryTreeCommon.CalculateMinimumLengthTableSizePowerOfTwo<byte>(allEntries);

            // Create a bit mask that will do the equivalent of (N % tableLength), since bitwise AND is faster
            _initialTableLengthMask = 1;
            while (_initialTableLengthMask < _initialTableLength)
            {
                _initialTableLengthMask = (_initialTableLengthMask << 1) | 0x1;
            }

            _initialTableLengthMask >>= 1;

            // Create tables
            bool allKeysUnique;
            BinaryTreeCommon.CreateInitialTable<byte>(
                allEntries,
                _initialTableLength,
                out _table,
                out _byteTable,
                out _expectedLengths,
                out allKeysUnique,
                InterpretByte);
        }

        public InternalizerFeature Features => InternalizerFeature.None;

        public long EstimatedMemoryUse
        {
            get
            {
                return (_byteTable.Length * sizeof(byte)) +
                    (_table.Length * 12 /* sizeof(BinaryTreeNode) */) +
                    (_expectedLengths.Length * sizeof(int));
            }
        }

        /// <inheritdoc />
        public bool TryGetInternalizedKey(ReadOnlySpan<byte> data, out InternedKey<ReadOnlyMemory<byte>> returnVal)
        {
            //int nodeIdx = data.Length % _initialTableLength;
            int nodeIdx = data.Length & _initialTableLengthMask;

            if (data.Length != _expectedLengths[nodeIdx])
            {
                returnVal = default(InternedKey<ReadOnlyMemory<byte>>);
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
            //int nodeIdx = data.Length % _initialTableLength;
            int nodeIdx = data.Length & _initialTableLengthMask;

            if (data.Length != _expectedLengths[nodeIdx])
            {
                internedValue = ReadOnlySpan<byte>.Empty;
                returnVal = default(InternedKey<ReadOnlyMemory<byte>>);
                return false;
            }

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
