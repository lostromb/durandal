/* Ported from CRC-32C library: https://crc32c.machinezoo.com/ */
/*
  Copyright (c) 2013 - 2014, 2016 Mark Adler, Robert Vazan, Max Vysokikh

  This software is provided 'as-is', without any express or implied
  warranty.  In no event will the author be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
  claim that you wrote the original software. If you use this software
  in a product, an acknowledgment in the product documentation would be
  appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
  misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.
*/

using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Runtime.InteropServices;

namespace Durandal.Common.IO.Crc
{
    /// <summary>
    /// Baseline managed implementation of CRC32C. Only supports little-endian hardware.
    /// Favors 32-bit architectures; if you are running 64-bit then you should use that variant.
    /// </summary>
    internal class ManagedCRC32C : ICRC32C
    {
        /// <inheritdoc />
        public void Slurp(ref CRC32CState state, byte value)
        {
            uint crc = state.Checksum ^ 0xffffffff;
            crc = CrcTables.CrcTable32[0][(crc ^ value) & 0xff] ^ (crc >> 8);
            state.Checksum = crc ^ 0xffffffff;
        }

        /// <inheritdoc />
        public void Slurp(ref CRC32CState state, ReadOnlySpan<byte> input_8)
        {
#if DEBUG
            if (!BitConverter.IsLittleEndian)
            {
                throw new PlatformNotSupportedException("Cannot use little-endian CRC algorithm on a big-endian platform");
            }
#endif
            uint crc = state.Checksum ^ 0xffffffff;

            // align the input byte span to a 4-byte boundary, both
            // for performance and to run on platforms that don't support unaligned access e.g. ARM
            int offset = FastMath.Min(input_8.Length, BinaryHelpers.GetMemoryAlignment(input_8, sizeof(uint)));

            for (int misalignedByte = 0; misalignedByte < offset; misalignedByte++)
            {
                crc = CrcTables.CrcTable32[0][(crc ^ input_8[misalignedByte]) & 0xff] ^ (crc >> 8);
            }

            ReadOnlySpan<uint> input_32 = MemoryMarshal.Cast<byte, uint>(input_8.Slice(offset));
            int input_idx_32 = 0;
            int input_idx_8 = offset;
            int length = input_8.Length - offset;

            while (length >= 3 * sizeof(uint))
            {
                crc ^= input_32[input_idx_32];
                uint high = input_32[input_idx_32 + 1];
                uint high2 = input_32[input_idx_32 + 2];
                crc = CrcTables.CrcTable32[11][crc & 0xff]
                    ^ CrcTables.CrcTable32[10][(crc >> 8) & 0xff]
                    ^ CrcTables.CrcTable32[9][(crc >> 16) & 0xff]
                    ^ CrcTables.CrcTable32[8][crc >> 24]
                    ^ CrcTables.CrcTable32[7][high & 0xff]
                    ^ CrcTables.CrcTable32[6][(high >> 8) & 0xff]
                    ^ CrcTables.CrcTable32[5][(high >> 16) & 0xff]
                    ^ CrcTables.CrcTable32[4][high >> 24]
                    ^ CrcTables.CrcTable32[3][high2 & 0xff]
                    ^ CrcTables.CrcTable32[2][(high2 >> 8) & 0xff]
                    ^ CrcTables.CrcTable32[1][(high2 >> 16) & 0xff]
                    ^ CrcTables.CrcTable32[0][high2 >> 24];
                input_idx_32 += 3;
                input_idx_8 += 3 * sizeof(uint);
                length -= 3 * sizeof(uint);
            }

            while (length != 0)
            {
                crc = CrcTables.CrcTable32[0][(crc ^ input_8[input_idx_8++]) & 0xff] ^ (crc >> 8);
                --length;
            }

            state.Checksum = crc ^ 0xffffffff;
        }
    }
}
