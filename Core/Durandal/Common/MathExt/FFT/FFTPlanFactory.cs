namespace Durandal.Common.MathExt.FFT
{
    using System;

    /// <summary>
    /// Static factory class which creates FFT (Fast Fourier Transform) implementations.
    /// </summary>
    public static class FFTPlanFactory
    {
        private static Func<int, IReal1DFFTPlanFloat32> _factory_1DReal32 = Internal_Create1DRealFFTPlanFloat32;
        private static Func<int, IReal1DFFTPlanFloat64> _factory_1DReal64 = Internal_Create1DRealFFTPlanFloat64;
        private static Func<int, IComplex1DFFTPlanFloat32> _factory_1DComplex32 = Internal_Create1DComplexFFTPlanFloat32;
        private static Func<int, IComplex1DFFTPlanFloat64> _factory_1DComplex64 = Internal_Create1DComplexFFTPlanFloat64;

        /// <summary>
        /// Creates an FFT plan for 1-dimensional float32 complex to complex transforms.
        /// </summary>
        /// <param name="length">The length of the transform kernel.</param>
        /// <returns>A newly created FFT plan.</returns>
        public static IComplex1DFFTPlanFloat32 Create1DComplexFFTPlanFloat32(int length)
        {
            return _factory_1DComplex32(length);
        }

        /// <summary>
        /// Creates an FFT plan for 1-dimensional float64 complex to complex transforms.
        /// </summary>
        /// <param name="length">The length of the transform kernel.</param>
        /// <returns>A newly created FFT plan.</returns>
        public static IComplex1DFFTPlanFloat64 Create1DComplexFFTPlanFloat64(int length)
        {
            return _factory_1DComplex64(length);
        }

        /// <summary>
        /// Creates an FFT plan for 1-dimensional float32 real to real transforms.
        /// </summary>
        /// <param name="length">The length of the transform kernel.</param>
        /// <returns>A newly created FFT plan.</returns>
        public static IReal1DFFTPlanFloat32 Create1DRealFFTPlanFloat32(int length)
        {
            return _factory_1DReal32(length);
        }

        /// <summary>
        /// Creates an FFT plan for 1-dimensional float64 real to real transforms.
        /// </summary>
        /// <param name="length">The length of the transform kernel.</param>
        /// <returns>A newly created FFT plan.</returns>
        public static IReal1DFFTPlanFloat64 Create1DRealFFTPlanFloat64(int length)
        {
            return _factory_1DReal64(length);
        }

        public static void SetGlobalFactory(Func<int, IReal1DFFTPlanFloat32> factory)
        {
            _factory_1DReal32 = factory ?? Internal_Create1DRealFFTPlanFloat32;
        }

        public static void SetGlobalFactory(Func<int, IReal1DFFTPlanFloat64> factory)
        {
            _factory_1DReal64 = factory ?? Internal_Create1DRealFFTPlanFloat64;
        }

        public static void SetGlobalFactory(Func<int, IComplex1DFFTPlanFloat32> factory)
        {
            _factory_1DComplex32 = factory ?? Internal_Create1DComplexFFTPlanFloat32;
        }

        public static void SetGlobalFactory(Func<int, IComplex1DFFTPlanFloat64> factory)
        {
            _factory_1DComplex64 = factory ?? Internal_Create1DComplexFFTPlanFloat64;
        }

        /// <summary>
        /// Default factory, creates a managed FFT implementation
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        private static IComplex1DFFTPlanFloat32 Internal_Create1DComplexFFTPlanFloat32(int length)
        {
            if (length == 0)
            {
                throw new ArgumentOutOfRangeException("FFT length must be greater than zero");
            }

            if ((length < 50) || (FFTIntrinsics.largest_prime_factor(length) <= Math.Sqrt(length)))
            {
                return new PlanComplexPackedFloat32(length);
            }

            double comp1 = FFTIntrinsics.cost_guess(length);
            double comp2 = 2 * FFTIntrinsics.cost_guess(FFTIntrinsics.good_size(2 * length - 1));
            comp2 *= 1.5; /* fudge factor that appears to give good overall performance */

            if (comp2 < comp1) // use Bluestein
            {
                return new PlanBluesteinFloat32(length);
            }
            else
            {
                return new PlanComplexPackedFloat32(length);
            }
        }

        /// <summary>
        /// Default factory, creates a managed FFT implementation
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        private static IComplex1DFFTPlanFloat64 Internal_Create1DComplexFFTPlanFloat64(int length)
        {
            if (length == 0)
            {
                throw new ArgumentOutOfRangeException("FFT length must be greater than zero");
            }

            if ((length < 50) || (FFTIntrinsics.largest_prime_factor(length) <= Math.Sqrt(length)))
            {
                return new PlanComplexPackedFloat64(length);
            }

            double comp1 = FFTIntrinsics.cost_guess(length);
            double comp2 = 2 * FFTIntrinsics.cost_guess(FFTIntrinsics.good_size(2 * length - 1));
            comp2 *= 1.5; /* fudge factor that appears to give good overall performance */

            if (comp2 < comp1) // use Bluestein
            {
                return new PlanBluesteinFloat64(length);
            }
            else
            {
                return new PlanComplexPackedFloat64(length);
            }
        }

        /// <summary>
        /// Default factory, creates a managed FFT implementation
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        private static IReal1DFFTPlanFloat32 Internal_Create1DRealFFTPlanFloat32(int length)
        {
            if (length == 0)
            {
                throw new ArgumentOutOfRangeException("FFT length must be greater than zero");
            }

            if ((length < 50) || (FFTIntrinsics.largest_prime_factor(length) <= Math.Sqrt(length)))
            {
                return new PlanRealPackedFloat32(length);
            }

            double comp1 = 0.5 * FFTIntrinsics.cost_guess(length);
            double comp2 = 2 * FFTIntrinsics.cost_guess(FFTIntrinsics.good_size(2 * length - 1));
            comp2 *= 1.5; /* fudge factor that appears to give good overall performance */
            if (comp2 < comp1) // use Bluestein
            {
                return new PlanBluesteinFloat32(length);
            }
            else
            {
                return new PlanRealPackedFloat32(length);
            }
        }

        /// <summary>
        /// Default factory, creates a managed FFT implementation
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        private static IReal1DFFTPlanFloat64 Internal_Create1DRealFFTPlanFloat64(int length)
        {
            if (length == 0)
            {
                throw new ArgumentOutOfRangeException("FFT length must be greater than zero");
            }

            if ((length < 50) || (FFTIntrinsics.largest_prime_factor(length) <= Math.Sqrt(length)))
            {
                return new PlanRealPackedFloat64(length);
            }

            double comp1 = 0.5 * FFTIntrinsics.cost_guess(length);
            double comp2 = 2 * FFTIntrinsics.cost_guess(FFTIntrinsics.good_size(2 * length - 1));
            comp2 *= 1.5; /* fudge factor that appears to give good overall performance */
            if (comp2 < comp1) // use Bluestein
            {
                return new PlanBluesteinFloat64(length);
            }
            else
            {
                return new PlanRealPackedFloat64(length);
            }
        }
    }
}