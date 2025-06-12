using Durandal.Common.Utils;
using Durandal.Common.Cache;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Collections.Indexing
{
    /// <summary>
    /// Abstract class for page storage mechanisms which use a block transform (usually compression)
    /// and want the most recently used pages to be saved in an MFU cache.
    /// </summary>
    public abstract class CachedTransformMemoryPageStorage : IMemoryPageStorage
    {
        private readonly List<byte[]> _records = new List<byte[]>();
        private long _uncompressedSize = 0;
        private long _compressedSize = 0;
        private IReadThroughCache<uint, byte[]> _memCache;

        public CachedTransformMemoryPageStorage(int cacheSize = 20)
        {
            if (cacheSize > 0)
            {
                _memCache = new MFUCache<uint, byte[]>(RetrieveUncached, cacheSize);
            }
            else
            {
                _memCache = new NullCache<uint, byte[]>(Retrieve);
            }
        }

        private byte[] RetrieveUncached(uint key)
        {
            return Decompress(_records[(int)key]);
        }

        public long IndexSize
        {
            get
            {
                return _uncompressedSize;
            }
        }

        public long MemoryUse
        {
            get
            {
                int cachedBlockSize = 0;
                if (_records.Count > 0)
                {
                    cachedBlockSize = _memCache.ItemsCached * _records[0].Length;
                }

                return _compressedSize + cachedBlockSize;
            }
        }

        /// <summary>
        /// Returns the current compression ratio for pooled data
        /// </summary>
        public double CompressionRatio
        {
            get
            {
                if (_uncompressedSize == 0)
                {
                    return 1;
                }

                return MemoryUse / (double)_uncompressedSize;
            }
        }

        public void Clear()
        {
            _records.Clear();
            _memCache.Clear();
            _compressedSize = 0;
            _uncompressedSize = 0;
        }

        public byte[] Retrieve(uint blockNum)
        {
            if (blockNum < _records.Count)
            {
                return _memCache.GetCache(blockNum);
            }
            else
            {
                throw new ArgumentOutOfRangeException(string.Format("Attempted to retrieve nonexistent data at address {0}!", blockNum));
            }
        }

        public uint Store(byte[] block)
        {
            _uncompressedSize += block.Length;
            byte[] compressedData = Compress(block);
            _records.Add(compressedData);
            _compressedSize += compressedData.Length;
            return (uint)(_records.Count - 1);
        }

        protected abstract byte[] Compress(byte[] input);

        protected abstract byte[] Decompress(byte[] compressed);
    }
}
