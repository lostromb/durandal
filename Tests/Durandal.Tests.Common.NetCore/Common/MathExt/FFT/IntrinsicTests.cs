namespace Durandal.Tests.Common.MathExt.FFT
{
    using Durandal.Common.MathExt.FFT;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    [TestClass]
    public class IntrinsicTests
    {
        [TestMethod]
        public void TestSpanRefEquals()
        {
            byte[] array = new byte[1000];
            byte[] array2 = new byte[1000];
            Span<byte> a = array.AsSpan();
            Span<byte> b = array.AsSpan();
            Assert.IsTrue(FFTIntrinsics.SpanRefEquals(a, b));
            Assert.IsFalse(FFTIntrinsics.SpanRefEquals(a, b.Slice(1)));
            Assert.IsTrue(FFTIntrinsics.SpanRefEquals(a.Slice(1), b.Slice(1)));
            Assert.IsTrue(FFTIntrinsics.SpanRefEquals(array.AsSpan(), array.AsSpan()));
            Assert.IsFalse(FFTIntrinsics.SpanRefEquals(array.AsSpan(), array2.AsSpan()));
            Assert.IsFalse(FFTIntrinsics.SpanRefEquals(a, Span<byte>.Empty));
            Assert.IsFalse(FFTIntrinsics.SpanRefEquals(Span<byte>.Empty, b));
            Assert.IsTrue(FFTIntrinsics.SpanRefEquals(Span<byte>.Empty, Span<byte>.Empty));
        }

        [TestMethod]
        public void TestCastSingleToDouble()
        {
            for (int length = 0; length < 1000; length++)
            {
                float[] input = new float[length];
                double[] output = new double[length];
                for (int c = 0; c < length; c++)
                {
                    input[c] = Random.Shared.NextSingle();
                }

                FFTIntrinsics.CastSingleToDouble(input.AsSpan(), output.AsSpan());

                for (int c = 0; c < length; c++)
                {
                    Assert.AreEqual(input[c], (float)output[c], 0.00001f);
                }
            }
        }

        [TestMethod]
        public void TestCastDoubleToSingle()
        {
            for (int length = 0; length < 1000; length++)
            {
                double[] input = new double[length];
                float[] output = new float[length];
                for (int c = 0; c < length; c++)
                {
                    input[c] = Random.Shared.NextDouble();
                }

                FFTIntrinsics.CastDoubleToSingle(input.AsSpan(), output.AsSpan());

                for (int c = 0; c < length; c++)
                {
                    Assert.AreEqual(input[c], (double)output[c], 0.00001f);
                }
            }
        }
    }
}
