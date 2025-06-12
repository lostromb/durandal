using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Durandal.Common.Collections.Interning.Impl
{
    /// <summary>
    /// Test data structure to see if holding the tree data contiguously
    /// with an alternate layout improves cache line performance (based on existing linear case-insensitive impl)
    /// </summary>
    internal class SealedInternalizer_CharIgnoreCase_CacheOptimized : ISealedPrimitiveInternalizer<char>
    {
        private const int SIZE_OF_BINARY_TREE_NODE = 12;
        private readonly byte[] _rawTable;
        private readonly int _longestEntryLength;

        /// <summary>
        /// Creates a new <see cref="SealedInternalizer_CharIgnoreCase_Linear"/> from a collection of already established
        /// ordinals (presumably from a dictionary of some kind).
        /// The cost of creating the internal binary tree is non-trivial as it tries to do as much work
        /// as possible beforehand to make sure that lookups are as performant as possible with minimal
        /// memory footprint.
        /// </summary>
        /// <param name="entries">The list of entries to build this internalizer from.</param>
        public SealedInternalizer_CharIgnoreCase_CacheOptimized(
            IEnumerable<KeyValuePair<InternedKey<ReadOnlyMemory<char>>,
            ReadOnlyMemory<char>>> entries,
            bool depthFirst = true)
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
            BinaryTreeNode[] table;
            char[] charTable;
            bool allKeysUnique;
            BinaryTreeCommon.CreateInitialTable<char>(
                allEntries,
                _longestEntryLength + 1,
                out table,
                out charTable,
                out expectedLengths,
                out allKeysUnique,
                InterpretCharInvariant);

            _rawTable = ReformatTables(table, charTable, _longestEntryLength + 1, depthFirst);
        }

        private static byte[] ReformatTables(BinaryTreeNode[] tree, char[] chars, int initialTableLength, bool depthFirst)
        {
            // todo see if we can do better padding calculation?
            int maxPadding = (sizeof(int) - 1) * tree.Count();
            int totalDataLength = (tree.Length * SIZE_OF_BINARY_TREE_NODE) + (chars.Length * sizeof(char));
            byte[] returnVal = new byte[totalDataLength + maxPadding];

            // Copy the initial table across
            int byteIndex = initialTableLength * SIZE_OF_BINARY_TREE_NODE;

            if (depthFirst)
            {
                // As we're recursing from the initial table, we're also copying from the typed array to the byte array
                for (int c = 0; c < initialTableLength; c++)
                {
                    BinaryTreeNode root = tree[c];
                    ReformatTablesRecursiveDepthFirst(returnVal, tree, chars, ref root, ref byteIndex);
                    MemoryMarshal.Write(returnVal.AsSpan(c * SIZE_OF_BINARY_TREE_NODE, SIZE_OF_BINARY_TREE_NODE), ref root);
                }
            }
            else
            {
                Queue<int> nodesQueue = new Queue<int>();   
                for (int c = 0; c < initialTableLength; c++)
                {
                    BinaryTreeNode root = tree[c];
                    MemoryMarshal.Write(returnVal.AsSpan(c * SIZE_OF_BINARY_TREE_NODE, SIZE_OF_BINARY_TREE_NODE), ref root);
                    nodesQueue.Enqueue(c * SIZE_OF_BINARY_TREE_NODE);
                }

                ReformatTablesRecursiveBreadthFirst(returnVal, tree, chars, ref byteIndex, nodesQueue);
            }

            return returnVal;
        }

        private static int Align(ref int index, int wordSize)
        {
            // Align to 4-byte words to try and avoid weird unaligned data access penalties
            // depending on the underlying processor
            // (though not too much padding either since cache lines are probably only 64 bytes)
            if (wordSize > 1 && (index % wordSize) != 0)
            {
                int offset = wordSize - (index % wordSize);
                index += offset;
                return offset;
            }
            else
            {
                return 0;
            }
        }

        private static void ReformatTablesRecursiveDepthFirst(
            byte[] outputTable,
            BinaryTreeNode[] tree,
            char[] chars,
            ref BinaryTreeNode rootNode,
            ref int dataIndex)
        {
            if (rootNode.IsNullNode)
            {
                // Nothing to do.
            }
            else if (rootNode.IsLeafNode)
            {
                // Copy data across.
                int charDataLengthBytes = rootNode.DataLength * sizeof(char);
                Align(ref dataIndex, sizeof(char));
                MemoryMarshal.Cast<char, byte>(chars.AsSpan(rootNode.DataStart, rootNode.DataLength)).CopyTo(outputTable.AsSpan(dataIndex, charDataLengthBytes));
                
                // Make sure we update the root node with the new index
                rootNode.DataStart = dataIndex;
                rootNode.DataLength = charDataLengthBytes;
                dataIndex += charDataLengthBytes;
            }
            else
            {
                // It's a branch node.
                BinaryTreeNode nodeLeft = tree[rootNode.SubtableIndex];
                BinaryTreeNode nodeRight = tree[rootNode.SubtableIndex + 1];

                // Reserve space for left and right nodes next to each other
                Align(ref dataIndex, SIZE_OF_BINARY_TREE_NODE);
                int idxLeft = dataIndex;
                int idxRight = dataIndex + SIZE_OF_BINARY_TREE_NODE;
                dataIndex += (SIZE_OF_BINARY_TREE_NODE * 2);

                // Recurse into each side
                ReformatTablesRecursiveDepthFirst(outputTable, tree, chars, ref nodeLeft, ref dataIndex);
                ReformatTablesRecursiveDepthFirst(outputTable, tree, chars, ref nodeRight, ref dataIndex);

                // Then copy the now modified left and right nodes into the tree
                MemoryMarshal.Write(outputTable.AsSpan(idxLeft, SIZE_OF_BINARY_TREE_NODE), ref nodeLeft);
                MemoryMarshal.Write(outputTable.AsSpan(idxRight, SIZE_OF_BINARY_TREE_NODE), ref nodeRight);

                rootNode.SubtableIndex = idxLeft / SIZE_OF_BINARY_TREE_NODE; // convert byte index to node index
            }
        }

        private static void ReformatTablesRecursiveBreadthFirst(
            byte[] outputTable,
            BinaryTreeNode[] tree,
            char[] chars,
            ref int dataIndex,
            Queue<int> treeIndexesToProcess)
        {
            while (treeIndexesToProcess.Count > 0)
            {
                int rootNodeByteIdx = treeIndexesToProcess.Dequeue();
                BinaryTreeNode rootNode = MemoryMarshal.Read<BinaryTreeNode>(outputTable.AsSpan(rootNodeByteIdx, SIZE_OF_BINARY_TREE_NODE));
                if (rootNode.IsNullNode)
                {
                    // Nothing to do.
                }
                else if (rootNode.IsLeafNode)
                {
                    // Copy data across.
                    int charDataLengthBytes = rootNode.DataLength * sizeof(char);
                    Align(ref dataIndex, sizeof(char));
                    MemoryMarshal.Cast<char, byte>(chars.AsSpan(rootNode.DataStart, rootNode.DataLength)).CopyTo(outputTable.AsSpan(dataIndex, charDataLengthBytes));

                    // Make sure we update the root node with the new index
                    rootNode.DataStart = dataIndex;
                    rootNode.DataLength = charDataLengthBytes;
                    MemoryMarshal.Write(outputTable.AsSpan(rootNodeByteIdx, SIZE_OF_BINARY_TREE_NODE), ref rootNode);
                    dataIndex += charDataLengthBytes;
                }
                else
                {
                    // It's a branch node.
                    BinaryTreeNode nodeLeft = tree[rootNode.SubtableIndex];
                    BinaryTreeNode nodeRight = tree[rootNode.SubtableIndex + 1];

                    // Reserve space for left and right nodes next to each other
                    Align(ref dataIndex, SIZE_OF_BINARY_TREE_NODE);
                    int idxLeft = dataIndex;
                    int idxRight = dataIndex + SIZE_OF_BINARY_TREE_NODE;
                    dataIndex += (SIZE_OF_BINARY_TREE_NODE * 2);

                    // Copy them into the tree as-is (they will be modified later)
                    MemoryMarshal.Write(outputTable.AsSpan(idxLeft, SIZE_OF_BINARY_TREE_NODE), ref nodeLeft);
                    MemoryMarshal.Write(outputTable.AsSpan(idxRight, SIZE_OF_BINARY_TREE_NODE), ref nodeRight);
                    treeIndexesToProcess.Enqueue(idxLeft);
                    treeIndexesToProcess.Enqueue(idxRight);

                    // And update the root node's data pointer
                    rootNode.SubtableIndex = idxLeft / SIZE_OF_BINARY_TREE_NODE; // convert byte index to node index
                    MemoryMarshal.Write(outputTable.AsSpan(rootNodeByteIdx, SIZE_OF_BINARY_TREE_NODE), ref rootNode);
                }
            }
        }

        public InternalizerFeature Features => InternalizerFeature.CaseInsensitive;

        public long EstimatedMemoryUse
        {
            get
            {
                return _rawTable.Length;
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

            int nodeOffset = data.Length;// * (SIZE_OF_BINARY_TREE_NODE / sizeof(int));
            Span<BinaryTreeNode> nodeEnum = MemoryMarshal.Cast<byte, BinaryTreeNode>(_rawTable.AsSpan());

            while (true)
            {
                ref BinaryTreeNode curNode = ref nodeEnum[nodeOffset];
                if (curNode.IsBranchNode)
                {
                    nodeOffset = curNode.SubtableIndex + 1;
                    if (InterpretCharInvariant(data[curNode.PivotIndex]) <= curNode.PivotValue)
                    {
                        nodeOffset = curNode.SubtableIndex;
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
                    return curNode.DataStart != 0 && data.Equals(MemoryMarshal.Cast<byte, char>(_rawTable.AsSpan(curNode.DataStart, curNode.DataLength)), StringComparison.OrdinalIgnoreCase);
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

            int nodeOffsetBytes = data.Length * SIZE_OF_BINARY_TREE_NODE;

            while (true)
            {
                BinaryTreeNode curNode = MemoryMarshal.Read<BinaryTreeNode>(_rawTable.AsSpan(nodeOffsetBytes, SIZE_OF_BINARY_TREE_NODE));
                if (curNode.IsBranchNode)
                {
                    nodeOffsetBytes = curNode.SubtableIndex + 1;
                    if (InterpretCharInvariant(data[curNode.PivotIndex]) <= curNode.PivotValue)
                    {
                        nodeOffsetBytes = curNode.SubtableIndex;
                    }

                    nodeOffsetBytes *= SIZE_OF_BINARY_TREE_NODE;
                }
                else
                {
                    // could be a leaf node or null node
                    // If it's a leaf node, dataLength will be positive and we
                    // just do the regular value comparison
                    // If it's a null node, returnVal will be zero
                    // use dataStart == 0 to detect the null node
                    returnVal = new InternedKey<ReadOnlyMemory<char>>(curNode.ValueOrdinal);
                    internedValue = MemoryMarshal.Cast<byte, char>(_rawTable.AsSpan(curNode.DataStart, curNode.DataLength));
                    return curNode.DataStart != 0 && data.Equals(internedValue, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public IEnumerator<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>> GetEnumerator()
        {
            return new Enumerator(_rawTable, _longestEntryLength + 1);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(_rawTable, _longestEntryLength + 1);
        }

        private static int InterpretCharInvariant(char c)
        {
            return (int)(char.ToUpperInvariant(c));
        }

        private class Enumerator : IEnumerator<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>>
        {
            private readonly byte[] _rawTable;
            private readonly int _initialTableLength;
            private readonly Stack<int> _nodesToVisit;

            public Enumerator(byte[] rawTable, int initialTableLength)
            {
                _rawTable = rawTable.AssertNonNull(nameof(rawTable));
                _initialTableLength = initialTableLength.AssertNonNegative(nameof(initialTableLength));
                _nodesToVisit = new Stack<int>();
                Reset();
            }

            public KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> Current { get; private set; }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                while (_nodesToVisit.Count > 0)
                {
                    int thisNodeIndex = _nodesToVisit.Pop();
                    BinaryTreeNode thisNode = MemoryMarshal.Read<BinaryTreeNode>(_rawTable.AsSpan(thisNodeIndex, SIZE_OF_BINARY_TREE_NODE));
                    
                    if (thisNode.IsLeafNode)
                    {
                        // hack since we can't use MemoryMarshal.Cast on a Memory reference...
                        char[] copiedData = new char[thisNode.DataLength / sizeof(char)];
                        MemoryMarshal.Cast<byte, char>(_rawTable.AsSpan(thisNode.DataStart, thisNode.DataLength)).CopyTo(copiedData.AsSpan());
                        Current = new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                            new InternedKey<ReadOnlyMemory<char>>(thisNode.ValueOrdinal),
                            copiedData.AsMemory());

                        return true;
                    }
                    else if (thisNode.IsBranchNode)
                    {
                        _nodesToVisit.Push(thisNode.SubtableIndex * SIZE_OF_BINARY_TREE_NODE);
                        _nodesToVisit.Push((thisNode.SubtableIndex * SIZE_OF_BINARY_TREE_NODE) + SIZE_OF_BINARY_TREE_NODE);
                    }
                }

                Current = default(KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>);
                return false;
            }

            public void Reset()
            {
                _nodesToVisit.Clear();
                for (int c = 0; c < _initialTableLength; c++)
                {
                    _nodesToVisit.Push(c * SIZE_OF_BINARY_TREE_NODE);
                }
            }
        }
    }
}
