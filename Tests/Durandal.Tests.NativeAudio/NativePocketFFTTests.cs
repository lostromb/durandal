
namespace Durandal.Tests.NativeAudio
{
    using Durandal.Common.Logger;
    using Durandal.Common.MathExt;
    using Durandal.Common.MathExt.FFT;
    using Durandal.Common.Utils.NativePlatform;
    using Durandal.Extensions.NativeAudio.FFT;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;

    [TestClass]
    public class NativePocketFFTTests   
    {
        private const int MAX_LENGTH = 4096;
        private const float epsilon32 = 2e-6f;
        private const double epsilon64 = 2e-15;
        private static bool applySuccess = false;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            NativePlatformUtils.SetGlobalResolver(new NativeLibraryResolverImpl());
            applySuccess = new NativeFFTAccelerator().Apply(DebugLogger.Default);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            new NativeFFTAccelerator().Unapply(DebugLogger.Default);
        }

        [TestMethod]
        public void TestReal32()
        {
            if (!applySuccess)
            {
                Assert.Inconclusive("libpocketfft did not seem to load properly or is not available for this platform; cannot run test");
            }

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
                    Assert.IsInstanceOfType(plan, typeof(PlanNativeRealFloat32));
                    plan.Forward(data, 1.0f);
                    plan.Backward(data, 1.0f / length);
                }

                float err = errcalc(data, odata, length);
                Assert.IsTrue(err < epsilon32, $"problem at real length {length}: {err}");
                errsum += err;
            }

            Console.WriteLine($"errsum: {errsum}");
        }

        [TestMethod]
        public void TestReal64()
        {
            if (!applySuccess)
            {
                Assert.Inconclusive("libpocketfft did not seem to load properly or is not available for this platform; cannot run test");
            }

            double errsum = 0;
            for (int length = 1; length <= MAX_LENGTH; ++length)
            {
                //Console.WriteLine("testing real " + length);
                double[] data = new double[2 * length];
                double[] odata = new double[2 * length];
                fill_random(odata, length);
                odata.AsSpan(0, length).CopyTo(data);
                using (IReal1DFFTPlanFloat64 plan = FFTPlanFactory.Create1DRealFFTPlanFloat64(length))
                {
                    Assert.IsInstanceOfType(plan, typeof(PlanNativeRealFloat64));
                    plan.Forward(data, 1.0);
                    plan.Backward(data, 1.0 / length);
                }

                double err = errcalc(data, odata, length);
                Assert.IsTrue(err < epsilon64, $"problem at real length {length}: {err}");
                errsum += err;
            }

            Console.WriteLine($"errsum: {errsum}");
        }

        [TestMethod]
        public void TestComplex32()
        {
            if (!applySuccess)
            {
                Assert.Inconclusive("libpocketfft did not seem to load properly or is not available for this platform; cannot run test");
            }

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
                    Assert.IsInstanceOfType(plan, typeof(PlanNativeComplexFloat32));
                    plan.Forward(data, 1.0f);
                    plan.Backward(data, 1.0f / length);
                }
                float err = errcalc(data, odata, length);
                Assert.IsTrue(err < epsilon32, $"problem at complex length {length}: {err}");
                errsum += err;
            }

            Console.WriteLine($"errsum: {errsum}");
        }

        [TestMethod]
        public void TestComplex64()
        {
            if (!applySuccess)
            {
                Assert.Inconclusive("libpocketfft did not seem to load properly or is not available for this platform; cannot run test");
            }

            double errsum = 0;
            for (int length = 1; length <= MAX_LENGTH; ++length)
            {
                //Console.WriteLine("testing Complex " + length);
                Complex[] data = new Complex[length];
                Complex[] odata = new Complex[length];
                fill_random(odata, length);
                odata.AsSpan(0, length).CopyTo(data);
                using (IComplex1DFFTPlanFloat64 plan = FFTPlanFactory.Create1DComplexFFTPlanFloat64(length))
                {
                    Assert.IsInstanceOfType(plan, typeof(PlanNativeComplexFloat64));
                    plan.Forward(data, 1.0);
                    plan.Backward(data, 1.0 / length);
                }
                double err = errcalc(data, odata, length);
                Assert.IsTrue(err < epsilon64, $"problem at complex length {length}: {err}");
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

        private static void fill_random(Span<double> data, int length)
        {
            for (int m = 0; m < length; ++m)
            {
                data[m] = Random.Shared.NextDouble() - 0.5;
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

        private static void fill_random(Span<Complex> data, int length)
        {
            for (int m = 0; m < length; ++m)
            {
                data[m].Re = Random.Shared.NextDouble() - 0.5;
                data[m].Im = Random.Shared.NextDouble() - 0.5;
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

        private static double errcalc(Span<double> data, Span<double> odata, int length)
        {
            double sum = 0, errsum = 0;
            for (int m = 0; m < length; ++m)
            {
                errsum += (data[m] - odata[m]) * (data[m] - odata[m]);
                sum += odata[m] * odata[m];
            }

            return Math.Sqrt(errsum / sum);
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

        private static double errcalc(Span<Complex> data, Span<Complex> odata, int length)
        {
            double sum = 0, errsum = 0;
            for (int m = 0; m < length; ++m)
            {
                errsum += (data[m].Re - odata[m].Re) * (data[m].Re - odata[m].Re);
                errsum += (data[m].Im - odata[m].Im) * (data[m].Im - odata[m].Im);
                sum += odata[m].Re * odata[m].Re;
                sum += odata[m].Im * odata[m].Im;
            }

            return Math.Sqrt(errsum / sum);
        }
    }
}