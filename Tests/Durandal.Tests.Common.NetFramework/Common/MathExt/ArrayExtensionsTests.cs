using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.MathExt
{
    [TestClass]
    public class ArrayExtensionsTests
    {
        [TestMethod]
        public void TestArrayExtensionsWriteZeroes_Byte()
        {
            const int FIELD_SIZE = 100000;
            const int NUM_PASSES = 20;
            byte[] expectedField = new byte[FIELD_SIZE];
            byte[] originalField = new byte[FIELD_SIZE];
            byte[] field = new byte[FIELD_SIZE];
            IRandom rand = new FastRandom(64);
            for (int pass = 0; pass < NUM_PASSES; pass++)
            {
                rand.NextBytes(field);
                field.AsSpan().CopyTo(originalField);
                field.AsSpan().CopyTo(expectedField);

                int offset = rand.NextInt(0, FIELD_SIZE - 2);
                int count = rand.NextInt(1, FIELD_SIZE - offset);
                expectedField.AsSpan(offset, count).Fill(0);

                ArrayExtensions.WriteZeroes(field, offset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<byte>(field, offset, count));
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<byte>(field, offset, FIELD_SIZE - offset), 0, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                int splitOffset = rand.NextInt(0, offset);
                ArrayExtensions.WriteZeroes(new ArraySegment<byte>(field, offset - splitOffset, count + splitOffset), splitOffset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));
            }
        }

        [TestMethod]
        public void TestArrayExtensionsWriteZeroes_Int32()
        {
            const int FIELD_SIZE = 50000;
            const int NUM_PASSES = 20;
            int[] expectedField = new int[FIELD_SIZE];
            int[] originalField = new int[FIELD_SIZE];
            int[] field = new int[FIELD_SIZE];
            IRandom rand = new FastRandom(981821);
            for (int pass = 0; pass < NUM_PASSES; pass++)
            {
                for (int c = 0; c < FIELD_SIZE; c++)
                {
                    field[c] = rand.NextInt();
                }

                field.AsSpan().CopyTo(originalField);
                field.AsSpan().CopyTo(expectedField);

                int offset = rand.NextInt(0, FIELD_SIZE - 2);
                int count = rand.NextInt(1, FIELD_SIZE - offset);
                expectedField.AsSpan(offset, count).Fill(0);

                ArrayExtensions.WriteZeroes(field, offset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<int>(field, offset, count));
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<int>(field, offset, FIELD_SIZE - offset), 0, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                int splitOffset = rand.NextInt(0, offset);
                ArrayExtensions.WriteZeroes(new ArraySegment<int>(field, offset - splitOffset, count + splitOffset), splitOffset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));
            }
        }

        [TestMethod]
        public void TestArrayExtensionsWriteZeroes_Uint32()
        {
            const int FIELD_SIZE = 50000;
            const int NUM_PASSES = 20;
            uint[] expectedField = new uint[FIELD_SIZE];
            uint[] originalField = new uint[FIELD_SIZE];
            uint[] field = new uint[FIELD_SIZE];
            IRandom rand = new FastRandom(53895);
            for (int pass = 0; pass < NUM_PASSES; pass++)
            {
                for (int c = 0; c < FIELD_SIZE; c++)
                {
                    field[c] = (uint)rand.NextInt64();
                }

                field.AsSpan().CopyTo(originalField);
                field.AsSpan().CopyTo(expectedField);

                int offset = rand.NextInt(0, FIELD_SIZE - 2);
                int count = rand.NextInt(1, FIELD_SIZE - offset);
                expectedField.AsSpan(offset, count).Fill(0);

                ArrayExtensions.WriteZeroes(field, offset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<uint>(field, offset, count));
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<uint>(field, offset, FIELD_SIZE - offset), 0, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                int splitOffset = rand.NextInt(0, offset);
                ArrayExtensions.WriteZeroes(new ArraySegment<uint>(field, offset - splitOffset, count + splitOffset), splitOffset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));
            }
        }

        [TestMethod]
        public void TestArrayExtensionsWriteZeroes_Int16()
        {
            const int FIELD_SIZE = 50000;
            const int NUM_PASSES = 20;
            short[] expectedField = new short[FIELD_SIZE];
            short[] originalField = new short[FIELD_SIZE];
            short[] field = new short[FIELD_SIZE];
            IRandom rand = new FastRandom(17887);
            for (int pass = 0; pass < NUM_PASSES; pass++)
            {
                for (int c = 0; c < FIELD_SIZE; c++)
                {
                    field[c] = (short)rand.NextInt(short.MinValue, short.MaxValue);
                }

                field.AsSpan().CopyTo(originalField);
                field.AsSpan().CopyTo(expectedField);

                int offset = rand.NextInt(0, FIELD_SIZE - 2);
                int count = rand.NextInt(1, FIELD_SIZE - offset);
                expectedField.AsSpan(offset, count).Fill(0);

                ArrayExtensions.WriteZeroes(field, offset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<short>(field, offset, count));
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<short>(field, offset, FIELD_SIZE - offset), 0, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                int splitOffset = rand.NextInt(0, offset);
                ArrayExtensions.WriteZeroes(new ArraySegment<short>(field, offset - splitOffset, count + splitOffset), splitOffset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));
            }
        }

        [TestMethod]
        public void TestArrayExtensionsWriteZeroes_Int64()
        {
            const int FIELD_SIZE = 50000;
            const int NUM_PASSES = 20;
            long[] expectedField = new long[FIELD_SIZE];
            long[] originalField = new long[FIELD_SIZE];
            long[] field = new long[FIELD_SIZE];
            IRandom rand = new FastRandom(431);
            for (int pass = 0; pass < NUM_PASSES; pass++)
            {
                for (int c = 0; c < FIELD_SIZE; c++)
                {
                    field[c] = rand.NextInt64();
                }

                field.AsSpan().CopyTo(originalField);
                field.AsSpan().CopyTo(expectedField);

                int offset = rand.NextInt(0, FIELD_SIZE - 2);
                int count = rand.NextInt(1, FIELD_SIZE - offset);
                expectedField.AsSpan(offset, count).Fill(0);

                ArrayExtensions.WriteZeroes(field, offset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<long>(field, offset, count));
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<long>(field, offset, FIELD_SIZE - offset), 0, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                int splitOffset = rand.NextInt(0, offset);
                ArrayExtensions.WriteZeroes(new ArraySegment<long>(field, offset - splitOffset, count + splitOffset), splitOffset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));
            }
        }

        [TestMethod]
        public void TestArrayExtensionsWriteZeroes_Float32()
        {
            const int FIELD_SIZE = 50000;
            const int NUM_PASSES = 20;
            float[] expectedField = new float[FIELD_SIZE];
            float[] originalField = new float[FIELD_SIZE];
            float[] field = new float[FIELD_SIZE];
            IRandom rand = new FastRandom(9428);
            for (int pass = 0; pass < NUM_PASSES; pass++)
            {
                for (int c = 0; c < FIELD_SIZE; c++)
                {
                    field[c] = rand.NextFloat();
                }

                field.AsSpan().CopyTo(originalField);
                field.AsSpan().CopyTo(expectedField);

                int offset = rand.NextInt(0, FIELD_SIZE - 2);
                int count = rand.NextInt(1, FIELD_SIZE - offset);
                expectedField.AsSpan(offset, count).Fill(0);

                ArrayExtensions.WriteZeroes(field, offset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<float>(field, offset, count));
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<float>(field, offset, FIELD_SIZE - offset), 0, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                int splitOffset = rand.NextInt(0, offset);
                ArrayExtensions.WriteZeroes(new ArraySegment<float>(field, offset - splitOffset, count + splitOffset), splitOffset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));
            }
        }

        [TestMethod]
        public void TestArrayExtensionsWriteZeroes_Float64()
        {
            const int FIELD_SIZE = 50000;
            const int NUM_PASSES = 20;
            double[] expectedField = new double[FIELD_SIZE];
            double[] originalField = new double[FIELD_SIZE];
            double[] field = new double[FIELD_SIZE];
            IRandom rand = new FastRandom(62888);
            for (int pass = 0; pass < NUM_PASSES; pass++)
            {
                for (int c = 0; c < FIELD_SIZE; c++)
                {
                    field[c] = rand.NextDouble();
                }

                field.AsSpan().CopyTo(originalField);
                field.AsSpan().CopyTo(expectedField);

                int offset = rand.NextInt(0, FIELD_SIZE - 2);
                int count = rand.NextInt(1, FIELD_SIZE - offset);
                expectedField.AsSpan(offset, count).Fill(0);

                ArrayExtensions.WriteZeroes(field, offset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<double>(field, offset, count));
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                ArrayExtensions.WriteZeroes(new ArraySegment<double>(field, offset, FIELD_SIZE - offset), 0, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));

                originalField.AsSpan().CopyTo(field);
                int splitOffset = rand.NextInt(0, offset);
                ArrayExtensions.WriteZeroes(new ArraySegment<double>(field, offset - splitOffset, count + splitOffset), splitOffset, count);
                Assert.IsTrue(ArrayExtensions.ArrayEquals(expectedField, 0, field, 0, FIELD_SIZE));
            }
        }
    }
}
