using Durandal.Common.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Collections.Interning.Impl
{
    internal class StringBinaryTreeEnumerator : IEnumerator<KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>>
    {
        private readonly BinaryTreeNode[] _nodeTable;
        private readonly string[] _valueTable;
        private int _currentNodeTableIdx;

        public StringBinaryTreeEnumerator(BinaryTreeNode[] nodeTable, string[] valueTable)
        {
            _nodeTable = nodeTable.AssertNonNull(nameof(nodeTable));
            _valueTable = valueTable.AssertNonNull(nameof(valueTable));
            _currentNodeTableIdx = -1;
        }

        public KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>> Current { get; private set; }

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
                Current = new KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>(
                    new InternedKey<ReadOnlyMemory<char>>(node.ValueOrdinal),
                    _valueTable[node.DataStart].AsMemory());
                return true;
            }
            else
            {
                Current = default(KeyValuePair<InternedKey<ReadOnlyMemory<char>>, ReadOnlyMemory<char>>);
                return false;
            }
        }

        public void Reset()
        {
            _currentNodeTableIdx = -1;
        }
    }
}
