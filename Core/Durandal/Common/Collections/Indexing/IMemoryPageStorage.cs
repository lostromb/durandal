using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Collections.Indexing
{
    public interface IMemoryPageStorage
    {
        uint Store(byte[] block);
        byte[] Retrieve(uint blockNum);

        /// <summary>
        /// Clears all data from this index
        /// </summary>
        void Clear();

        /// <summary>
        /// Returns the size of the indexed data (virtual size)
        /// </summary>
        long IndexSize { get; }

        /// <summary>
        /// Returns the actual memory footprint of the index, factoring in compression
        /// </summary>
        long MemoryUse { get; }

        /// <summary>
        /// Returns the current page compression ratio from 0 to 1 (1 = uncompressed)
        /// </summary>
        double CompressionRatio { get; }
    }
}
