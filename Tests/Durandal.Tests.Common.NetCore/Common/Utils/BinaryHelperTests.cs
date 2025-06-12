using Durandal.Common.Utils;
using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Test;
using Durandal.Common.Collections;
using System.Runtime.InteropServices;

namespace Durandal.Tests.Common.Utils
{
    [TestClass]
    public class BinaryHelperTests
    {
        [TestMethod]
        public void TestBinaryConversionInt16()
        {
            IRandom rand = new FastRandom(16);
            byte[] scratchBuf = new byte[2 + 2];
            for (int loop = 0; loop < 10000; loop++)
            {
                rand.NextBytes(scratchBuf);
                short input = BitConverter.ToInt16(scratchBuf, 0);

                BinaryHelpers.Int16ToByteArrayLittleEndian(input, scratchBuf, 2);
                short output = BinaryHelpers.ByteArrayToInt16LittleEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.Int16ToByteArrayBigEndian(input, scratchBuf, 2);
                output = BinaryHelpers.ByteArrayToInt16BigEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.Int16ToByteArrayLittleEndian(input, scratchBuf, 0);
                byte[] expectedArray = BitConverter.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(scratchBuf, 0, expectedArray, 0, expectedArray.Length));
            }
        }

        [TestMethod]
        public void TestBinaryConversionUInt16()
        {
            IRandom rand = new FastRandom(16);
            byte[] scratchBuf = new byte[2 + 2];
            for (int loop = 0; loop < 10000; loop++)
            {
                rand.NextBytes(scratchBuf);
                ushort input = BitConverter.ToUInt16(scratchBuf, 0);

                BinaryHelpers.UInt16ToByteArrayLittleEndian(input, scratchBuf, 2);
                ushort output = BinaryHelpers.ByteArrayToUInt16LittleEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.UInt16ToByteArrayBigEndian(input, scratchBuf, 2);
                output = BinaryHelpers.ByteArrayToUInt16BigEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.UInt16ToByteArrayLittleEndian(input, scratchBuf, 0);
                byte[] expectedArray = BitConverter.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(scratchBuf, 0, expectedArray, 0, expectedArray.Length));
            }
        }

        [TestMethod]
        public void TestBinaryConversionInt32()
        {
            IRandom rand = new FastRandom(16);
            byte[] scratchBuf = new byte[2 + 4];
            for (int loop = 0; loop < 10000; loop++)
            {
                rand.NextBytes(scratchBuf);
                int input = BitConverter.ToInt32(scratchBuf, 0);

                BinaryHelpers.Int32ToByteArrayLittleEndian(input, scratchBuf, 2);
                int output = BinaryHelpers.ByteArrayToInt32LittleEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.Int32ToByteArrayBigEndian(input, scratchBuf, 2);
                output = BinaryHelpers.ByteArrayToInt32BigEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.Int32ToByteArrayLittleEndian(input, scratchBuf, 0);
                byte[] expectedArray = BitConverter.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(scratchBuf, 0, expectedArray, 0, expectedArray.Length));
            }
        }

        [TestMethod]
        public void TestBinaryConversionUInt32()
        {
            IRandom rand = new FastRandom(16);
            byte[] scratchBuf = new byte[2 + 4];
            for (int loop = 0; loop < 10000; loop++)
            {
                rand.NextBytes(scratchBuf);
                uint input = BitConverter.ToUInt32(scratchBuf, 0);

                BinaryHelpers.UInt32ToByteArrayLittleEndian(input, scratchBuf, 2);
                uint output = BinaryHelpers.ByteArrayToUInt32LittleEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.UInt32ToByteArrayBigEndian(input, scratchBuf, 2);
                output = BinaryHelpers.ByteArrayToUInt32BigEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.UInt32ToByteArrayLittleEndian(input, scratchBuf, 0);
                byte[] expectedArray = BitConverter.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(scratchBuf, 0, expectedArray, 0, expectedArray.Length));
            }
        }

        [TestMethod]
        public void TestBinaryConversionInt64()
        {
            IRandom rand = new FastRandom(16);
            byte[] scratchBuf = new byte[2 + 8];
            for (int loop = 0; loop < 10000; loop++)
            {
                rand.NextBytes(scratchBuf);
                long input = BitConverter.ToInt64(scratchBuf, 0);

                BinaryHelpers.Int64ToByteArrayLittleEndian(input, scratchBuf, 2);
                long output = BinaryHelpers.ByteArrayToInt64LittleEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.Int64ToByteArrayBigEndian(input, scratchBuf, 2);
                output = BinaryHelpers.ByteArrayToInt64BigEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.Int64ToByteArrayLittleEndian(input, scratchBuf, 0);
                byte[] expectedArray = BitConverter.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(scratchBuf, 0, expectedArray, 0, expectedArray.Length));
            }
        }

        [TestMethod]
        public void TestBinaryConversionUInt64()
        {
            IRandom rand = new FastRandom(16);
            byte[] scratchBuf = new byte[2 + 8];
            for (int loop = 0; loop < 10000; loop++)
            {
                rand.NextBytes(scratchBuf);
                ulong input = BitConverter.ToUInt64(scratchBuf, 0);

                BinaryHelpers.UInt64ToByteArrayLittleEndian(input, scratchBuf, 2);
                ulong output = BinaryHelpers.ByteArrayToUInt64LittleEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.UInt64ToByteArrayBigEndian(input, scratchBuf, 2);
                output = BinaryHelpers.ByteArrayToUInt64BigEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.UInt64ToByteArrayLittleEndian(input, scratchBuf, 0);
                byte[] expectedArray = BitConverter.GetBytes(input);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(scratchBuf, 0, expectedArray, 0, expectedArray.Length));
            }
        }

        [TestMethod]
        public void TestBinaryConversionFloat()
        {
            IRandom rand = new FastRandom(87655);
            byte[] scratchBuf = new byte[2 + 8];
            for (int loop = 0; loop < 10000; loop++)
            {
                float input = (rand.NextFloat() - 0.5f) * 1000000f;

                BinaryHelpers.FloatToByteArrayLittleEndian(input, scratchBuf, 2);
                float output = BinaryHelpers.ByteArrayToFloatLittleEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.FloatToByteArrayBigEndian(input, scratchBuf, 2);
                output = BinaryHelpers.ByteArrayToFloatBigEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);
            }
        }

        [TestMethod]
        public void TestBinaryConversionDouble()
        {
            byte[] pi = new byte[8];
            BinaryHelpers.DoubleToByteArrayBigEndian(Math.PI, pi, 0);
            Assert.AreEqual("400921FB54442D18", BinaryHelpers.ToHexString(pi, 0, 8));
            BinaryHelpers.DoubleToByteArrayLittleEndian(Math.PI, pi, 0);
            Assert.AreEqual("182D4454FB210940", BinaryHelpers.ToHexString(pi, 0, 8));

            IRandom rand = new FastRandom(16);
            byte[] scratchBuf = new byte[2 + 8];
            for (int loop = 0; loop < 10000; loop++)
            {
                double input = (rand.NextDouble() - 0.5) * 1000000d;

                BinaryHelpers.DoubleToByteArrayLittleEndian(input, scratchBuf, 2);
                double output = BinaryHelpers.ByteArrayToDoubleLittleEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);

                BinaryHelpers.DoubleToByteArrayBigEndian(input, scratchBuf, 2);
                output = BinaryHelpers.ByteArrayToDoubleBigEndian(scratchBuf, 2);
                Assert.AreEqual(input, output);
            }
        }

        [TestMethod]
        public void TestBinaryConversionDecimal()
        {
            IRandom rand = new FastRandom(16);
            byte[] scratchBuf = new byte[2 + 16];
            for (int loop = 0; loop < 10000; loop++)
            {
                decimal input = (decimal)((rand.NextInt() - 0.5)) * 1834029858401495541M;

                BinaryHelpers.DecimalToByteArray(input, scratchBuf, 2);
                decimal output = BinaryHelpers.ByteArrayToDecimal(scratchBuf, 2);
                Assert.AreEqual(input, output);
            }
        }

        [TestMethod]
        public void TestBinaryConversionToFromHexString()
        {
            IRandom rand = new FastRandom(74565);
            byte[] scratchBuf = new byte[16];
            for (int loop = 0; loop < 10000; loop++)
            {
                rand.NextBytes(scratchBuf);
                string hexString = BinaryHelpers.ToHexString(scratchBuf);
                byte[] parsedBinary = BinaryHelpers.FromHexString(hexString);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(scratchBuf, parsedBinary));
            }

            for (int loop = 0; loop < 10000; loop++)
            {
                rand.NextBytes(scratchBuf);
                string hexString = BinaryHelpers.ToHexString(scratchBuf).ToLowerInvariant();
                byte[] parsedBinary = BinaryHelpers.FromHexString(hexString);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(scratchBuf, parsedBinary));
            }

            for (int loop = 0; loop < 10000; loop++)
            {
                rand.NextBytes(scratchBuf);
                string hexString = BinaryHelpers.ToHexString(scratchBuf).ToUpperInvariant();
                byte[] parsedBinary = BinaryHelpers.FromHexString(hexString);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(scratchBuf, parsedBinary));
            }
        }

        [TestMethod]
        public void TestWriteBase64ToStringBuilder()
        {
            FastRandom rand = new FastRandom(534);
            StringBuilder builder = new StringBuilder();
            int maxDataSize = 300010;
            byte[] buffer = new byte[maxDataSize];

            for (int test = 0; test < 20; test++)
            {
                int padding = rand.NextInt(0, 10);
                int dataSize = rand.NextInt(1, 300000);
                rand.NextBytes(buffer, padding, dataSize);
                string expected = Convert.ToBase64String(buffer, padding, dataSize);
                builder.Clear();
                BinaryHelpers.EncodeBase64ToStringBuilder(buffer, padding, dataSize, builder);
                string actual = builder.ToString();
                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void TestReverseEndianness_Int16()
        {
            IRandom rand = new FastRandom(8509234);
            byte[] inputBytes = new byte[1000 * sizeof(short)];
            rand.NextBytes(inputBytes);
            byte[] outputBytes = new byte[inputBytes.Length];
            Array.Copy(inputBytes, 0, outputBytes, 0, inputBytes.Length);
            for (int c = 0; c < outputBytes.Length; c += sizeof(short))
            {
                Array.Reverse(outputBytes, c, sizeof(short));
            }

            Span<short> inputValues = MemoryMarshal.Cast<byte, short>(inputBytes.AsSpan());
            Span<short> expectedOutputValues = MemoryMarshal.Cast<byte, short>(outputBytes.AsSpan());
            for (int c = 0; c < inputValues.Length; c++)
            {
                Assert.AreEqual(expectedOutputValues[c], BinaryHelpers.ReverseEndianness(inputValues[c]));
            }
        }

        [TestMethod]
        public void TestReverseEndianness_UInt16()
        {
            IRandom rand = new FastRandom(8509234);
            byte[] inputBytes = new byte[1000 * sizeof(ushort)];
            rand.NextBytes(inputBytes);
            byte[] outputBytes = new byte[inputBytes.Length];
            Array.Copy(inputBytes, 0, outputBytes, 0, inputBytes.Length);
            for (int c = 0; c < outputBytes.Length; c += sizeof(ushort))
            {
                Array.Reverse(outputBytes, c, sizeof(ushort));
            }

            Span<ushort> inputValues = MemoryMarshal.Cast<byte, ushort>(inputBytes.AsSpan());
            Span<ushort> expectedOutputValues = MemoryMarshal.Cast<byte, ushort>(outputBytes.AsSpan());
            for (int c = 0; c < inputValues.Length; c++)
            {
                Assert.AreEqual(expectedOutputValues[c], BinaryHelpers.ReverseEndianness(inputValues[c]));
            }
        }

        [TestMethod]
        public void TestReverseEndianness_Int32()
        {
            IRandom rand = new FastRandom(8509234);
            byte[] inputBytes = new byte[1000 * sizeof(int)];
            rand.NextBytes(inputBytes);
            byte[] outputBytes = new byte[inputBytes.Length];
            Array.Copy(inputBytes, 0, outputBytes, 0, inputBytes.Length);
            for (int c = 0; c < outputBytes.Length; c += sizeof(int))
            {
                Array.Reverse(outputBytes, c, sizeof(int));
            }

            Span<int> inputValues = MemoryMarshal.Cast<byte, int>(inputBytes.AsSpan());
            Span<int> expectedOutputValues = MemoryMarshal.Cast<byte, int>(outputBytes.AsSpan());
            for (int c = 0; c < inputValues.Length; c++)
            {
                Assert.AreEqual(expectedOutputValues[c], BinaryHelpers.ReverseEndianness(inputValues[c]));
            }
        }

        [TestMethod]
        public void TestReverseEndianness_UInt32()
        {
            IRandom rand = new FastRandom(8509234);
            byte[] inputBytes = new byte[1000 * sizeof(uint)];
            rand.NextBytes(inputBytes);
            byte[] outputBytes = new byte[inputBytes.Length];
            Array.Copy(inputBytes, 0, outputBytes, 0, inputBytes.Length);
            for (int c = 0; c < outputBytes.Length; c += sizeof(uint))
            {
                Array.Reverse(outputBytes, c, sizeof(uint));
            }

            Span<uint> inputValues = MemoryMarshal.Cast<byte, uint>(inputBytes.AsSpan());
            Span<uint> expectedOutputValues = MemoryMarshal.Cast<byte, uint>(outputBytes.AsSpan());
            for (int c = 0; c < inputValues.Length; c++)
            {
                Assert.AreEqual(expectedOutputValues[c], BinaryHelpers.ReverseEndianness(inputValues[c]));
            }
        }

        [TestMethod]
        public void TestReverseEndianness_Int64()
        {
            IRandom rand = new FastRandom(8509234);
            byte[] inputBytes = new byte[1000 * sizeof(long)];
            rand.NextBytes(inputBytes);
            byte[] outputBytes = new byte[inputBytes.Length];
            Array.Copy(inputBytes, 0, outputBytes, 0, inputBytes.Length);
            for (int c = 0; c < outputBytes.Length; c += sizeof(long))
            {
                Array.Reverse(outputBytes, c, sizeof(long));
            }

            Span<long> inputValues = MemoryMarshal.Cast<byte, long>(inputBytes.AsSpan());
            Span<long> expectedOutputValues = MemoryMarshal.Cast<byte, long>(outputBytes.AsSpan());
            for (int c = 0; c < inputValues.Length; c++)
            {
                Assert.AreEqual(expectedOutputValues[c], BinaryHelpers.ReverseEndianness(inputValues[c]));
            }
        }

        [TestMethod]
        public void TestReverseEndianness_UInt64()
        {
            IRandom rand = new FastRandom(8509234);
            byte[] inputBytes = new byte[1000 * sizeof(ulong)];
            rand.NextBytes(inputBytes);
            byte[] outputBytes = new byte[inputBytes.Length];
            Array.Copy(inputBytes, 0, outputBytes, 0, inputBytes.Length);
            for (int c = 0; c < outputBytes.Length; c += sizeof(ulong))
            {
                Array.Reverse(outputBytes, c, sizeof(ulong));
            }

            Span<ulong> inputValues = MemoryMarshal.Cast<byte, ulong>(inputBytes.AsSpan());
            Span<ulong> expectedOutputValues = MemoryMarshal.Cast<byte, ulong>(outputBytes.AsSpan());
            for (int c = 0; c < inputValues.Length; c++)
            {
                Assert.AreEqual(expectedOutputValues[c], BinaryHelpers.ReverseEndianness(inputValues[c]));
            }
        }

        [TestMethod]
        public void TestBinaryHelpersGetMemoryAlignment()
        {
            byte[] alignedByteBuffer = new byte[100];
            Assert.AreEqual(0, BinaryHelpers.GetMemoryAlignment<byte>(alignedByteBuffer.AsSpan(), 8));
            Assert.AreEqual(7, BinaryHelpers.GetMemoryAlignment<byte>(alignedByteBuffer.AsSpan().Slice(1), 8));
            Assert.AreEqual(5, BinaryHelpers.GetMemoryAlignment<byte>(alignedByteBuffer.AsSpan().Slice(3), 8));
            Assert.AreEqual(1, BinaryHelpers.GetMemoryAlignment<byte>(alignedByteBuffer.AsSpan().Slice(7), 8));
            Assert.AreEqual(0, BinaryHelpers.GetMemoryAlignment<byte>(alignedByteBuffer.AsSpan().Slice(8), 8));
        }
     }
}
