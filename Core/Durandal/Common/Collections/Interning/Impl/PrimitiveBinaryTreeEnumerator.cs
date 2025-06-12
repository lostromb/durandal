using Durandal.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections.Interning.Impl
{
    internal class PrimitiveBinaryTreeEnumerator<T> : IEnumerator<KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>>>
    {
        private readonly BinaryTreeNode[] _nodeTable;
        private readonly T[] _valueTable;
        private int _currentNodeTableIdx;

        public PrimitiveBinaryTreeEnumerator(BinaryTreeNode[] nodeTable, T[] valueTable)
        {
            _nodeTable = nodeTable.AssertNonNull(nameof(nodeTable));
            _valueTable = valueTable.AssertNonNull(nameof(valueTable));
            _currentNodeTableIdx = -1;
        }

        public KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>> Current { get; private set; }

        object IEnumerator.Current => Current;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            _currentNodeTableIdx++;
            while (_currentNodeTableIdx < _nodeTable.Length &&
                !_nodeTable[_currentNodeTableIdx].IsLeafNode)
            {
                _currentNodeTableIdx++;
            }

            if (_currentNodeTableIdx < _nodeTable.Length &&
                _nodeTable[_currentNodeTableIdx].IsLeafNode)
            {
                var node = _nodeTable[_currentNodeTableIdx];
                Current = new KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>>(
                    new InternedKey<ReadOnlyMemory<T>>(node.ValueOrdinal),
                    _valueTable.AsMemory(node.DataStart, node.DataLength));
                return true;
            }
            else
            {
                Current = default(KeyValuePair<InternedKey<ReadOnlyMemory<T>>, ReadOnlyMemory<T>>);
                return false;
            }
        }

        public void Reset()
        {
            _currentNodeTableIdx = -1;
        }
    }
}
