using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.IO.Crc
{
    /// <summary>
    /// An abstract implementation of the CRC32C (Castagnoli) checksum algorithm.
    /// </summary>
    public interface ICRC32C
    {
        /// <summary>
        /// Processes a single byte and updates the checksum.
        /// </summary>
        /// <param name="state">The structure containing CRC32C state</param>
        /// <param name="value">The byte to ingest</param>
        void Slurp(ref CRC32CState state, byte value);

        /// <summary>
        /// Processes a span of bytes and updates the checksum.
        /// </summary>
        /// <param name="state">The structure containing CRC32C state</param>
        /// <param name="value">The span of bytes to ingest</param>
        void Slurp(ref CRC32CState state, ReadOnlySpan<byte> value);
    }
}
