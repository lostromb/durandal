using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.Collections.Interning
{
    /// <summary>
    /// This struct can have 3 interpretations:
    /// 1. A branched node.
    ///     Signified by SubtableIndex &gt; 0.
    ///     In this case, SubtableIndex, PivotIndex, PivotValue are valid.
    /// 2. A leaf node.
    ///     Signified by SubtableIndex &lt; 0
    ///     In this case, ValueOrdinal, DataStart, DataLength are valid
    /// 3. A null node
    ///     Signified by SubtableIndex == 0 and DataLength == 0
    /// </summary>
    [StructLayout(layoutKind: LayoutKind.Explicit, Size = sizeof(int) * 3)]
    internal struct BinaryTreeNode
    {
        public static BinaryTreeNode CreateBranchNode(
            int pivotIndex,
            int pivotValue,
            int subTableIndex)
        {
            return new BinaryTreeNode()
            {
                SubtableIndex = subTableIndex,
                //ValueOrdinalNegative = subTableIndex,
                PivotIndex = pivotIndex,
                //DataStart = pivotIndex,
                PivotValue = pivotValue,
                //DataLength = pivotValue
            };
        }

        public static BinaryTreeNode CreateLeafNode(
            int dataStart,
            int dataLength,
            int valueOrdinal)
        {
            return new BinaryTreeNode()
            {
                //SubtableIndex = 0 - valueOrdinal,
                ValueOrdinalNegative = 0 - valueOrdinal,
                //PivotIndex = dataStart,
                DataStart = dataStart,
                //PivotValue = dataLength,
                DataLength = dataLength
            };
        }

        public bool IsBranchNode => SubtableIndex > 0;
        public bool IsLeafNode => SubtableIndex <= 0 && DataStart > 0;
        public bool IsNullNode => SubtableIndex == 0 && DataStart == 0;

        public int ValueOrdinal => 0 - SubtableIndex;

        /// <summary>
        /// If this node is a branch node, this value will be > 0.
        /// Otherwise, this field is the negative of the value ordinal
        /// </summary>
        [FieldOffset(0)]
        public int SubtableIndex;

        /// <summary>
        /// Ordinal value if this is a leaf node,
        /// </summary>
        [FieldOffset(0)]
        public int ValueOrdinalNegative;

        /// <summary>
        /// Pointer to start of binary data in byte table, or -1 for invalid
        /// </summary>
        [FieldOffset(4)]
        public int DataStart;

        /// <summary>
        /// The index inside of the data array which this node pivots on
        /// </summary>
        [FieldOffset(4)]
        public int PivotIndex;

        /// <summary>
        /// Length of binary data, or 0 if no data
        /// </summary>
        [FieldOffset(8)]
        public int DataLength;

        /// <summary>
        /// The value that this node pivots on, less than or equal == left, greater = right
        /// </summary>
        [FieldOffset(8)]
        public int PivotValue;

        public override string ToString()
        {
            if (IsNullNode)
            {
                return "NULL";
            }
            else if (IsLeafNode)
            {
                return string.Format("LEAF Ordinal {0} DataIdx {1} DataLen {2}", ValueOrdinal, DataStart, DataLength);
            }
            else if (IsBranchNode)
            {
                return string.Format("BRANCH PivotIdx {0} PivotValue {1} Subtable {2}", PivotIndex, PivotValue, SubtableIndex);
            }
            else
                return "ERROR";
        }
    }
}
