using Durandal.Common.Collections;
using Durandal.Common.IO.Crc;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using Durandal.Extensions.Compression.Crc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Compression.Crc
{
    [TestClass]
    public class CRC32C_ARMTests
    {
        private static readonly int INPUT_DATA_LENGTH = 5197;
        private static readonly int INPUT_DATA_LENGTH_NPOT = 4096;
        private static readonly uint ExpectedCrcFull = 3381176338U;
        private static readonly uint ExpectedCrcNPOT = 2624716338U;

        private static byte[] InputData;

        [ClassInitialize]
        public static void TestSetup(TestContext context)
        {
            InputData = new byte[INPUT_DATA_LENGTH];
            for (int c = 0; c < INPUT_DATA_LENGTH; c++)
            {
                InputData[c] = (byte)(c % 256);
            }
        }

        [TestMethod]
        public void TestCRC32TestVector_ARM32()
        {
            if (!Crc32.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM hardware supporting CRC32 instructions");
            }

            byte[] input = BinaryHelpers.FromHexString("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F202122232425262728292A2B2C2D2E2F");
            ICRC32C crc = new ManagedCRC32C_ARM();
            CRC32CState state = new CRC32CState();
            crc.Slurp(ref state, input.AsSpan());
            Assert.AreEqual(0x3C25332AU, state.Checksum);
        }

        [TestMethod]
        public void TestCRC32TestVector_ARM64()
        {
            if (!Crc32.Arm64.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM64 hardware supporting CRC32 instructions");
            }

            byte[] input = BinaryHelpers.FromHexString("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F202122232425262728292A2B2C2D2E2F");
            ICRC32C crc = new ManagedCRC32C_ARM64();
            CRC32CState state = new CRC32CState();
            crc.Slurp(ref state, input.AsSpan());
            Assert.AreEqual(0x3C25332AU, state.Checksum);
        }

        [TestMethod]
        public void TestCRC32AlignedBlock_ARM32()
        {
            if (!Crc32.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM hardware supporting CRC32 instructions");
            }

            TestCRCBasicAlignedBlock(new ManagedCRC32C_ARM());
        }

        [TestMethod]
        public void TestCRC32AlignedBlock_ARM64()
        {
            if (!Crc32.Arm64.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM64 hardware supporting CRC32 instructions");
            }

            TestCRCBasicAlignedBlock(new ManagedCRC32C_ARM64());
        }

        private void TestCRCBasicAlignedBlock(ICRC32C crc)
        {
            CRC32CState state = new CRC32CState();
            crc.Slurp(ref state, InputData.AsSpan(0, INPUT_DATA_LENGTH_NPOT));
            Assert.AreEqual(ExpectedCrcNPOT, state.Checksum);
        }

        [TestMethod]
        public void TestCRC32Aligned_ARM32()
        {
            if (!Crc32.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM hardware supporting CRC32 instructions");
            }

            TestCRCBasicAligned(new ManagedCRC32C_ARM());
        }

        [TestMethod]
        public void TestCRC32Aligned_ARM64()
        {
            if (!Crc32.Arm64.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM64 hardware supporting CRC32 instructions");
            }

            TestCRCBasicAligned(new ManagedCRC32C_ARM64());
        }

        private void TestCRCBasicAligned(ICRC32C crc)
        {
            CRC32CState state = new CRC32CState();
            crc.Slurp(ref state, InputData.AsSpan(0, INPUT_DATA_LENGTH));
            Assert.AreEqual(ExpectedCrcFull, state.Checksum);
        }

        [TestMethod]
        public void TestCRC32Unaligned_ARM32()
        {
            if (!Crc32.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM hardware supporting CRC32 instructions");
            }

            TestCRCBasicUnaligned(new ManagedCRC32C_ARM());
        }

        [TestMethod]
        public void TestCRC32Unaligned_ARM64()
        {
            if (!Crc32.Arm64.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM64 hardware supporting CRC32 instructions");
            }

            TestCRCBasicUnaligned(new ManagedCRC32C_ARM64());
        }

        private void TestCRCBasicUnaligned(ICRC32C crc)
        {
            IRandom rand = new FastRandom();
            byte[] field = new byte[INPUT_DATA_LENGTH + 16];
            for (int offset = 0; offset < 16; offset++)
            {
                CRC32CState state = new CRC32CState();
                rand.NextBytes(field);
                ArrayExtensions.MemCopy(InputData, 0, field, offset, INPUT_DATA_LENGTH);
                crc.Slurp(ref state, field.AsSpan(offset, INPUT_DATA_LENGTH));
                Assert.AreEqual(ExpectedCrcFull, state.Checksum);
            }
        }

        [TestMethod]
        public void TestCRC32UnalignedShort_ARM32()
        {
            if (!Crc32.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM hardware supporting CRC32 instructions");
            }

            TestCRCBasicUnalignedShort(new ManagedCRC32C_ARM());
        }

        [TestMethod]
        public void TestCRC32UnalignedShort_ARM64()
        {
            if (!Crc32.Arm64.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM64 hardware supporting CRC32 instructions");
            }

            TestCRCBasicUnalignedShort(new ManagedCRC32C_ARM64());
        }

        private void TestCRCBasicUnalignedShort(ICRC32C crc)
        {
            IRandom rand = new FastRandom();
            byte[] field = new byte[INPUT_DATA_LENGTH + 16];
            for (int offset = 0; offset < 16; offset++)
            {
                CRC32CState state = new CRC32CState();
                rand.NextBytes(field);
                ArrayExtensions.MemCopy(InputData, 0, field, offset, INPUT_DATA_LENGTH);
                for (int idx = 0; idx < INPUT_DATA_LENGTH; idx++)
                {
                    crc.Slurp(ref state, field.AsSpan(idx + offset, 1));
                }

                Assert.AreEqual(ExpectedCrcFull, state.Checksum);
            }
        }

        [TestMethod]
        public void TestCRC32RandomBlocks_ARM32()
        {
            if (!Crc32.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM hardware supporting CRC32 instructions");
            }

            TestCRCRandomBlocks(new ManagedCRC32C_ARM());
        }

        [TestMethod]
        public void TestCRC32RandomBlocks_ARM64()
        {
            if (!Crc32.Arm64.IsSupported)
            {
                Assert.Inconclusive("Test can only run on ARM64 hardware supporting CRC32 instructions");
            }

            TestCRCRandomBlocks(new ManagedCRC32C_ARM64());
        }

        private void TestCRCRandomBlocks(ICRC32C crc)
        {
            CRC32CState state = new CRC32CState();
            IRandom rand = new FastRandom(5519);
            int idx = 0;
            while (idx < INPUT_DATA_LENGTH)
            {
                if (rand.NextFloat() < 0.2f)
                {
                    crc.Slurp(ref state, InputData[idx++]);
                }
                else
                {
                    int length = Math.Min(INPUT_DATA_LENGTH - idx, rand.NextInt(0, 256));
                    crc.Slurp(ref state, InputData.AsSpan(idx, length));
                    idx += length;
                }
            }

            Assert.AreEqual(ExpectedCrcFull, state.Checksum);
        }
    }
}
