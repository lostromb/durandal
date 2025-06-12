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
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Durandal.Extensions.Compression.Crc
{
    /// <summary>
    /// Managed version of CRC32C which takes advantage of 64-bit SSE4.2 CRC32 intrinsics.
    /// Still only about half the performance of C++ code, but it doesn't require P/invoke
    /// or unsafe code, and could theoretically run on any x86 operating system.
    /// </summary>
    public class ManagedCRC32C_SSE_X64 : ICRC32C
    {
        private const int LONG_SHIFT_8 = 8192;
        private const int LONG_SHIFT_64 = LONG_SHIFT_8 / sizeof(ulong);
        private const int SHORT_SHIFT_8 = 256;
        private const int SHORT_SHIFT_64 = SHORT_SHIFT_8 / sizeof(ulong);

        /// <inheritdoc />
        public void Slurp(ref CRC32CState state, byte value)
        {
            state.Checksum = Sse42.Crc32(state.Checksum ^ 0xffffffff, value) ^ 0xffffffff;
        }

        /// <inheritdoc />
        public void Slurp(ref CRC32CState state, ReadOnlySpan<byte> input_8)
        {
#if DEBUG
            if (!Sse42.X64.IsSupported)
            {
                throw new PlatformNotSupportedException("Platform architecture does not support CRC32.X64 extension");
            }
#endif
            int end_idx_8;
            ulong crc0, crc1, crc2;

            /* pre-process the crc */
            crc0 = state.Checksum ^ 0xffffffff;

            // align the input byte span to an 8-byte boundary for performance
            int offset = FastMath.Min(input_8.Length, BinaryHelpers.GetMemoryAlignment(input_8, sizeof(ulong)));

            for (int misalignedByte = 0; misalignedByte < offset; misalignedByte++)
            {
                crc0 = Sse42.Crc32((uint)crc0, input_8[misalignedByte]);
            }

            ReadOnlySpan<ulong> input_64 = MemoryMarshal.Cast<byte, ulong>(input_8.Slice(offset));
            int input_idx_64 = 0;
            int input_idx_8 = offset;
            int length = input_8.Length - offset;

            /* compute the crc on sets of LONG_SHIFT*3 bytes, executing three independent crc
            instructions, each on LONG_SHIFT bytes -- this is optimized for the Nehalem,
            Westmere, Sandy Bridge, and Ivy Bridge architectures, which have a
            throughput of one crc per cycle, but a latency of three cycles */
            while (length >= 3 * LONG_SHIFT_8)
            {
                crc1 = 0;
                crc2 = 0;
                end_idx_8 = input_idx_8 + LONG_SHIFT_8;
                do
                {
                    crc0 = Sse42.X64.Crc32(crc0, input_64[input_idx_64]);
                    crc1 = Sse42.X64.Crc32(crc1, input_64[input_idx_64 + LONG_SHIFT_64]);
                    crc2 = Sse42.X64.Crc32(crc2, input_64[input_idx_64 + (2 * LONG_SHIFT_64)]);
                    input_idx_8 += sizeof(ulong);
                    input_idx_64 += 1;
                } while (input_idx_8 < end_idx_8);
                crc0 = shift_crc(CrcTables.LongShifts32, (uint)crc0) ^ crc1;
                crc0 = shift_crc(CrcTables.LongShifts32, (uint)crc0) ^ crc2;
                input_idx_8 += (2 * LONG_SHIFT_8);
                input_idx_64 += (2 * LONG_SHIFT_64);
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
                    crc0 = Sse42.X64.Crc32(crc0, input_64[input_idx_64]);
                    crc1 = Sse42.X64.Crc32(crc1, input_64[input_idx_64 + SHORT_SHIFT_64]);
                    crc2 = Sse42.X64.Crc32(crc2, input_64[input_idx_64 + (2 * SHORT_SHIFT_64)]);
                    input_idx_8 += sizeof(ulong);
                    input_idx_64 += 1;
                } while (input_idx_8 < end_idx_8);
                crc0 = shift_crc(CrcTables.ShortShifts32, (uint)crc0) ^ crc1;
                crc0 = shift_crc(CrcTables.ShortShifts32, (uint)crc0) ^ crc2;
                input_idx_8 += (2 * SHORT_SHIFT_8);
                input_idx_64 += (2 * SHORT_SHIFT_64);
                length -= 3 * SHORT_SHIFT_8;
            }

            /* compute the crc on the remaining eight-byte units less than a SHORT_SHIFT*3
            block */
            end_idx_8 = input_idx_8 + (length - (length & 7));
            while (input_idx_8 < end_idx_8)
            {
                crc0 = Sse42.X64.Crc32(crc0, input_64[input_idx_64]);
                input_idx_8 += sizeof(ulong);
                input_idx_64 += 1;
            }

            length &= 7;

            /* compute the crc for up to seven trailing bytes */
            while (length != 0)
            {
                crc0 = Sse42.Crc32((uint)crc0, input_8[input_idx_8++]);
                --length;
            }

            /* return a post-processed crc */
            state.Checksum = (uint)(crc0 ^ 0xffffffff);
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