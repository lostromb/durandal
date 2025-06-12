using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Collections
{
    [TestClass]
    public class ArrayExtensionsTests
    {
        [TestMethod]
        public void TestArrayExtensionSort()
        {
            int[] keys = new int[] { 5, 3, 1, 4, 2 };
            string[] values = new string[] { "five", "three", "one", "four", "two" };
            ArrayExtensions.Sort(keys, values);
            Assert.AreEqual(1, keys[0]);
            Assert.AreEqual(2, keys[1]);
            Assert.AreEqual(3, keys[2]);
            Assert.AreEqual(4, keys[3]);
            Assert.AreEqual(5, keys[4]);
            Assert.AreEqual("one", values[0]);
            Assert.AreEqual("two", values[1]);
            Assert.AreEqual("three", values[2]);
            Assert.AreEqual("four", values[3]);
            Assert.AreEqual("five", values[4]);
        }

        [TestMethod]
        public void TestArrayExtensionSortWithComparer()
        {
            int[] keys = new int[] { 5, 3, 1, 4, 2 };
            string[] values = new string[] { "five", "three", "one", "four", "two" };
            ArrayExtensions.Sort(keys, values, new DescendingComparer());
            Assert.AreEqual(5, keys[0]);
            Assert.AreEqual(4, keys[1]);
            Assert.AreEqual(3, keys[2]);
            Assert.AreEqual(2, keys[3]);
            Assert.AreEqual(1, keys[4]);
            Assert.AreEqual("one", values[4]);
            Assert.AreEqual("two", values[3]);
            Assert.AreEqual("three", values[2]);
            Assert.AreEqual("four", values[1]);
            Assert.AreEqual("five", values[0]);
        }

        private class DescendingComparer : IComparer<int>
        {
            public int Compare(int x, int y)
            {
                return Math.Sign(y - x);
            }
        }

        [TestMethod]
        public void TestMemCopyMoveSemanticsRight()
        {
            int[] field = new int[101];
            int[] sequence = Enumerable.Range(0, 30).ToArray();
            Array.Copy(sequence, 0, field, 0, 30);
            for (int startIdx = 0; startIdx < field.Length - 30; startIdx++)
            {
                ArrayExtensions.MemCopy(field, startIdx, field, startIdx + 1, 30);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(sequence, 0, field, startIdx + 1, 30));
            }
        }

        [TestMethod]
        public void TestMemCopyMoveSemanticsLeft()
        {
            int[] field = new int[100];
            int[] sequence = Enumerable.Range(0, 30).ToArray();
            Array.Copy(sequence, 0, field, 70, 30);
            for (int startIdx = 70; startIdx > 0; startIdx--)
            {
                ArrayExtensions.MemCopy(field, startIdx, field, startIdx - 1, 30);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(sequence, 0, field, startIdx - 1, 30));
            }
        }

        [TestMethod]
        public void TestMemMoveRight()
        {
            int[] field = new int[101];
            int[] sequence = Enumerable.Range(0, 30).ToArray();
            Array.Copy(sequence, 0, field, 0, 30);
            for (int startIdx = 0; startIdx < field.Length - 30; startIdx++)
            {
                ArrayExtensions.MemMove(field, startIdx, startIdx + 1, 30);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(sequence, 0, field, startIdx + 1, 30));
            }
        }

        [TestMethod]
        public void TestMemMoveLeft()
        {
            int[] field = new int[100];
            int[] sequence = Enumerable.Range(0, 30).ToArray();
            Array.Copy(sequence, 0, field, 70, 30);
            for (int startIdx = 70; startIdx > 0; startIdx--)
            {
                ArrayExtensions.MemMove(field, startIdx, startIdx - 1, 30);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(sequence, 0, field, startIdx - 1, 30));
            }
        }

        [TestMethod]
        public void TestMemCopyRandomMoves()
        {
            int[] field = new int[1000];
            int[] sequence = Enumerable.Range(0, 128).ToArray();
            Array.Copy(sequence, 0, field, 0, sequence.Length);
            int oldIdx = 0;
            IRandom rand = new FastRandom(32844);
            for (int c = 0; c < 10000; c++)
            {
                int newIdx = rand.NextInt(0, field.Length - sequence.Length);
                ArrayExtensions.MemCopy(field, oldIdx, field, newIdx, sequence.Length);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(sequence, 0, field, newIdx, sequence.Length));
                oldIdx = newIdx;
            }
        }

        [TestMethod]
        public void TestMemMoveRandomMoves()
        {
            int[] field = new int[1000];
            int[] sequence = Enumerable.Range(0, 128).ToArray();
            Array.Copy(sequence, 0, field, 0, sequence.Length);
            int oldIdx = 0;
            IRandom rand = new FastRandom(876777);
            for (int c = 0; c < 10000; c++)
            {
                int newIdx = rand.NextInt(0, field.Length - sequence.Length);
                ArrayExtensions.MemMove(field, oldIdx, newIdx, sequence.Length);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(sequence, 0, field, newIdx, sequence.Length));
                oldIdx = newIdx;
            }
        }

        [TestMethod]
        public void TestMemCopyByte()
        {
            byte[] field = new byte[1000];
            byte[] sequence = new byte[128];
            IRandom rand = new FastRandom(65);
            for (int c = 0; c < sequence.Length; c++)
            {
                sequence[c] = (byte)rand.NextInt();
            }

            Array.Copy(sequence, 0, field, 0, sequence.Length);
            int oldIdx = 0;
            for (int c = 0; c < 10000; c++)
            {
                int newIdx = rand.NextInt(0, field.Length - sequence.Length);
                ArrayExtensions.MemCopy(field, oldIdx, field, newIdx, sequence.Length);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(sequence, 0, field, newIdx, sequence.Length));
                oldIdx = newIdx;
            }
        }

        [TestMethod]
        public void TestMemCopyFloat()
        {
            float[] field = new float[1000];
            float[] sequence = new float[128];
            IRandom rand = new FastRandom(8722);
            for (int c = 0; c < sequence.Length; c++)
            {
                sequence[c] = rand.NextFloat();
            }

            Array.Copy(sequence, 0, field, 0, sequence.Length);
            int oldIdx = 0;
            for (int c = 0; c < 10000; c++)
            {
                int newIdx = rand.NextInt(0, field.Length - sequence.Length);
                ArrayExtensions.MemCopy(field, oldIdx, field, newIdx, sequence.Length);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(sequence, 0, field, newIdx, sequence.Length));
                oldIdx = newIdx;
            }
        }

        [TestMethod]
        public void TestMemCopyLong()
        {
            long[] field = new long[1000];
            long[] sequence = new long[128];
            IRandom rand = new FastRandom(553);
            for (int c = 0; c < sequence.Length; c++)
            {
                sequence[c] = ((long)rand.NextInt() << 32) | (long)rand.NextInt();
            }

            Array.Copy(sequence, 0, field, 0, sequence.Length);
            int oldIdx = 0;
            for (int c = 0; c < 10000; c++)
            {
                int newIdx = rand.NextInt(0, field.Length - sequence.Length);
                ArrayExtensions.MemCopy(field, oldIdx, field, newIdx, sequence.Length);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(sequence, 0, field, newIdx, sequence.Length));
                oldIdx = newIdx;
            }
        }

        [TestMethod]
        public void TestMemClearByte()
        {
            byte[] field = new byte[1000];
            IRandom rand = new FastRandom(12211);
            for (int c = 0; c < field.Length; c++)
            {
                field[c] = (byte)rand.NextInt();
            }

            ArrayExtensions.WriteZeroes(field, 64, 128);
            for (int c = 64; c < 64 + 128; c++)
            {
                Assert.AreEqual(0, field[c]);
            }
        }

        [TestMethod]
        public void TestMemClearInt()
        {
            int[] field = new int[1000];
            IRandom rand = new FastRandom(12211);
            for (int c = 0; c < field.Length; c++)
            {
                field[c] = rand.NextInt();
            }

            ArrayExtensions.WriteZeroes(field, 64, 128);
            for (int c = 64; c < 64 + 128; c++)
            {
                Assert.AreEqual(0, field[c]);
            }
        }

        [TestMethod]
        public void TestMemClearFloat()
        {
            float[] field = new float[1000];
            IRandom rand = new FastRandom(12211);
            for (int c = 0; c < field.Length; c++)
            {
                field[c] = rand.NextFloat();
            }

            ArrayExtensions.WriteZeroes(field, 64, 128);
            for (int c = 64; c < 64 + 128; c++)
            {
                Assert.AreEqual(0, field[c]);
            }
        }

        [TestMethod]
        public void TestMemCopyReinterpretCast()
        {
            float[] source = new float[100];
            byte[] target = new byte[400];

            IRandom rand = new FastRandom(8722);
            for (int c = 0; c < source.Length; c++)
            {
                source[c] = rand.NextFloat();
            }

            byte[] expectedTarget = new byte[400];
            Buffer.BlockCopy(source, 85, expectedTarget, 200, 127);

            ArrayExtensions.ReinterpretCastMemCopy(source, 85, target, 200, 127);
            Assert.IsTrue(ArrayExtensions.ArrayEquals(target, expectedTarget));
        }
    }
}
