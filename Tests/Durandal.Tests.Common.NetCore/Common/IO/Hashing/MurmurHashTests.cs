using Durandal.Common.IO.Hashing;
using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.IO.Hashing
{
    [TestClass]
    public class MurmurHashTests
    {
        [TestMethod]
        public void TestMurmurHash3TestVectors()
        {
            Assert.AreEqual(0x00000000U, HashInternal(new byte[] { }, 0));
            Assert.AreEqual(0x514E28B7U, HashInternal(new byte[] { }, 1));
            Assert.AreEqual(0x81F16F39U, HashInternal(new byte[] { }, 0xFFFFFFFFU));
            Assert.AreEqual(0x76293B50U, HashInternal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0));
            Assert.AreEqual(0xF55B516BU, HashInternal(new byte[] { 0x21, 0x43, 0x65, 0x87 }, 0));
            Assert.AreEqual(0x2362F9DEU, HashInternal(new byte[] { 0x21, 0x43, 0x65, 0x87 }, 0x5082EDEEU));
            Assert.AreEqual(0x7E4A8634U, HashInternal(new byte[] { 0x21, 0x43, 0x65 }, 0));
            Assert.AreEqual(0xA0F7B07AU, HashInternal(new byte[] { 0x21, 0x43 }, 0));
            Assert.AreEqual(0x72661CF4U, HashInternal(new byte[] { 0x21 }, 0));
            Assert.AreEqual(0x2362F9DEU, HashInternal(new byte[] { 0x00, 0x00, 0x00, 0x00 }, 0));
            Assert.AreEqual(0x85F0B427U, HashInternal(new byte[] { 0x00, 0x00, 0x00}, 0));
            Assert.AreEqual(0x30F4C306U, HashInternal(new byte[] { 0x00, 0x00 }, 0));
            Assert.AreEqual(0x514E28B7U, HashInternal(new byte[] { 0x00 }, 0));

            Assert.AreEqual(0x2362F9DEU, MurmurHash3_32.HashSingleInteger(0, 0));
            Assert.AreEqual(0x76293B50U, MurmurHash3_32.HashSingleInteger(0xFFFFFFFFU, 0));
            Assert.AreEqual(0xF55B516BU, MurmurHash3_32.HashSingleInteger(0x87654321, 0));
            Assert.AreEqual(0x2362F9DEU, MurmurHash3_32.HashSingleInteger(0x87654321, 0x5082EDEEU));
        }

        [TestMethod]
        public void TestMurmurHash3SingleWrites()
        {
            byte[] input = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            const uint seed = 50;
            uint expectedHash = HashInternal(input, seed);

            MurmurHash3_32 hasher = new MurmurHash3_32(seed);

            hasher.Ingest(input, 0, 3);
            hasher.Ingest(input, 3, 4);
            hasher.Ingest(input, 7, 6);
            hasher.Ingest(input, 13, 3);

            uint actualHash = hasher.Finish();
            Assert.AreEqual(expectedHash, actualHash);
        }

        [TestMethod]
        public void TestMurmurHash3PartialWrites()
        {
            IRandom rand = new FastRandom(23665);
            byte[] input = new byte[10000];
            rand.NextBytes(input);
            const uint seed = 50;
            for (int iter = 0; iter < 1000; iter++)
            {
                int used = 0;
                int totalLength = rand.NextInt(0, input.Length);
                MurmurHash3_32 hasher = new MurmurHash3_32(seed);
                hasher.Ingest(input, 0, totalLength);
                uint expectedHash = hasher.Finish();

                hasher = new MurmurHash3_32(seed);
                
                while (used < totalLength)
                {
                    int nextBlockSize = Math.Min(totalLength - used, rand.NextInt(0, 50));
                    hasher.Ingest(input, used, nextBlockSize);
                    used += nextBlockSize;
                }

                uint actualHash = hasher.Finish();
                Assert.AreEqual(expectedHash, actualHash);
            }
        }

        private static uint HashInternal(byte[] data, uint seed)
        {
            MurmurHash3_32 hasher = new MurmurHash3_32(seed);
            hasher.Ingest(data, 0, data.Length);
            return hasher.Finish();
        }
    }
}
