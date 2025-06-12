using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Sandbox
{
    internal static class Base64Intrinsics
    {
        /// <summary>
        /// Encodes base64 data using the fastest intrinsics available.
        /// </summary>
        /// <param name="inData">The span of input bytes to encode. The full span will be used.</param>
        /// <param name="outAscii">The buffer space to write encoded ASCII chars. Must be at least 4/3 the size of the input.</param>
        /// <returns>The number of output characters that were produced.</returns>
        internal static unsafe int EncodeBase64(ReadOnlySpan<byte> inData, Span<byte> outAscii)
        {
            // Make sure we validate bounds checks before we start writing to random byte pointers!
            if (inData.Length == 0)
            {
                return 0;
            }

            int outputBytesRequired = 4 * ((inData.Length / 3) + (((inData.Length % 3) != 0) ? 1 : 0));
            if (outAscii.Length < outputBytesRequired)
            {
                throw new IndexOutOfRangeException("Output ASCII buffer must be at least " + outputBytesRequired);
            }

#if NET6_0_OR_GREATER
#if DEBUG
            if (FastRandom.Shared.NextBool() && Avx2.IsSupported)
#else
            if (Avx2.IsSupported)
#endif
            {
                return EncodeBase64_AVX2(inData, outAscii);
            }
#if DEBUG
            else if (FastRandom.Shared.NextBool() && Ssse3.IsSupported)
#else
            else if (Ssse3.IsSupported)
#endif
            {
                return EncodeBase64_SSSE3(inData, outAscii);
            }
            else
#endif
            {
                fixed (byte* inPtr = inData)
                fixed (byte* outPtr = outAscii)
                {
                    return EncodeBase64_BasicUnsafe(inPtr, inData.Length, outPtr);
                }
            }
        }

        private static readonly byte[] BASE64_ASCII_ENCODING_TABLE =
            {
                // A - Z
                65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90,
                // a - z
                97, 98, 99, 100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 122,
                // 0 - 9
                48, 49, 50, 51, 52, 53, 54, 55, 56, 57,
                // +
                43,
                // /
                47,
                // =
                61
            };

        private static IntPtr ASCII_ENCODING_TABLE = NativeAlloc(BASE64_ASCII_ENCODING_TABLE);

        private static unsafe IntPtr NativeAlloc<T>(T[] data) where T : unmanaged
        {
            void* ptr = NativeMemory.Alloc((nuint)(data.Length * sizeof(T)));
            data.AsSpan().CopyTo(new Span<T>(ptr, data.Length));
            return (IntPtr)ptr;
        }

        /// <summary>
        /// Basic unmanaged implementation of base64 encoding.
        /// Adapted from C# reference source https://source.dot.net/#System.Private.CoreLib/Convert.cs,440a570fbff23b16.
        /// Will produce padded bytes unless input length is exactly a multiple of 3.
        /// </summary>
        /// <param name="inData">The input data to encode</param>
        /// <param name="outAscii">The span of bytes to write the encoded ASCII characters (4 bytes output per 3 bytes input)</param>
        /// <returns>The number of output characters that were produced.</returns>
        private static unsafe int EncodeBase64_BasicUnsafe(byte* inData, int inDataLength, byte* outAscii)
        {
            int inByteIndex = 0;
            int outByteIndex = 0;

            // Process 3 input bytes at a time, producing 4 bytes output on each pass
            int lengthmod3 = inDataLength % 3;
            int blockProcessEndIdx = (inDataLength - lengthmod3);
            byte* encodingTablePtr = (byte*)ASCII_ENCODING_TABLE;
            for (; inByteIndex < blockProcessEndIdx; inByteIndex += 3)
            {
                outAscii[outByteIndex] = encodingTablePtr[(inData[inByteIndex] & 0xfc) >> 2];
                outAscii[outByteIndex + 1] = encodingTablePtr[((inData[inByteIndex] & 0x03) << 4) | ((inData[inByteIndex + 1] & 0xf0) >> 4)];
                outAscii[outByteIndex + 2] = encodingTablePtr[((inData[inByteIndex + 1] & 0x0f) << 2) | ((inData[inByteIndex + 2] & 0xc0) >> 6)];
                outAscii[outByteIndex + 3] = encodingTablePtr[inData[inByteIndex + 2] & 0x3f];
                outByteIndex += 4;
            }

            inByteIndex = blockProcessEndIdx;

            // And the final padded sequence if necessary
            switch (lengthmod3)
            {
                case 2: // One character padding needed
                    outAscii[outByteIndex] = encodingTablePtr[(inData[inByteIndex] & 0xfc) >> 2];
                    outAscii[outByteIndex + 1] = encodingTablePtr[((inData[inByteIndex] & 0x03) << 4) | ((inData[inByteIndex + 1] & 0xf0) >> 4)];
                    outAscii[outByteIndex + 2] = encodingTablePtr[(inData[inByteIndex + 1] & 0x0f) << 2];
                    outAscii[outByteIndex + 3] = encodingTablePtr[64]; // Pad
                    outByteIndex += 4;
                    break;
                case 1: // Two character padding needed
                    outAscii[outByteIndex] = encodingTablePtr[(inData[inByteIndex] & 0xfc) >> 2];
                    outAscii[outByteIndex + 1] = encodingTablePtr[(inData[inByteIndex] & 0x03) << 4];
                    outAscii[outByteIndex + 2] = encodingTablePtr[64]; // Pad
                    outAscii[outByteIndex + 3] = encodingTablePtr[64]; // Pad
                    outByteIndex += 4;
                    break;
            }

            return outByteIndex;
        }

#if NET6_0_OR_GREATER

        static Base64Intrinsics()
        {
            // Do native allocations only for the vector datasets that might be needed
#if !DEBUG
            if (Avx2.IsSupported)
#endif
            {
                AVX2_MASKS = NativeAlloc(new uint[]
                {
                    0xFC000000,
                    0x03F00000,
                    0x000FC000,
                    0x00003F00,
                });

                AVX2_SHUFFLE_1 = NativeAlloc(new byte[]
                {
                    0x80, 0x02, 0x01, 0x00,
                    0x80, 0x05, 0x04, 0x03,
                    0x80, 0x08, 0x07, 0x06,
                    0x80, 0x0B, 0x0A, 0x09,

                    0x80, 0x02, 0x01, 0x00,
                    0x80, 0x05, 0x04, 0x03,
                    0x80, 0x08, 0x07, 0x06,
                    0x80, 0x0B, 0x0A, 0x09,
                });

                AVX2_SHUFFLE_2 = NativeAlloc(new byte[]
                {
                    0x03, 0x02, 0x01, 0x00,
                    0x07, 0x06, 0x05, 0x04,
                    0x0B, 0x0A, 0x09, 0x08,
                    0x0F, 0x0E, 0x0D, 0x0C,

                    0x03, 0x02, 0x01, 0x00,
                    0x07, 0x06, 0x05, 0x04,
                    0x0B, 0x0A, 0x09, 0x08,
                    0x0F, 0x0E, 0x0D, 0x0C,
                });

                AVX_CONSTANTS = NativeAlloc(new sbyte[]
                {
                    65, 6, 25, 75, 51, 15, 61, 3, 62
                });
            }
#if !DEBUG
            else if (Ssse3.IsSupported)
#endif
            {
                SSE3_MASKS1 = NativeAlloc(new uint[]
                {
                    0xFC000000, 0xFC000000, 0xFC000000, 0xFC000000,
                    0x03F00000, 0x03F00000, 0x03F00000, 0x03F00000,
                    0x000FC000, 0x000FC000, 0x000FC000, 0x000FC000,
                    0x00003F00, 0x00003F00, 0x00003F00, 0x00003F00,
                });

                SSE3_SHUFFLE_1 = NativeAlloc(new byte[]
                {
                    0x80, 0x02, 0x01, 0x00,
                    0x80, 0x05, 0x04, 0x03,
                    0x80, 0x08, 0x07, 0x06,
                    0x80, 0x0B, 0x0A, 0x09,
                });

                SSE3_SHUFFLE_2 = NativeAlloc(new byte[]
                {
                    0x03, 0x02, 0x01, 0x00,
                    0x07, 0x06, 0x05, 0x04,
                    0x0B, 0x0A, 0x09, 0x08,
                    0x0F, 0x0E, 0x0D, 0x0C,
                });

                SSE3_CONSTANTS = NativeAlloc(new uint[]
                {
                    // 16
                    0x10101010U, 0x10101010U, 0x10101010U, 0x10101010U,
                    // 63
                    0x3F3F3F3FU, 0x3F3F3F3FU, 0x3F3F3F3FU, 0x3F3F3F3FU,
                    // 3
                    0x03030303U, 0x03030303U, 0x03030303U, 0x03030303U,
                    // 62
                    0x3E3E3E3EU, 0x3E3E3E3EU, 0x3E3E3E3EU, 0x3E3E3E3EU,
                    // 15
                    0x0F0F0F0FU, 0x0F0F0F0FU, 0x0F0F0F0FU, 0x0F0F0F0FU,
                    // 52
                    0x34343434U, 0x34343434U, 0x34343434U, 0x34343434U,
                    // 75
                    0x4B4B4B4BU, 0x4B4B4B4BU, 0x4B4B4B4BU, 0x4B4B4B4BU,
                    // 26
                    0x1A1A1A1AU, 0x1A1A1A1AU, 0x1A1A1A1AU, 0x1A1A1A1AU,
                    // 6
                    0x06060606U, 0x06060606U, 0x06060606U, 0x06060606U,
                });
            }
        }

        #region AVX2 Implementation

        // AVX2 vectors

        private static readonly IntPtr AVX2_MASKS;

        private static readonly IntPtr AVX2_SHUFFLE_1;

        private static readonly IntPtr AVX2_SHUFFLE_2;

        private static readonly IntPtr AVX_CONSTANTS;

        internal static unsafe int EncodeBase64_AVX2(
            ReadOnlySpan<byte> inData,
            Span<byte> outAscii)
        {
            int inByteIndex = 0;
            int outByteIndex = 0;

            // AVX2 implementation
            // Input is 24 bytes, output is 32 bytes representing ASCII chars
            fixed (byte* inPtr = inData)
            fixed (byte* outPtr = outAscii)
            {
                int vectorEndIdx = inData.Length - (inData.Length % 24) - 12;

                // Loop 1 - take groups of 3 bytes of input and shift them to expand to 4-byte blocks
                // which will map to character classes later
                while (inByteIndex < vectorEndIdx)
                {
                    // Map 24 inputs bytes into the boundaries of 8 uint32 slots
                    // We would like to just do this with a single shuffle command, but
                    // can't because shuffles are only done within 128-bit lanes.
                    // So we have to preprocess a bit more to get 12 input bytes aligned
                    // correctly into each high and low 128-bit lane, then shuffle after
                    Vector256<byte> dataVec = Avx2.InsertVector128(
                        Avx.LoadVector256(inPtr + inByteIndex),
                        Sse2.LoadVector128(inPtr + inByteIndex + 12),
                        1);
                    Vector256<byte> shufVec1 = Avx.LoadVector256((byte*)AVX2_SHUFFLE_1);
                    Vector256<byte> result = Avx2.Shuffle(dataVec, shufVec1);
                    Vector256<uint> castVec = Vector256.AsUInt32(result);

                    // Apply shifts and masks to each uint32 slot so its constituent bytes
                    // become indexes into the ASCII lookup table
                    Vector256<uint> sextet1 = Avx2.ShiftRightLogical(Avx2.And(castVec, Avx2.BroadcastScalarToVector256(((uint*)AVX2_MASKS) + 0)), 2);
                    Vector256<uint> sextet2 = Avx2.ShiftRightLogical(Avx2.And(castVec, Avx2.BroadcastScalarToVector256(((uint*)AVX2_MASKS) + 1)), 4);
                    Vector256<uint> sextet3 = Avx2.ShiftRightLogical(Avx2.And(castVec, Avx2.BroadcastScalarToVector256(((uint*)AVX2_MASKS) + 2)), 6);
                    Vector256<uint> sextet4 = Avx2.ShiftRightLogical(Avx2.And(castVec, Avx2.BroadcastScalarToVector256(((uint*)AVX2_MASKS) + 3)), 8);
                    Vector256<byte> unmappedAscii = Vector256.AsByte(
                        Avx2.Or(
                            Avx2.Or(sextet1, sextet2),
                            Avx2.Or(sextet3, sextet4)));

                    // Do one last shuffle to reverse the endianness of each 32-bit lane, and copy to dest
                    Vector256<byte> shufVec2 = Avx.LoadVector256((byte*)AVX2_SHUFFLE_2);
                    Vector256<byte> byteSwappedUnmappedAscii = Avx2.Shuffle(unmappedAscii, shufVec2);
                    Avx.Store(outPtr + outByteIndex, byteSwappedUnmappedAscii);

                    inByteIndex += 24;
                    outByteIndex += 32;
                }

                // Loop 2: Do compounding arithmetic to map byte indices to ASCII char classes
                // These adds will overflow a signed byte, but it will all work out in the end
                // Classes are:
                // A - Z: input 0 - 25, output 65 - 90 (+65)
                // a - z: input 26 - 51, output 97 - 122 (+71)
                // 0 - 9: input 52 - 61, output 48 - 57 (-4)
                // +: input 62, output 43 (-19)
                // /: input 63, output 47 (-16)

                // The algorithm is:
                // - Add 65 to all elements
                // - Add 6 to all elements above 25
                // - Subtract 75 from all elements above 51
                // - Subtract 15 from all elements above 61
                // - Add 3 to all elements above 62

                Vector256<sbyte> sixtyFive = Avx2.BroadcastScalarToVector256(((sbyte*)AVX_CONSTANTS) + 0);
                Vector256<sbyte> six = Avx2.BroadcastScalarToVector256(((sbyte*)AVX_CONSTANTS) + 1);
                Vector256<sbyte> twentyFive = Avx2.BroadcastScalarToVector256(((sbyte*)AVX_CONSTANTS) + 2);
                Vector256<sbyte> seventyFive = Avx2.BroadcastScalarToVector256(((sbyte*)AVX_CONSTANTS) + 3);
                Vector256<sbyte> fiftyOne = Avx2.BroadcastScalarToVector256(((sbyte*)AVX_CONSTANTS) + 4);
                Vector256<sbyte> fifteen = Avx2.BroadcastScalarToVector256(((sbyte*)AVX_CONSTANTS) + 5);
                Vector256<sbyte> sixtyOne = Avx2.BroadcastScalarToVector256(((sbyte*)AVX_CONSTANTS) + 6);
                Vector256<sbyte> three = Avx2.BroadcastScalarToVector256(((sbyte*)AVX_CONSTANTS) + 7);
                Vector256<sbyte> sixtyTwo = Avx2.BroadcastScalarToVector256(((sbyte*)AVX_CONSTANTS) + 8);

                byte* outputIterator = outPtr;
                byte* outputEnd = outPtr + outByteIndex;
                while (outputIterator < outputEnd)
                {
                    Vector256<sbyte> byteSwappedUnmappedAscii = Avx.LoadVector256((sbyte*)outputIterator);

                    Vector256<sbyte> mappedAscii =
                        Avx2.Add(
                            byteSwappedUnmappedAscii,
                            sixtyFive);

                    mappedAscii =
                        Avx2.Add(
                            mappedAscii,
                            Avx2.And(
                                Avx2.CompareGreaterThan(
                                    byteSwappedUnmappedAscii,
                                    twentyFive),
                                six));

                    mappedAscii =
                        Avx2.Subtract(
                            mappedAscii,
                            Avx2.And(
                                Avx2.CompareGreaterThan(
                                    byteSwappedUnmappedAscii,
                                    fiftyOne),
                                seventyFive));

                    mappedAscii =
                        Avx2.Subtract(
                            mappedAscii,
                            Avx2.And(
                                Avx2.CompareGreaterThan(
                                    byteSwappedUnmappedAscii,
                                    sixtyOne),
                                fifteen));

                    mappedAscii =
                        Avx2.Add(
                            mappedAscii,
                            Avx2.And(
                                Avx2.CompareGreaterThan(
                                    byteSwappedUnmappedAscii,
                                    sixtyTwo),
                                three));

                    Avx.Store(outputIterator, Vector256.AsByte(mappedAscii));
                    outputIterator += 32;
                }

                // Call the linear method to handle the residual
                return outByteIndex + EncodeBase64_BasicUnsafe(inPtr + inByteIndex, inData.Length - inByteIndex, outPtr + outByteIndex);
            }
        }

        #endregion

        #region SSE3 Implementation

        // SSE vectors

        private static readonly IntPtr SSE3_MASKS1;

        private static readonly IntPtr SSE3_SHUFFLE_1;

        private static readonly IntPtr SSE3_SHUFFLE_2;

        private static readonly IntPtr SSE3_CONSTANTS;

        private static unsafe int EncodeBase64_SSSE3(
            ReadOnlySpan<byte> inData,
            Span<byte> outAscii)
        {
            int inByteIndex = 0;
            int outByteIndex = 0;

            // Input is 12 bytes, output is 16 bytes representing ASCII chars
            fixed (byte* inPtr = inData)
            fixed (byte* outPtr = outAscii)
            {
                int vectorEndIdx = inData.Length - (inData.Length % 12);
                // Loop 1 - take groups of 3 bytes of input and shift them to expand to 4-byte blocks
                // which will map to character classes later
                while (inByteIndex < vectorEndIdx)
                {
                    // Shuffle to map 12 inputs bytes into the boundaries of 4 uint32 slots
                    Vector128<uint> arrangedInputs = Vector128.AsUInt32(
                        Ssse3.Shuffle(
                            Sse2.LoadVector128(inPtr + inByteIndex),
                            Sse2.LoadVector128((byte*)SSE3_SHUFFLE_1)));

                    // Apply shifts and masks to each uint32 slot so its constituent bytes
                    // become indexes into the ASCII lookup table
                    Vector128<uint> sextet1 = Sse2.ShiftRightLogical(Sse2.And(arrangedInputs, Sse2.LoadVector128(((uint*)SSE3_MASKS1) + 0)), 2);
                    Vector128<uint> sextet2 = Sse2.ShiftRightLogical(Sse2.And(arrangedInputs, Sse2.LoadVector128(((uint*)SSE3_MASKS1) + 4)), 4);
                    Vector128<uint> sextet3 = Sse2.ShiftRightLogical(Sse2.And(arrangedInputs, Sse2.LoadVector128(((uint*)SSE3_MASKS1) + 8)), 6);
                    Vector128<uint> sextet4 = Sse2.ShiftRightLogical(Sse2.And(arrangedInputs, Sse2.LoadVector128(((uint*)SSE3_MASKS1) + 12)), 8);
                    Vector128<byte> unmappedAscii = Vector128.AsByte(
                        Sse2.Or(
                            Sse2.Or(sextet1, sextet2),
                            Sse2.Or(sextet3, sextet4)));

                    // Shuffle to reverse the endianness of each 32-bit lane
                    Vector128<byte> shufVec2 = Sse2.LoadVector128((byte*)SSE3_SHUFFLE_2);
                    Vector128<byte> byteSwappedUnmappedAscii = Ssse3.Shuffle(unmappedAscii, shufVec2);
                    Sse2.Store(outPtr + outByteIndex, byteSwappedUnmappedAscii);

                    inByteIndex += 12;
                    outByteIndex += 16;
                }

                // Loop 2: Do compounding arithmetic to map byte indices to ASCII char classes
                // These adds will overflow a signed byte, but it will all work out in the end
                // Classes are:
                // A - Z: input 0 - 25, output 65 - 90 (+65)
                // a - z: input 26 - 51, output 97 - 122 (+71)
                // 0 - 9: input 52 - 61, output 48 - 57 (-4)
                // +: input 62, output 43 (-19)
                // /: input 63, output 47 (-16)

                // The algorithm is:
                // - Subtract 16 from all elements
                // - Subtract 3 from all elements below 63
                // - Add 15 to all elements below 62
                // - Add 75 to all elements below 52
                // - Subtract 6 from all elements below 26

                Vector128<sbyte> sixteen = Sse2.LoadVector128(((sbyte*)SSE3_CONSTANTS));
                Vector128<sbyte> sixtyThree = Sse2.LoadVector128(((sbyte*)SSE3_CONSTANTS) + 16);
                Vector128<sbyte> three = Sse2.LoadVector128(((sbyte*)SSE3_CONSTANTS) + 32);
                Vector128<sbyte> sixtyTwo = Sse2.LoadVector128(((sbyte*)SSE3_CONSTANTS) + 48);
                Vector128<sbyte> fifteen = Sse2.LoadVector128(((sbyte*)SSE3_CONSTANTS) + 64);
                Vector128<sbyte> fiftyTwo = Sse2.LoadVector128(((sbyte*)SSE3_CONSTANTS) + 80);
                Vector128<sbyte> seventyFive = Sse2.LoadVector128(((sbyte*)SSE3_CONSTANTS) + 96);
                Vector128<sbyte> twentySix = Sse2.LoadVector128(((sbyte*)SSE3_CONSTANTS) + 112);
                Vector128<sbyte> six = Sse2.LoadVector128(((sbyte*)SSE3_CONSTANTS) + 128);

                byte* cur = outPtr;
                byte* end = outPtr + outByteIndex;
                while (cur < end)
                {
                    Vector128<sbyte> byteSwappedUnmappedAscii = Sse2.LoadVector128((sbyte*)cur);

                    Vector128<sbyte> mappedAscii =
                        Sse2.Subtract(
                            byteSwappedUnmappedAscii,
                            sixteen);

                    mappedAscii =
                        Sse2.Subtract(
                            mappedAscii,
                            Sse2.And(
                                Sse2.CompareLessThan(
                                    byteSwappedUnmappedAscii,
                                    sixtyThree),
                                three));

                    mappedAscii =
                        Sse2.Add(
                            mappedAscii,
                            Sse2.And(
                                Sse2.CompareLessThan(
                                    byteSwappedUnmappedAscii,
                                    sixtyTwo),
                                fifteen));

                    mappedAscii =
                        Sse2.Add(
                            mappedAscii,
                            Sse2.And(
                                Sse2.CompareLessThan(
                                    byteSwappedUnmappedAscii,
                                    fiftyTwo),
                                seventyFive));

                    mappedAscii =
                        Sse2.Subtract(
                            mappedAscii,
                            Sse2.And(
                                Sse2.CompareLessThan(
                                    byteSwappedUnmappedAscii,
                                    twentySix),
                                six));

                    Sse2.Store(cur, Vector128.AsByte(mappedAscii));
                    cur += 16;
                }

                // Call the linear method to handle the residual
                return outByteIndex + EncodeBase64_BasicUnsafe(inPtr + inByteIndex, inData.Length - inByteIndex, outPtr + outByteIndex);
            }
        }

        #endregion

#endif // NET6_0_OR_GREATER
    }
}
