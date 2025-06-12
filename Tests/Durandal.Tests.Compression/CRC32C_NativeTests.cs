using Durandal.Common.Collections;
using Durandal.Common.IO.Crc;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using Durandal.Extensions.Compression.Crc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Compression.Crc
{
    [TestClass]
    public class CRC32C_NativeTests
    {
        private static readonly int INPUT_DATA_LENGTH = 5197;
        private static readonly int INPUT_DATA_LENGTH_NPOT = 4096;
        private static readonly uint ExpectedCrcFull = 3381176338U;
        private static readonly uint ExpectedCrcNPOT = 2624716338U;

        private static byte[] InputData;
        private static ICRC32C _nativeCRC;

        [ClassInitialize]
        public static void TestSetup(TestContext context)
        {
            InputData = new byte[INPUT_DATA_LENGTH];
            for (int c = 0; c < INPUT_DATA_LENGTH; c++)
            {
                InputData[c] = (byte)(c % 256);
            }

            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            if (new CRC32CAccelerator().Apply(DebugLogger.Default))
            {
                ICRC32C nativeCRC = CRC32CFactory.Create();
                if (nativeCRC is NativeCRC32C)
                {
                    _nativeCRC = nativeCRC;
                }
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            new CRC32CAccelerator().Unapply(DebugLogger.Default);
        }

        [TestMethod]
        public void TestCRC32TestVector_Native()
        {
            if (_nativeCRC == null)
            {
                Assert.Inconclusive("No native library found for this current platform");
            }

            byte[] input = BinaryHelpers.FromHexString("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F202122232425262728292A2B2C2D2E2F");
            CRC32CState state = new CRC32CState();
            _nativeCRC.Slurp(ref state, input.AsSpan());
            Assert.AreEqual(0x3C25332AU, state.Checksum);
        }

        [TestMethod]
        public void TestCRC32AlignedBlock_Native()
        {
            if (_nativeCRC == null)
            {
                Assert.Inconclusive("No native library found for this current platform");
            }

            CRC32CState state = new CRC32CState();
            _nativeCRC.Slurp(ref state, InputData.AsSpan(0, INPUT_DATA_LENGTH_NPOT));
            Assert.AreEqual(ExpectedCrcNPOT, state.Checksum);
        }

        [TestMethod]
        public void TestCRC32Aligned_Native()
        {
            if (_nativeCRC == null)
            {
                Assert.Inconclusive("No native library found for this current platform");
            }

            CRC32CState state = new CRC32CState();
            _nativeCRC.Slurp(ref state, InputData.AsSpan(0, INPUT_DATA_LENGTH));
            Assert.AreEqual(ExpectedCrcFull, state.Checksum);
        }

        [TestMethod]
        public void TestCRC32Unaligned_Native()
        {
            if (_nativeCRC == null)
            {
                Assert.Inconclusive("No native library found for this current platform");
            }

            IRandom rand = new FastRandom();
            byte[] field = new byte[INPUT_DATA_LENGTH + 16];
            for (int offset = 0; offset < 16; offset++)
            {
                CRC32CState state = new CRC32CState();
                rand.NextBytes(field);
                ArrayExtensions.MemCopy(InputData, 0, field, offset, INPUT_DATA_LENGTH);
                _nativeCRC.Slurp(ref state, field.AsSpan(offset, INPUT_DATA_LENGTH));
                Assert.AreEqual(ExpectedCrcFull, state.Checksum);
            }
        }

        [TestMethod]
        public void TestCRC32UnalignedShort_Native()
        {
            if (_nativeCRC == null)
            {
                Assert.Inconclusive("No native library found for this current platform");
            }

            IRandom rand = new FastRandom();
            byte[] field = new byte[INPUT_DATA_LENGTH + 16];
            for (int offset = 0; offset < 16; offset++)
            {
                CRC32CState state = new CRC32CState();
                rand.NextBytes(field);
                ArrayExtensions.MemCopy(InputData, 0, field, offset, INPUT_DATA_LENGTH);
                for (int idx = 0; idx < INPUT_DATA_LENGTH; idx++)
                {
                    _nativeCRC.Slurp(ref state, field.AsSpan(idx + offset, 1));
                }

                Assert.AreEqual(ExpectedCrcFull, state.Checksum);
            }
        }

        [TestMethod]
        public void TestCRC32RandomBlocks_Native()
        {
            if (_nativeCRC == null)
            {
                Assert.Inconclusive("No native library found for this current platform");
            }

            CRC32CState state = new CRC32CState();
            IRandom rand = new FastRandom(5519);
            int idx = 0;
            while (idx < INPUT_DATA_LENGTH)
            {
                if (rand.NextFloat() < 0.2f)
                {
                    _nativeCRC.Slurp(ref state, InputData[idx++]);
                }
                else
                {
                    int length = Math.Min(INPUT_DATA_LENGTH - idx, rand.NextInt(0, 256));
                    _nativeCRC.Slurp(ref state, InputData.AsSpan(idx, length));
                    idx += length;
                }
            }

            Assert.AreEqual(ExpectedCrcFull, state.Checksum);
        }
    }
}
