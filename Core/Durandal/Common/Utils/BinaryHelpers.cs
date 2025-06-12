using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// Provides helper methods for converting binary numbers and byte arrays, with varying endianness.
    /// As a side note, the default <see cref="BitConverter"/> methods use the machine's native endianness, which can lead to some variation
    /// </summary>
    public static class BinaryHelpers
    {
        /// <summary>
        /// Static reference to an empty byte[0] array; used to reduce allocations.
        /// </summary>
        public static readonly byte[] EMPTY_BYTE_ARRAY = new byte[0];

        public static readonly float[] EMPTY_FLOAT_ARRAY = new float[0];

        public static readonly int[] EMPTY_INT_ARRAY = new int[0];

        // this only exists to sit here and be properly word-aligned in memory.
        private static readonly long[] OFFSET_CHECK = new long[1];

        static BinaryHelpers()
        {
            SupportsUnalignedMemoryAccess = false;
            try
            {
                int[] field32 = new int[2];
                Span<byte> field8 = MemoryMarshal.Cast<int, byte>(field32.AsSpan());
                Span<int> unalignedField = MemoryMarshal.Cast<byte, int>(field8.Slice(2, sizeof(int)));
                unalignedField[0] = 10;
                SupportsUnalignedMemoryAccess = unalignedField[0] == 10;
            }
            catch (Exception) { }

            SizeOfIntPtr = Unsafe.SizeOf<IntPtr>();
        }

        /// <summary>
        /// Indicates whether the current runtime architecture supports unaligned
        /// memory access, most commonly encountered with MemoryMarshal.Cast()
        /// operations. These operations can "randomly" fail on some platforms
        /// like ARM if the data happens to be misaligned.
        /// </summary>
        public static bool SupportsUnalignedMemoryAccess { get; private set; }

        /// <summary>
        /// Gets the size of a native <see cref="IntPtr"/> in the current runtime architecture.
        /// Either 4 or 8 bytes return value.
        /// </summary>
        public static int SizeOfIntPtr { get; private set; }

        public static void Int16ToByteArrayLittleEndian(short val, byte[] target, int targetOffset)
        {
            UInt16ToByteArrayLittleEndian((ushort)val, target, targetOffset);
        }

        public static void UInt16ToByteArrayLittleEndian(ushort val, byte[] target, int targetOffset)
        {
            target[targetOffset + 1] = (byte)(val >> 8 & 0xFF);
            target[targetOffset + 0] = (byte)(val >> 0 & 0xFF);
        }

        public static void Int16ToByteSpanLittleEndian(short val, ref Span<byte> target)
        {
            UInt16ToByteArraySpanEndian((ushort)val, ref target);
        }

        public static void UInt16ToByteArraySpanEndian(ushort val, ref Span<byte> target)
        {
            target[1] = (byte)(val >> 8 & 0xFF);
            target[0] = (byte)(val >> 0 & 0xFF);
        }

        public static void Int16ToByteArrayBigEndian(short val, byte[] target, int targetOffset)
        {
            UInt16ToByteArrayBigEndian((ushort)val, target, targetOffset);
        }

        public static void UInt16ToByteArrayBigEndian(ushort val, byte[] target, int targetOffset)
        {
            target[targetOffset + 0] = (byte)(val >> 8 & 0xFF);
            target[targetOffset + 1] = (byte)(val >> 0 & 0xFF);
        }

        public static void Int16ToByteSpanBigEndian(short val, ref Span<byte> target)
        {
            UInt16ToByteSpanBigEndian((ushort)val, ref target);
        }

        public static void UInt16ToByteSpanBigEndian(ushort val, ref Span<byte> target)
        {
            target[0] = (byte)(val >> 8 & 0xFF);
            target[1] = (byte)(val >> 0 & 0xFF);
        }

        public static void Int32ToByteArrayLittleEndian(int val, byte[] target, int targetOffset)
        {
            UInt32ToByteArrayLittleEndian((uint)val, target, targetOffset);
        }

        public static void UInt32ToByteArrayLittleEndian(uint val, byte[] target, int targetOffset)
        {
            target[targetOffset + 3] = (byte)(val >> 24 & 0xFF);
            target[targetOffset + 2] = (byte)(val >> 16 & 0xFF);
            target[targetOffset + 1] = (byte)(val >> 8 & 0xFF);
            target[targetOffset + 0] = (byte)(val >> 0 & 0xFF);
        }

        public static void Int32ToByteSpanLittleEndian(int val, ref Span<byte> target)
        {
            UInt32ToByteSpanLittleEndian((uint)val, ref target);
        }

        public static void UInt32ToByteSpanLittleEndian(uint val, ref Span<byte> target)
        {
            target[3] = (byte)(val >> 24 & 0xFF);
            target[2] = (byte)(val >> 16 & 0xFF);
            target[1] = (byte)(val >> 8 & 0xFF);
            target[0] = (byte)(val >> 0 & 0xFF);
        }

        public static void UInt24ToByteArrayBigEndian(uint val, byte[] target, int targetOffset)
        {
            target[targetOffset + 0] = (byte)(val >> 16 & 0xFF);
            target[targetOffset + 1] = (byte)(val >> 8 & 0xFF);
            target[targetOffset + 2] = (byte)(val >> 0 & 0xFF);
        }

        public static void UInt24ToByteSpanBigEndian(uint val, ref Span<byte> target)
        {
            target[0] = (byte)(val >> 16 & 0xFF);
            target[1] = (byte)(val >> 8 & 0xFF);
            target[2] = (byte)(val >> 0 & 0xFF);
        }

        public static void Int32ToByteArrayBigEndian(int val, byte[] target, int targetOffset)
        {
            UInt32ToByteArrayBigEndian((uint)val, target, targetOffset);
        }

        public static void UInt32ToByteArrayBigEndian(uint val, byte[] target, int targetOffset)
        {
            target[targetOffset + 0] = (byte)(val >> 24 & 0xFF);
            target[targetOffset + 1] = (byte)(val >> 16 & 0xFF);
            target[targetOffset + 2] = (byte)(val >> 8 & 0xFF);
            target[targetOffset + 3] = (byte)(val >> 0 & 0xFF);
        }

        public static void Int32ToByteSpanBigEndian(int val, ref Span<byte> target)
        {
            UInt32ToByteSpanBigEndian((uint)val, ref target);
        }

        public static void UInt32ToByteSpanBigEndian(uint val, ref Span<byte> target)
        {
            target[0] = (byte)(val >> 24 & 0xFF);
            target[1] = (byte)(val >> 16 & 0xFF);
            target[2] = (byte)(val >> 8 & 0xFF);
            target[3] = (byte)(val >> 0 & 0xFF);
        }

        public static void Int64ToByteArrayLittleEndian(long val, byte[] target, int targetOffset)
        {
            UInt64ToByteArrayLittleEndian((ulong)val, target, targetOffset);
        }

        public static void UInt64ToByteArrayLittleEndian(ulong val, byte[] target, int targetOffset)
        {
            target[targetOffset + 7] = (byte)(val >> 56 & 0xFF);
            target[targetOffset + 6] = (byte)(val >> 48 & 0xFF);
            target[targetOffset + 5] = (byte)(val >> 40 & 0xFF);
            target[targetOffset + 4] = (byte)(val >> 32 & 0xFF);
            target[targetOffset + 3] = (byte)(val >> 24 & 0xFF);
            target[targetOffset + 2] = (byte)(val >> 16 & 0xFF);
            target[targetOffset + 1] = (byte)(val >> 8 & 0xFF);
            target[targetOffset + 0] = (byte)(val >> 0 & 0xFF);
        }

        public static void Int64ToByteSpanLittleEndian(long val, ref Span<byte> target)
        {
            UInt64ToByteSpanLittleEndian((ulong)val, ref target);
        }

        public static void UInt64ToByteSpanLittleEndian(ulong val, ref Span<byte> target)
        {
            target[7] = (byte)(val >> 56 & 0xFF);
            target[6] = (byte)(val >> 48 & 0xFF);
            target[5] = (byte)(val >> 40 & 0xFF);
            target[4] = (byte)(val >> 32 & 0xFF);
            target[3] = (byte)(val >> 24 & 0xFF);
            target[2] = (byte)(val >> 16 & 0xFF);
            target[1] = (byte)(val >> 8 & 0xFF);
            target[0] = (byte)(val >> 0 & 0xFF);
        }

        public static void Int64ToByteArrayBigEndian(long val, byte[] target, int targetOffset)
        {
            UInt64ToByteArrayBigEndian((ulong)val, target, targetOffset);
        }

        public static void UInt64ToByteArrayBigEndian(ulong val, byte[] target, int targetOffset)
        {
            target[targetOffset + 0] = (byte)(val >> 56 & 0xFF);
            target[targetOffset + 1] = (byte)(val >> 48 & 0xFF);
            target[targetOffset + 2] = (byte)(val >> 40 & 0xFF);
            target[targetOffset + 3] = (byte)(val >> 32 & 0xFF);
            target[targetOffset + 4] = (byte)(val >> 24 & 0xFF);
            target[targetOffset + 5] = (byte)(val >> 16 & 0xFF);
            target[targetOffset + 6] = (byte)(val >> 8 & 0xFF);
            target[targetOffset + 7] = (byte)(val >> 0 & 0xFF);
        }

        public static void Int64ToByteSpanBigEndian(long val, ref Span<byte> target)
        {
            UInt64ToByteSpanBigEndian((ulong)val, ref target);
        }

        public static void UInt64ToByteSpanBigEndian(ulong val, ref Span<byte> target)
        {
            target[0] = (byte)(val >> 56 & 0xFF);
            target[1] = (byte)(val >> 48 & 0xFF);
            target[2] = (byte)(val >> 40 & 0xFF);
            target[3] = (byte)(val >> 32 & 0xFF);
            target[4] = (byte)(val >> 24 & 0xFF);
            target[5] = (byte)(val >> 16 & 0xFF);
            target[6] = (byte)(val >> 8 & 0xFF);
            target[7] = (byte)(val >> 0 & 0xFF);
        }

        public static short ByteArrayToInt16LittleEndian(byte[] source, int offset)
        {
            short returnVal = 0;
            returnVal |= (short)(source[offset + 1] << 8);
            returnVal |= (short)(source[offset + 0] << 0);
            return returnVal;
        }

        public static short ByteSpanToInt16LittleEndian(ref Span<byte> source)
        {
            short returnVal = 0;
            returnVal |= (short)(source[1] << 8);
            returnVal |= (short)(source[0] << 0);
            return returnVal;
        }

        public static short ByteSpanToInt16LittleEndian(ref ReadOnlySpan<byte> source)
        {
            short returnVal = 0;
            returnVal |= (short)(source[1] << 8);
            returnVal |= (short)(source[0] << 0);
            return returnVal;
        }

        public static ushort ByteArrayToUInt16LittleEndian(byte[] source, int offset)
        {
            ushort returnVal = 0;
            returnVal |= (ushort)(source[offset + 1] << 8);
            returnVal |= (ushort)(source[offset + 0] << 0);
            return returnVal;
        }

        public static ushort ByteSpanToUInt16LittleEndian(ref Span<byte> source)
        {
            ushort returnVal = 0;
            returnVal |= (ushort)(source[1] << 8);
            returnVal |= (ushort)(source[0] << 0);
            return returnVal;
        }

        public static ushort ByteSpanToUInt16LittleEndian(ref ReadOnlySpan<byte> source)
        {
            ushort returnVal = 0;
            returnVal |= (ushort)(source[1] << 8);
            returnVal |= (ushort)(source[0] << 0);
            return returnVal;
        }

        public static short ByteArrayToInt16BigEndian(byte[] source, int offset)
        {
            short returnVal = 0;
            returnVal |= (short)(source[offset + 0] << 8);
            returnVal |= (short)(source[offset + 1] << 0);
            return returnVal;
        }

        public static short ByteSpanToInt16BigEndian(ref Span<byte> source)
        {
            short returnVal = 0;
            returnVal |= (short)(source[0] << 8);
            returnVal |= (short)(source[1] << 0);
            return returnVal;
        }

        public static short ByteSpanToInt16BigEndian(ref ReadOnlySpan<byte> source)
        {
            short returnVal = 0;
            returnVal |= (short)(source[0] << 8);
            returnVal |= (short)(source[1] << 0);
            return returnVal;
        }

        public static ushort ByteArrayToUInt16BigEndian(byte[] source, int offset)
        {
            ushort returnVal = 0;
            returnVal |= (ushort)(source[offset + 0] << 8);
            returnVal |= (ushort)(source[offset + 1] << 0);
            return returnVal;
        }

        public static ushort ByteSpanToUInt16BigEndian(ref Span<byte> source)
        {
            ushort returnVal = 0;
            returnVal |= (ushort)(source[0] << 8);
            returnVal |= (ushort)(source[1] << 0);
            return returnVal;
        }

        public static ushort ByteSpanToUInt16BigEndian(ref ReadOnlySpan<byte> source)
        {
            ushort returnVal = 0;
            returnVal |= (ushort)(source[0] << 8);
            returnVal |= (ushort)(source[1] << 0);
            return returnVal;
        }

        public static int ByteArrayToInt32LittleEndian(byte[] source, int offset)
        {
            int returnVal = 0;
            returnVal |= (int)source[offset + 3] << 24;
            returnVal |= (int)source[offset + 2] << 16;
            returnVal |= (int)source[offset + 1] << 8;
            returnVal |= (int)source[offset + 0] << 0;
            return returnVal;
        }

        public static int ByteSpanToInt32LittleEndian(ref Span<byte> source)
        {
            int returnVal = 0;
            returnVal |= (int)source[3] << 24;
            returnVal |= (int)source[2] << 16;
            returnVal |= (int)source[1] << 8;
            returnVal |= (int)source[0] << 0;
            return returnVal;
        }

        public static int ByteSpanToInt32LittleEndian(ref ReadOnlySpan<byte> source)
        {
            int returnVal = 0;
            returnVal |= (int)source[3] << 24;
            returnVal |= (int)source[2] << 16;
            returnVal |= (int)source[1] << 8;
            returnVal |= (int)source[0] << 0;
            return returnVal;
        }

        public static uint ByteArrayToUInt32LittleEndian(byte[] source, int offset)
        {
            uint returnVal = 0;
            returnVal |= (uint)source[offset + 3] << 24;
            returnVal |= (uint)source[offset + 2] << 16;
            returnVal |= (uint)source[offset + 1] << 8;
            returnVal |= (uint)source[offset + 0] << 0;
            return returnVal;
        }

        public static uint ByteSpanToUInt32LittleEndian(ref Span<byte> source)
        {
            uint returnVal = 0;
            returnVal |= (uint)source[3] << 24;
            returnVal |= (uint)source[2] << 16;
            returnVal |= (uint)source[1] << 8;
            returnVal |= (uint)source[0] << 0;
            return returnVal;
        }

        public static uint ByteSpanToUInt32LittleEndian(ref ReadOnlySpan<byte> source)
        {
            uint returnVal = 0;
            returnVal |= (uint)source[3] << 24;
            returnVal |= (uint)source[2] << 16;
            returnVal |= (uint)source[1] << 8;
            returnVal |= (uint)source[0] << 0;
            return returnVal;
        }

        public static uint ByteArrayToUInt24BigEndian(byte[] source, int offset)
        {
            uint returnVal = 0;
            returnVal |= (uint)source[offset + 0] << 16;
            returnVal |= (uint)source[offset + 1] << 8;
            returnVal |= (uint)source[offset + 2] << 0;
            return returnVal;
        }

        public static uint ByteSpanToUInt24BigEndian(ref Span<byte> source)
        {
            uint returnVal = 0;
            returnVal |= (uint)source[0] << 16;
            returnVal |= (uint)source[1] << 8;
            returnVal |= (uint)source[2] << 0;
            return returnVal;
        }

        public static uint ByteSpanToUInt24BigEndian(ref ReadOnlySpan<byte> source)
        {
            uint returnVal = 0;
            returnVal |= (uint)source[0] << 16;
            returnVal |= (uint)source[1] << 8;
            returnVal |= (uint)source[2] << 0;
            return returnVal;
        }

        public static int ByteArrayToInt32BigEndian(byte[] source, int offset)
        {
            int returnVal = 0;
            returnVal |= (int)source[offset + 0] << 24;
            returnVal |= (int)source[offset + 1] << 16;
            returnVal |= (int)source[offset + 2] << 8;
            returnVal |= (int)source[offset + 3] << 0;
            return returnVal;
        }

        public static int ByteSpanToInt32BigEndian(ref Span<byte> source)
        {
            int returnVal = 0;
            returnVal |= (int)source[0] << 24;
            returnVal |= (int)source[1] << 16;
            returnVal |= (int)source[2] << 8;
            returnVal |= (int)source[3] << 0;
            return returnVal;
        }

        public static int ByteSpanToInt32BigEndian(ref ReadOnlySpan<byte> source)
        {
            int returnVal = 0;
            returnVal |= (int)source[0] << 24;
            returnVal |= (int)source[1] << 16;
            returnVal |= (int)source[2] << 8;
            returnVal |= (int)source[3] << 0;
            return returnVal;
        }

        public static uint ByteArrayToUInt32BigEndian(byte[] source, int offset)
        {
            uint returnVal = 0;
            returnVal |= (uint)source[offset + 0] << 24;
            returnVal |= (uint)source[offset + 1] << 16;
            returnVal |= (uint)source[offset + 2] << 8;
            returnVal |= (uint)source[offset + 3] << 0;
            return returnVal;
        }

        public static uint ByteSpanToUInt32BigEndian(ref Span<byte> source)
        {
            uint returnVal = 0;
            returnVal |= (uint)source[0] << 24;
            returnVal |= (uint)source[1] << 16;
            returnVal |= (uint)source[2] << 8;
            returnVal |= (uint)source[3] << 0;
            return returnVal;
        }

        public static uint ByteSpanToUInt32BigEndian(ref ReadOnlySpan<byte> source)
        {
            uint returnVal = 0;
            returnVal |= (uint)source[0] << 24;
            returnVal |= (uint)source[1] << 16;
            returnVal |= (uint)source[2] << 8;
            returnVal |= (uint)source[3] << 0;
            return returnVal;
        }

        public static long ByteArrayToInt64LittleEndian(byte[] source, int offset)
        {
            long returnVal = 0;
            returnVal |= (long)source[offset + 7] << 56;
            returnVal |= (long)source[offset + 6] << 48;
            returnVal |= (long)source[offset + 5] << 40;
            returnVal |= (long)source[offset + 4] << 32;
            returnVal |= (long)source[offset + 3] << 24;
            returnVal |= (long)source[offset + 2] << 16;
            returnVal |= (long)source[offset + 1] << 8;
            returnVal |= (long)source[offset + 0] << 0;
            return returnVal;
        }

        public static long ByteSpanToInt64LittleEndian(ref Span<byte> source)
        {
            long returnVal = 0;
            returnVal |= (long)source[7] << 56;
            returnVal |= (long)source[6] << 48;
            returnVal |= (long)source[5] << 40;
            returnVal |= (long)source[4] << 32;
            returnVal |= (long)source[3] << 24;
            returnVal |= (long)source[2] << 16;
            returnVal |= (long)source[1] << 8;
            returnVal |= (long)source[0] << 0;
            return returnVal;
        }

        public static long ByteSpanToInt64LittleEndian(ref ReadOnlySpan<byte> source)
        {
            long returnVal = 0;
            returnVal |= (long)source[7] << 56;
            returnVal |= (long)source[6] << 48;
            returnVal |= (long)source[5] << 40;
            returnVal |= (long)source[4] << 32;
            returnVal |= (long)source[3] << 24;
            returnVal |= (long)source[2] << 16;
            returnVal |= (long)source[1] << 8;
            returnVal |= (long)source[0] << 0;
            return returnVal;
        }

        public static ulong ByteArrayToUInt64LittleEndian(byte[] source, int offset)
        {
            ulong returnVal = 0;
            returnVal |= (ulong)source[offset + 7] << 56;
            returnVal |= (ulong)source[offset + 6] << 48;
            returnVal |= (ulong)source[offset + 5] << 40;
            returnVal |= (ulong)source[offset + 4] << 32;
            returnVal |= (ulong)source[offset + 3] << 24;
            returnVal |= (ulong)source[offset + 2] << 16;
            returnVal |= (ulong)source[offset + 1] << 8;
            returnVal |= (ulong)source[offset + 0] << 0;
            return returnVal;
        }

        public static ulong ByteSpanToUInt64LittleEndian(ref Span<byte> source)
        {
            ulong returnVal = 0;
            returnVal |= (ulong)source[7] << 56;
            returnVal |= (ulong)source[6] << 48;
            returnVal |= (ulong)source[5] << 40;
            returnVal |= (ulong)source[4] << 32;
            returnVal |= (ulong)source[3] << 24;
            returnVal |= (ulong)source[2] << 16;
            returnVal |= (ulong)source[1] << 8;
            returnVal |= (ulong)source[0] << 0;
            return returnVal;
        }

        public static ulong ByteSpanToUInt64LittleEndian(ref ReadOnlySpan<byte> source)
        {
            ulong returnVal = 0;
            returnVal |= (ulong)source[7] << 56;
            returnVal |= (ulong)source[6] << 48;
            returnVal |= (ulong)source[5] << 40;
            returnVal |= (ulong)source[4] << 32;
            returnVal |= (ulong)source[3] << 24;
            returnVal |= (ulong)source[2] << 16;
            returnVal |= (ulong)source[1] << 8;
            returnVal |= (ulong)source[0] << 0;
            return returnVal;
        }

        public static long ByteArrayToInt64BigEndian(byte[] source, int offset)
        {
            long returnVal = 0;
            returnVal |= (long)source[offset + 0] << 56;
            returnVal |= (long)source[offset + 1] << 48;
            returnVal |= (long)source[offset + 2] << 40;
            returnVal |= (long)source[offset + 3] << 32;
            returnVal |= (long)source[offset + 4] << 24;
            returnVal |= (long)source[offset + 5] << 16;
            returnVal |= (long)source[offset + 6] << 8;
            returnVal |= (long)source[offset + 7] << 0;
            return returnVal;
        }

        public static long ByteSpanToInt64BigEndian(ref Span<byte> source)
        {
            long returnVal = 0;
            returnVal |= (long)source[0] << 56;
            returnVal |= (long)source[1] << 48;
            returnVal |= (long)source[2] << 40;
            returnVal |= (long)source[3] << 32;
            returnVal |= (long)source[4] << 24;
            returnVal |= (long)source[5] << 16;
            returnVal |= (long)source[6] << 8;
            returnVal |= (long)source[7] << 0;
            return returnVal;
        }

        public static long ByteSpanToInt64BigEndian(ref ReadOnlySpan<byte> source)
        {
            long returnVal = 0;
            returnVal |= (long)source[0] << 56;
            returnVal |= (long)source[1] << 48;
            returnVal |= (long)source[2] << 40;
            returnVal |= (long)source[3] << 32;
            returnVal |= (long)source[4] << 24;
            returnVal |= (long)source[5] << 16;
            returnVal |= (long)source[6] << 8;
            returnVal |= (long)source[7] << 0;
            return returnVal;
        }

        public static ulong ByteArrayToUInt64BigEndian(byte[] source, int offset)
        {
            ulong returnVal = 0;
            returnVal |= (ulong)source[offset + 0] << 56;
            returnVal |= (ulong)source[offset + 1] << 48;
            returnVal |= (ulong)source[offset + 2] << 40;
            returnVal |= (ulong)source[offset + 3] << 32;
            returnVal |= (ulong)source[offset + 4] << 24;
            returnVal |= (ulong)source[offset + 5] << 16;
            returnVal |= (ulong)source[offset + 6] << 8;
            returnVal |= (ulong)source[offset + 7] << 0;
            return returnVal;
        }

        public static ulong ByteSpanToUInt64BigEndian(ref Span<byte> source)
        {
            ulong returnVal = 0;
            returnVal |= (ulong)source[0] << 56;
            returnVal |= (ulong)source[1] << 48;
            returnVal |= (ulong)source[2] << 40;
            returnVal |= (ulong)source[3] << 32;
            returnVal |= (ulong)source[4] << 24;
            returnVal |= (ulong)source[5] << 16;
            returnVal |= (ulong)source[6] << 8;
            returnVal |= (ulong)source[7] << 0;
            return returnVal;
        }

        public static ulong ByteSpanToUInt64BigEndian(ref ReadOnlySpan<byte> source)
        {
            ulong returnVal = 0;
            returnVal |= (ulong)source[0] << 56;
            returnVal |= (ulong)source[1] << 48;
            returnVal |= (ulong)source[2] << 40;
            returnVal |= (ulong)source[3] << 32;
            returnVal |= (ulong)source[4] << 24;
            returnVal |= (ulong)source[5] << 16;
            returnVal |= (ulong)source[6] << 8;
            returnVal |= (ulong)source[7] << 0;
            return returnVal;
        }

        public static decimal ByteArrayToDecimal(byte[] source, int offset)
        {
            // uses machine endianness here
            return MemoryMarshal.Read<decimal>(source.AsSpan(offset, sizeof(decimal)));
        }

        /// <summary>
        /// Endianness is really vague in 128-bit floats (and the underlying runtime is probably
        /// using "double-double" as a shortcut), so this ordering of these bytes should be considered
        /// arbitrary and not guaranteed to transfer to different machine architectures.
        /// </summary>
        /// <param name="val"></param>
        /// <param name="target"></param>
        /// <param name="targetOffset"></param>
        public static void DecimalToByteArray(decimal val, byte[] target, int targetOffset)
        {
            MemoryMarshal.Write(target.AsSpan(targetOffset, sizeof(decimal)), ref val);

            //Span<decimal> box = stackalloc decimal[1];
            //Span<byte> castBytes = MemoryMarshal.Cast<decimal, byte>(box);
            //box[0] = val;
            //castBytes.CopyTo(target.AsSpan(targetOffset));
        }

        public static void DoubleToByteArrayBigEndian(double val, byte[] target, int targetOffset)
        {
            UInt64ToByteArrayBigEndian(new DoubleLayout { doubleValue = val }.ulongValue, target, targetOffset);
        }

        public static void DoubleToByteArrayLittleEndian(double val, byte[] target, int targetOffset)
        {
            UInt64ToByteArrayLittleEndian(new DoubleLayout { doubleValue = val }.ulongValue, target, targetOffset);
        }

        public static double ByteArrayToDoubleLittleEndian(byte[] source, int offset)
        {
            return new DoubleLayout { ulongValue = ByteArrayToUInt64LittleEndian(source, offset) }.doubleValue;
        }

        public static double ByteArrayToDoubleBigEndian(byte[] source, int offset)
        {
            return new DoubleLayout { ulongValue = ByteArrayToUInt64BigEndian(source, offset) }.doubleValue;
        }

        public static void FloatToByteArrayBigEndian(float val, byte[] target, int targetOffset)
        {
            UInt32ToByteArrayBigEndian(new FloatLayout { floatValue = val }.uintValue, target, targetOffset);
        }

        public static void FloatToByteArrayLittleEndian(float val, byte[] target, int targetOffset)
        {
            UInt32ToByteArrayLittleEndian(new FloatLayout { floatValue = val }.uintValue, target, targetOffset);
        }

        public static uint Float32ToPlatformUInt32(float input)
        {
            return new FloatLayout { floatValue = input }.uintValue;
        }

        public static float UInt32ToPlatformFloat32(uint input)
        {
            return new FloatLayout { uintValue = input }.floatValue;
        }

        public static float ByteArrayToFloatLittleEndian(byte[] source, int offset)
        {
            return new FloatLayout { uintValue = ByteArrayToUInt32LittleEndian(source, offset) }.floatValue;
        }

        public static float ByteArrayToFloatBigEndian(byte[] source, int offset)
        {
            return new FloatLayout { uintValue = ByteArrayToUInt32BigEndian(source, offset)}.floatValue;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatLayout
        {
            [FieldOffset(0)]
            public uint uintValue;

            [FieldOffset(0)]
            public float floatValue;
        }

        // We assume here for most processors that the endianness of integers
        // and floating point is the same, so even on a big-endian architecture,
        // both of the values will be swapped uniformly. But I can't really
        // find any documentation to support this assumption...
        [StructLayout(LayoutKind.Explicit)]
        private struct DoubleLayout
        {
            [FieldOffset(0)]
            public ulong ulongValue;

            [FieldOffset(0)]
            public double doubleValue;
        }

        /// <summary>
        /// Reverses the endianness of the bytes comprising a signed 64-bit value.
        /// </summary>
        /// <param name="input">The value to reverse</param>
        /// <returns>The reversed value</returns>
        public static long ReverseEndianness(long input)
        {
#if NET6_0_OR_GREATER
            return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(input);
#else
            return unchecked((long)ReverseEndianness(unchecked((ulong)input)));
#endif
        }

        /// <summary>
        /// Reverses the endianness of the bytes comprising an unsigned 64-bit value.
        /// </summary>
        /// <param name="input">The value to reverse</param>
        /// <returns>The reversed value</returns>
        public static ulong ReverseEndianness(ulong input)
        {
#if NET6_0_OR_GREATER
            return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(input);
#else
            // swap adjacent 32-bit blocks
            input = (input >> 32) | (input << 32);
            // swap adjacent 16-bit blocks
            input = ((input & 0xFFFF0000_FFFF0000U) >> 16) | ((input & 0x0000FFFF_0000FFFFU) << 16);
            // swap adjacent 8-bit blocks
            return ((input & 0xFF00FF00_FF00FF00U) >> 8) | ((input & 0x00FF00FF_00FF00FFU) << 8);
#endif
        }

        /// <summary>
        /// Reverses the endianness of a signed 32-bit value.
        /// </summary>
        /// <param name="input">The value to reverse</param>
        /// <returns>The reversed value</returns>
        public static int ReverseEndianness(int input)
        {
#if NET6_0_OR_GREATER
            return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(input);
#else
            return unchecked((int)ReverseEndianness(unchecked((uint)input)));
#endif
        }

        /// <summary>
        /// Reverses the endianness of the bytes comprising an unsigned 32-bit value.
        /// </summary>
        /// <param name="input">The value to reverse</param>
        /// <returns>The reversed value</returns>
        public static uint ReverseEndianness(uint input)
        {
#if NET6_0_OR_GREATER
            return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(input);
#else
            // swap adjacent 16-bit blocks
            input = (input >> 16) | (input << 16);
            // swap adjacent 8-bit blocks
            return ((input & 0xFF00FF00U) >> 8) | ((input & 0x00FF00FFU) << 8);
#endif
        }

        /// <summary>
        /// Reverses the endianness of the bytes comprising a signed 16-bit value.
        /// </summary>
        /// <param name="input">The value to reverse</param>
        /// <returns>The reversed value</returns>
        public static short ReverseEndianness(short input)
        {
#if NET6_0_OR_GREATER
            return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(input);
#else
            return unchecked((short)ReverseEndianness(unchecked((ushort)input)));
#endif
        }

        /// <summary>
        /// Reverses the endianness of the bytes comprising an unsigned 16-bit value.
        /// </summary>
        /// <param name="input">The value to reverse</param>
        /// <returns>The reversed value</returns>
        public static ushort ReverseEndianness(ushort input)
        {
#if NET6_0_OR_GREATER
            return System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(input);
#else
            return (ushort)((input >> 8) | (input << 8));
#endif
        }

        /// <summary>
        /// Gets the absolute alignment offset of the first element of a primitive span, modulo
        /// to some given word size. This is usually either needed for performance or to allow
        /// some fancy low-level casting on architectures that don't support unaligned reads (e.g. ARM)
        /// </summary>
        /// <typeparam name="T">The data type of the span being examined. Must be a pure value type.</typeparam>
        /// <param name="span">The span of data to examine.</param>
        /// <param name="wordSize">The word size we want to align to, in bytes.</param>
        /// <returns>The number of unaligned bytes at the beginning of the data.
        /// This is 0 if the data is aligned to the word size.
        /// For numbers larger than zero, it means "add (wordSize - X) bytes of padding at the start to bring
        /// the structure into alignment".</returns>
        public static int GetMemoryAlignment<T>(ReadOnlySpan<T> span, int wordSize) where T : struct
        {
            int rawOffset = (int)(Unsafe.ByteOffset(
                ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(span)),
                ref MemoryMarshal.GetReference(MemoryMarshal.AsBytes(OFFSET_CHECK.AsSpan()))).ToInt64() % wordSize);

            // if modulo is negative, add wordsize. this is just fancy branchless stuff
            return rawOffset + (rawOffset >> 31 & wordSize);
        }

        /// <summary>
        /// Encodes a binary stream as base64 and writes it to a stringbuilder directly without allocating new strings.
        /// </summary>
        /// <param name="inputStream">The input stream to read</param>
        /// <param name="output">The string builder to append the base64 output to.</param>
        public static void EncodeBase64ToStringBuilder(Stream inputStream, StringBuilder output)
        {
            using (PooledBuffer<byte> byteScratch = BufferPool<byte>.Rent(BufferPool<byte>.DEFAULT_BUFFER_SIZE))
            using (PooledBuffer<char> charScratch = BufferPool<char>.Rent(BufferPool<char>.DEFAULT_BUFFER_SIZE))
            {
                int bytesInInput = 0;
                int thisReadSize = 1;
                int maxReadSize = Math.Min(byteScratch.Length, (charScratch.Length / 4) * 3);
                while (thisReadSize > 0)
                {
                    thisReadSize = inputStream.Read(byteScratch.Buffer, bytesInInput, maxReadSize - bytesInInput);

                    if (thisReadSize > 0)
                    {
                        bytesInInput += thisReadSize;
                    }

                    if (bytesInInput > 0)
                    {
                        int byteAlignedSize = bytesInInput - (bytesInInput % 4);
                        int charsGenerated = Convert.ToBase64CharArray(byteScratch.Buffer, 0, byteAlignedSize, charScratch.Buffer, 0);
                        output.Append(charScratch.Buffer, 0, charsGenerated);
                        if (byteAlignedSize < bytesInInput)
                        {
                            ArrayExtensions.MemMove(byteScratch.Buffer, byteAlignedSize, 0, bytesInInput - byteAlignedSize);
                            bytesInInput -= byteAlignedSize;
                        }
                        else
                        {
                            bytesInInput = 0;
                        }
                    }
                }

                // Any remainder left?
                if (bytesInInput > 0)
                {
                    int charsGenerated = Convert.ToBase64CharArray(byteScratch.Buffer, 0, bytesInInput, charScratch.Buffer, 0);
                    output.Append(charScratch.Buffer, 0, charsGenerated);
                }
            }
        }

        /// <summary>
        /// Encodes binary data to base64 and writes it to a stringbuilder directly without allocating new strings.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="output"></param>
        public static void EncodeBase64ToStringBuilder(byte[] input, int offset, int count, StringBuilder output)
        {
            using (PooledBuffer<char> scratch = BufferPool<char>.Rent())
            {
                int bytesRead = 0;
                int maxReadSize = (scratch.Length / 4) * 3;
                output.EnsureCapacity(output.Length + (((count + 2) * 4) / 3));
                while (bytesRead < count)
                {
                    int thisReadSize = Math.Min(maxReadSize, count - bytesRead);
                    int charsGenerated = Convert.ToBase64CharArray(input, offset + bytesRead, thisReadSize, scratch.Buffer, 0);
                    output.Append(scratch.Buffer, 0, charsGenerated);
                    bytesRead += thisReadSize;
                }
            }
        }

        /// <summary>
        /// Given a stringbuilder containing a base64-encoded string, decode it into a pooled byte buffer.
        /// </summary>
        /// <param name="inputBuilder">The builder to read from</param>
        /// <returns>The decoded base64 data.</returns>
        public static PooledBuffer<byte> DecodeBase64FromStringBuilder(StringBuilder inputBuilder)
        {
            return DecodeBase64FromStringBuilder(inputBuilder, 0, inputBuilder.Length);
        }

        /// <summary>
        /// Given a stringbuilder containing a base64-encoded string, decode it into a pooled byte buffer.
        /// </summary>
        /// <param name="inputBuilder">The builder to read from</param>
        /// <param name="firstChar">The starting character to read from the stringbuilder</param>
        /// <param name="stringLength">The length of the base64 substring</param>
        /// <returns>The decoded base64 data.</returns>
        public static PooledBuffer<byte> DecodeBase64FromStringBuilder(StringBuilder inputBuilder, int firstChar, int stringLength)
        {
            inputBuilder.AssertNonNull(nameof(inputBuilder));
            if (firstChar < 0 || firstChar >= inputBuilder.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(firstChar));
            }

            if (stringLength < 0 || firstChar + stringLength > inputBuilder.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(stringLength));
            }

            if (stringLength % 4 != 0)
            {
                throw new ArgumentException("String length of base64 must be a multiple of 4");
            }

            int binaryLength = (stringLength / 4) * 3;
            if (inputBuilder[firstChar + stringLength - 2] == '=')
            {
                binaryLength -= 2;
            }
            else if (inputBuilder[firstChar + stringLength - 1] == '=')
            {
                binaryLength -= 1;
            }

            PooledBuffer<byte> returnVal = BufferPool<byte>.Rent(binaryLength);
            using (StringBuilderReadStream sbStream = new StringBuilderReadStream(inputBuilder, firstChar, stringLength, StringUtils.UTF8_WITHOUT_BOM))
            using (Base64AsciiDecodingStream decodeStream = new Base64AsciiDecodingStream(sbStream, StreamDirection.Read, ownsInnerStream: false))
            {
                int bytesRead = 0;
                while (bytesRead < binaryLength)
                {
                    bytesRead += decodeStream.Read(returnVal.Buffer, bytesRead, binaryLength - bytesRead);
                }

                return returnVal;
            }
        }

        /// <summary>
        /// Implementation of <see href="https://www.rfc-editor.org/rfc/rfc4648.html#section-5">RFC 4648 section 5</see> which describes URL-safe Base64 encoding.
        /// The difference is that '=' chars at the end are trimmed, and the '+' and '/' are replaced with '-' and '_', respectively.
        /// </summary>
        /// <param name="data">The data to encode.</param>
        /// <param name="offset">The offset of the data.</param>
        /// <param name="count">The number of bytes to encode.</param>
        /// <returns>The encoded string.</returns>
        public static string EncodeUrlSafeBase64(byte[] data, int offset, int count)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                EncodeUrlSafeBase64(data, offset, count, pooledSb.Builder);
                return pooledSb.Builder.ToString();
            }
        }

        /// <summary>
        /// Implementation of <see href="https://www.rfc-editor.org/rfc/rfc4648.html#section-5">RFC 4648 section 5</see> which describes URL-safe Base64 encoding.
        /// The difference is that '=' chars at the end are trimmed, and the '+' and '/' are replaced with '-' and '_', respectively.
        /// </summary>
        /// <param name="data">The data to encode.</param>
        /// <param name="offset">The offset of the data.</param>
        /// <param name="count">The number of bytes to encode.</param>
        /// <param name="output">The string builder to append the encoded output to</param>
        /// <returns>The number of base64 characters that were encoded.</returns>
        public static int EncodeUrlSafeBase64(byte[] data, int offset, int count, StringBuilder output)
        {
            if (count == 0)
            {
                return 0;
            }

            int originalSbLength = output.Length;
            EncodeBase64ToStringBuilder(data, offset, count, output);

            // Trim '=' from end
            while (output[output.Length - 1] == '=')
            {
                output.Length -= 1;
            }

            int newSbLength = output.Length;
            // Replace non-URL safe chars. Make sure we only apply within the region of the builder that we modified.
            output.Replace('+', '-', originalSbLength, newSbLength - originalSbLength); // 62nd char of encoding
            output.Replace('/', '_', originalSbLength, newSbLength - originalSbLength); // 63rd char of encoding
            return newSbLength - originalSbLength;
        }

        /// <summary>
        /// Implementation of <see href="https://www.rfc-editor.org/rfc/rfc4648.html#section-5">RFC 4648 section 5</see> which describes URL-safe Base64 encoding.
        /// The difference is that '=' chars at the end are trimmed, and the '+' and '/' are replaced with '-' and '_', respectively.
        /// </summary>
        /// <param name="input">The data to decode.</param>
        /// <returns>The decoded bytes.</returns>
        public static PooledBuffer<byte> DecodeUrlSafeBase64(string input)
        {
            input.AssertNonNull(nameof(input));
            if (input.Length == 0)
            {
                return new PooledBuffer<byte>(EMPTY_BYTE_ARRAY, 0, -1);
            }

            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                pooledSb.Builder.Append(input);
                switch (input.Length % 4)
                {
                    case 0:
                    case 1:
                        break;
                    case 2:
                        pooledSb.Builder.Append("==");
                        break;
                    case 3:
                        pooledSb.Builder.Append("=");
                        break;
                }

                pooledSb.Builder.Replace('-', '+'); // 62nd char of encoding
                pooledSb.Builder.Replace('_', '/'); // 63rd char of encoding
                return DecodeBase64FromStringBuilder(pooledSb.Builder);
            }
        }

        /// <summary>
        /// Converts a hexadecimal string like "4EF9" into a byte array
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static byte[] FromHexString(string hex)
        {
            return FromHexString(hex, 0, hex.Length);
        }

        /// <summary>
        /// Converts a hexadecimal string like "4EF9" into a byte array
        /// </summary>
        /// <param name="input">The input string to read.</param>
        /// <param name="inputOffset">The initial character offset when reading the input.</param>
        /// <param name="charsToRead">The number of characters to process. Must be an even number.</param>
        /// <returns>A byte array containing the string's decoded binary.</returns>
        public static byte[] FromHexString(string input, int inputOffset, int charsToRead)
        {
            if (charsToRead % 2 != 0)
            {
                throw new ArgumentException("Hex string must have an even length");
            }

            byte[] returnVal = new byte[charsToRead / 2];
            FromHexString(input, inputOffset, charsToRead, returnVal, 0);
            return returnVal;
        }

        /// <summary>
        /// Converts a hexadecimal string like "4EF9" into a byte array
        /// </summary>
        /// <param name="input">The input string to read.</param>
        /// <param name="inputOffset">The initial character offset when reading the input.</param>
        /// <param name="charsToRead">The number of characters to process. Must be an even number.</param>
        /// <param name="output">A byte array to put the output.</param>
        /// <param name="outputOffset">The byte offset to use when writing to the output array.</param>
        /// <returns>The number of bytes that were written to the output array.</returns>
        public static int FromHexString(string input, int inputOffset, int charsToRead, byte[] output, int outputOffset)
        {
            if (charsToRead == 0)
            {
                return 0;
            }

            if (charsToRead % 2 != 0)
            {
                throw new ArgumentException("Hex string must have an even length");
            }

            int bytesToRead = charsToRead / 2;
            for (int c = 0; c < bytesToRead; c++)
            {
                byte thisByte = 0;
                char nibble = input[inputOffset + (2 * c)];
                if (nibble >= '0' && nibble <= '9')
                {
                    thisByte |= (byte)((nibble - '0') << 4);
                }
                else if (nibble >= 'A' && nibble <= 'F')
                {
                    thisByte |= (byte)((nibble - 'A' + 10) << 4);
                }
                else if (nibble >= 'a' && nibble <= 'f')
                {
                    thisByte |= (byte)((nibble - 'a' + 10) << 4);
                }
                else
                {
                    throw new FormatException("Non-hex digits found in string: " + input);
                }

                nibble = input[inputOffset + (2 * c) + 1];
                if (nibble >= '0' && nibble <= '9')
                {
                    thisByte |= (byte)(nibble - '0');
                }
                else if(nibble >= 'A' && nibble <= 'F')
                {
                    thisByte |= (byte)(nibble - 'A' + 10);
                }
                else if (nibble >= 'a' && nibble <= 'f')
                {
                    thisByte |= (byte)(nibble - 'a' + 10);
                }
                else
                {
                    throw new FormatException("Non-hex digits found in string: " + input);
                }

                output[outputOffset + c] = thisByte;
            }

            return bytesToRead;
        }

        public static string ToHexString(int value)
        {
            return value.ToString("X8");
        }

        public static string ToBinaryString(long value)
        {
            return ToBinaryString((ulong)value);
        }

        public static string ToBinaryString(ulong value)
        {
            ulong shift = 0x80000000_00000000U;
            using (PooledStringBuilder sb = StringBuilderPool.Rent())
            {
                for (int c = 0; c < 64; c++)
                {
                    if ((value & shift) != 0)
                    {
                        sb.Builder.Append('1');
                    }
                    else
                    {
                        sb.Builder.Append('0');
                    }

                    shift = shift >> 1;
                }

                return sb.Builder.ToString();
            }
        }

        public static string ToBinaryString(int value)
        {
            return ToBinaryString((uint)value);
        }

        public static string ToBinaryString(uint value)
        {
            uint shift = 0x80000000U;
            using (PooledStringBuilder sb = StringBuilderPool.Rent())
            {
                for (int c = 0; c < 32; c++)
                {
                    if ((value & shift) != 0)
                    {
                        sb.Builder.Append('1');
                    }
                    else
                    {
                        sb.Builder.Append('0');
                    }

                    shift = shift >> 1;
                }

                return sb.Builder.ToString();
            }
        }

        /// <summary>
        /// Prints a series of bytes as a hexadecimal string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string ToHexString(byte[] bytes)
        {
            return ToHexString(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Prints a series of bytes as a hexadecimal string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string ToHexString(ArraySegment<byte> bytes)
        {
            return ToHexString(bytes.Array, bytes.Offset, bytes.Count);
        }

        /// <summary>
        /// Prints a series of bytes as a hexadecimal string
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string ToHexString(byte[] bytes, int offset, int length)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                ToHexString(bytes, offset, length, pooledSb.Builder);
                return pooledSb.Builder.ToString();
            }
        }

        /// <summary>
        /// Prints a series of bytes as a hexadecimal string to the given string builder.
        /// </summary>
        /// <param name="bytes">The bytes to be read</param>
        /// <param name="offset">The input offset when reading bytes</param>
        /// <param name="length">The number of bytes to process</param>
        /// <param name="outputBuffer">A string builder to append the output hex string</param>
        public static void ToHexString(byte[] bytes, int offset, int length, StringBuilder outputBuffer)
        {
            // If we want super-turbo performance for some reason, we could look at vectorizing based on https://github.com/dotnet/runtime/blob/def5e3240bdee3ee37ba22c41c840bbf431c4b15/src/libraries/Common/src/System/HexConverter.cs
            outputBuffer.EnsureCapacity(outputBuffer.Length + (length * 2));
            for (int c = 0; c < length; c++)
            {
                PrintByte(bytes[c + offset], outputBuffer);
            }
        }

        private static void PrintByte(byte input, StringBuilder builder)
        {
            int low = input & 0xF;
            int high = (input >> 4) & 0xF;
            if (high < 10)
            {
                builder.Append((char)('0' + high));
            }
            else
            {
                builder.Append((char)(('A' - 10) + high));
            }

            if (low < 10)
            {
                builder.Append((char)('0' + low));
            }
            else
            {
                builder.Append((char)(('A' - 10) + low));
            }
        }
    }
}
