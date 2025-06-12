using Durandal.Common.Logger;
using Durandal.Common.MathExt.FFT;
using Durandal.Common.Utils.NativePlatform;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Extensions.NativeAudio.FFT
{
    /// <summary>
    /// Accelerates <see cref="FFTPlanFactory" /> using PocketFFT native library.
    /// </summary>
    public class NativeFFTAccelerator : IAccelerator
    {
        /// <inheritdoc />
        public bool Apply(ILogger logger)
        {
            try
            {
                if (NativePocketFFT.Initialize(logger))
                {
                    logger.Log("Accelerating FFT using native code adapter", LogLevel.Std);
                    FFTPlanFactory.SetGlobalFactory(Native_Create1DComplexFFTPlanFloat32);
                    FFTPlanFactory.SetGlobalFactory(Native_Create1DComplexFFTPlanFloat64);
                    FFTPlanFactory.SetGlobalFactory(Native_Create1DRealFFTPlanFloat32);
                    FFTPlanFactory.SetGlobalFactory(Native_Create1DRealFFTPlanFloat64);
                    return true;
                }
            }
            catch (Exception e)
            {
                logger.Log(e);
            }

            return false;
        }

        /// <inheritdoc />
        public void Unapply(ILogger logger)
        {
            FFTPlanFactory.SetGlobalFactory((Func<int, IReal1DFFTPlanFloat32>)null);
            FFTPlanFactory.SetGlobalFactory((Func<int, IReal1DFFTPlanFloat64>)null);
            FFTPlanFactory.SetGlobalFactory((Func<int, IComplex1DFFTPlanFloat32>)null);
            FFTPlanFactory.SetGlobalFactory((Func<int, IComplex1DFFTPlanFloat64>)null);
        }

        private static IComplex1DFFTPlanFloat32 Native_Create1DComplexFFTPlanFloat32(int length)
        {
            return new PlanNativeComplexFloat32(length);
        }

        private static IComplex1DFFTPlanFloat64 Native_Create1DComplexFFTPlanFloat64(int length)
        {
            return new PlanNativeComplexFloat64(length);
        }

        private static IReal1DFFTPlanFloat32 Native_Create1DRealFFTPlanFloat32(int length)
        {
            return new PlanNativeRealFloat32(length);
        }

        private static IReal1DFFTPlanFloat64 Native_Create1DRealFFTPlanFloat64(int length)
        {
            return new PlanNativeRealFloat64(length);
        }
    }
}
