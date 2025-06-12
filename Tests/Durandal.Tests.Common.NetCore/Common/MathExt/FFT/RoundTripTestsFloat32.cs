
namespace Durandal.Tests.Common.MathExt.FFT
{
    using Durandal.Common.MathExt;
    using Durandal.Common.MathExt.FFT;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;

    [TestClass]
    public class RoundTripTestsFloat32   
    {
        private const int MAX_LENGTH = 4096;
        private const float epsilon = 2e-6f;

        [TestMethod]
        public void TestReal()
        {
            float errsum = 0;
            for (int length = 1; length <= MAX_LENGTH; ++length)
            {
                //Console.WriteLine("testing real " + length);
                float[] data = new float[2 * length];
                float[] odata = new float[2 * length];
                fill_random(odata, length);
                odata.AsSpan(0, length).CopyTo(data);
                using (IReal1DFFTPlanFloat32 plan = FFTPlanFactory.Create1DRealFFTPlanFloat32(length))
                {
                    plan.Forward(data, 1.0f);
                    plan.Backward(data, 1.0f / length);
                }

                float err = errcalc(data, odata, length);
                Assert.IsTrue(err < epsilon, $"problem at real length {length}: {err}");
                errsum += err;
            }

            Console.WriteLine($"errsum: {errsum}");
        }

        [TestMethod]
        public void TestComplex()
        {
            float errsum = 0;

            for (int length = 1; length <= MAX_LENGTH; ++length)
            {
                //Console.WriteLine("testing ComplexF " + length);
                ComplexF[] data = new ComplexF[length];
                ComplexF[] odata = new ComplexF[length];
                fill_random(odata, length);
                odata.AsSpan(0, length).CopyTo(data);
                using (IComplex1DFFTPlanFloat32 plan = FFTPlanFactory.Create1DComplexFFTPlanFloat32(length))
                {
                    plan.Forward(data, 1.0f);
                    plan.Backward(data, 1.0f / length);
                }
                float err = errcalc(data, odata, length);
                Assert.IsTrue(err < epsilon, $"problem at complex length {length}: {err}");
                errsum += err;
            }

            Console.WriteLine($"errsum: {errsum}");
        }

        private static void fill_random(Span<float> data, int length)
        {
            for (int m = 0; m < length; ++m)
            {
                data[m] = (float)(Random.Shared.NextDouble() - 0.5);
            }
        }

        private static void fill_random(Span<ComplexF> data, int length)
        {
            for (int m = 0; m < length; ++m)
            {
                data[m].Re = (float)(Random.Shared.NextDouble() - 0.5);
                data[m].Im = (float)(Random.Shared.NextDouble() - 0.5);
            }
        }

        private static float errcalc(Span<float> data, Span<float> odata, int length)
        {
            float sum = 0, errsum = 0;
            for (int m = 0; m < length; ++m)
            {
                errsum += (data[m] - odata[m]) * (data[m] - odata[m]);
                sum += odata[m] * odata[m];
            }

            return (float)Math.Sqrt(errsum / sum);
        }

        private static float errcalc(Span<ComplexF> data, Span<ComplexF> odata, int length)
        {
            float sum = 0, errsum = 0;
            for (int m = 0; m < length; ++m)
            {
                errsum += (data[m].Re - odata[m].Re) * (data[m].Re - odata[m].Re);
                errsum += (data[m].Im - odata[m].Im) * (data[m].Im - odata[m].Im);
                sum += odata[m].Re * odata[m].Re;
                sum += odata[m].Im * odata[m].Im;
            }

            return (float)Math.Sqrt(errsum / sum);
        }
    }
}