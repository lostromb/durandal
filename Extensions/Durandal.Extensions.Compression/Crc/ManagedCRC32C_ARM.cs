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

#if NETCOREAPP
using Durandal.Common.IO.Crc;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;

namespace Durandal.Extensions.Compression.Crc
{
    /// <summary>
    /// Managed version of CRC32C which takes advantage of ARM64 CRC32C extensions
    /// </summary>
    public class ManagedCRC32C_ARM : ICRC32C
    {
        private const int LONG_SHIFT_8 = 8192;
        private const int LONG_SHIFT_32 = LONG_SHIFT_8 / sizeof(uint);
        private const int SHORT_SHIFT_8 = 256;
        private const int SHORT_SHIFT_32 = SHORT_SHIFT_8 / sizeof(uint);

        /// <inheritdoc />
        public void Slurp(ref CRC32CState state, byte value)
        {
            state.Checksum = Crc32.ComputeCrc32C(state.Checksum ^ 0xffffffff, value) ^ 0xffffffff;
        }

        /// <inheritdoc />
        public void Slurp(ref CRC32CState state, ReadOnlySpan<byte> input_8)
        {
#if DEBUG
            if (!Crc32.IsSupported)
            {
                throw new PlatformNotSupportedException("Platform architecture does not support CRC32.ARM extension");
            }
#endif
            int end_idx_8;
            //buffer end;
            uint crc0, crc1, crc2;

            /* pre-process the crc */
            crc0 = state.Checksum ^ 0xffffffff;

            // align the input byte span to a 4-byte boundary for performance
            int offset = FastMath.Min(input_8.Length, BinaryHelpers.GetMemoryAlignment(input_8, sizeof(uint)));

            for (int misalignedByte = 0; misalignedByte < offset; misalignedByte++)
            {
                crc0 = Crc32.ComputeCrc32C((uint)crc0, input_8[misalignedByte]);
            }

            ReadOnlySpan<uint> input_32 = MemoryMarshal.Cast<byte, uint>(input_8.Slice(offset));
            int input_idx_32 = 0;
            int input_idx_8 = offset;
            int length = input_8.Length - offset;

            /* compute the crc on sets of LONG_SHIFT*3 bytes, executing three independent crc
            instructions, each on LONG_SHIFT bytes -- this has not been profiled on actual ARM hardware,
            it's just copying the pipelining that works best on Intel x64 */
            while (length >= 3 * LONG_SHIFT_8)
            {
                crc1 = 0;
                crc2 = 0;
                end_idx_8 = input_idx_8 + LONG_SHIFT_8;
                do
                {
                    crc0 = Crc32.ComputeCrc32C(crc0, input_32[input_idx_32]);
                    crc1 = Crc32.ComputeCrc32C(crc1, input_32[input_idx_32 + LONG_SHIFT_32]);
                    crc2 = Crc32.ComputeCrc32C(crc2, input_32[input_idx_32 + (2 * LONG_SHIFT_32)]);
                    input_idx_8 += sizeof(uint);
                    input_idx_32 += 1;
                } while (input_idx_8 < end_idx_8);
                crc0 = shift_crc(CrcTables.LongShifts32, (uint)(crc0)) ^ crc1;
                crc0 = shift_crc(CrcTables.LongShifts32, (uint)(crc0)) ^ crc2;
                input_idx_8 += (2 * LONG_SHIFT_8);
                input_idx_32 += (2 * LONG_SHIFT_32);
                length -= 3 * LONG_SHIFT_8;
            }

            /* do the same thing, but now on SHORT_SHIFT*3 blocks for the remaining data less
            than a LONG_SHIFT*3 block */
            while (length >= 3 * SHORT_SHIFT_8)
            {
                crc1 = 0;
                crc2 = 0;
                end_idx_8 = input_idx_8 + SHORT_SHIFT_8;
                do
                {
                    crc0 = Crc32.ComputeCrc32C(crc0, input_32[input_idx_32]);
                    crc1 = Crc32.ComputeCrc32C(crc1, input_32[input_idx_32 + SHORT_SHIFT_32]);
                    crc2 = Crc32.ComputeCrc32C(crc2, input_32[input_idx_32 + (2 * SHORT_SHIFT_32)]);
                    input_idx_8 += sizeof(uint);
                    input_idx_32 += 1;
                } while (input_idx_8 < end_idx_8);
                crc0 = shift_crc(CrcTables.ShortShifts32, (uint)(crc0)) ^ crc1;
                crc0 = shift_crc(CrcTables.ShortShifts32, (uint)(crc0)) ^ crc2;
                input_idx_8 += (2 * SHORT_SHIFT_8);
                input_idx_32 += (2 * SHORT_SHIFT_32);
                length -= 3 * SHORT_SHIFT_8;
            }

            /* compute the crc on the remaining four-byte units less than a SHORT_SHIFT*3
            block */
            end_idx_8 = input_idx_8 + (length - (length & 3));
            while (input_idx_8 < end_idx_8)
            {
                crc0 = Crc32.ComputeCrc32C(crc0, input_32[input_idx_32]);
                input_idx_8 += sizeof(uint);
                input_idx_32 += 1;
            }

            length &= 3;

            /* compute the crc for up to three trailing bytes */
            while (length != 0)
            {
                crc0 = Crc32.ComputeCrc32C(crc0, input_8[input_idx_8++]);
                --length;
            }

            /* return a post-processed crc */
            state.Checksum = crc0 ^ 0xffffffff;
        }

        /// <summary>
        /// Apply the zeros operator table to crc.
        /// </summary>
        /// <param name="shift_table">The shift table to use</param>
        /// <param name="crc">The current CRC</param>
        /// <returns>The shifted CRC</returns>
        private static uint shift_crc(uint[][] shift_table, uint crc)
        {
            return shift_table[0][crc & 0xff]
                ^ shift_table[1][(crc >> 8) & 0xff]
                ^ shift_table[2][(crc >> 16) & 0xff]
                ^ shift_table[3][crc >> 24];
        }
    }
}

#endif