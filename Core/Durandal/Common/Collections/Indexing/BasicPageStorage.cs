using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Collections.Indexing
{
    public class BasicPageStorage : IMemoryPageStorage
    {
        private readonly List<byte[]> _records = new List<byte[]>();
        private long _dataSize = 0;

        public BasicPageStorage()
        {
        }

        public long IndexSize
        {
            get
            {
                return _dataSize;
            }
        }

        public long MemoryUse
        {
            get
            {
                return _dataSize;
            }
        }

        public void Clear()
        {
            _records.Clear();
        }

        public byte[] Retrieve(uint blockNum)
        {
            if (blockNum < _records.Count)
            {
                return _records[(int)blockNum];
            }
            else
            {
                throw new ArgumentOutOfRangeException(string.Format("Attempted to retrieve nonexistent data at address {0}!", blockNum));
            }
        }

        public uint Store(byte[] block)
        {
            _dataSize += block.Length;
            _records.Add(block);
            return (uint)(_records.Count - 1);
        }

        /// <summary>
        /// Returns the current compression ratio for pooled data
        /// </summary>
        public double CompressionRatio
        {
            get
            {
                return 1.0;
            }
        }
    }
}
