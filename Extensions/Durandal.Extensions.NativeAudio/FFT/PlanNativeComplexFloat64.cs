namespace Durandal.Extensions.NativeAudio.FFT
{
    using Durandal.Common.MathExt;
    using Durandal.Common.MathExt.FFT;
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    public class PlanNativeComplexFloat64 : IComplex1DFFTPlanFloat64
    {
        private int _length;
        private NativePocketFFT.NativeCFFTPlanHandle _handle;

        public PlanNativeComplexFloat64(int length)
        {
            _length = length;
            _handle = NativePocketFFT.make_cfft_plan(length);
        }

        public int Length => _length;

        public unsafe void Backward(Span<Complex> c, double fct)
        {
            fixed (Complex* ptr = c)
            {
                NativePocketFFT.cfft_backward(_handle, ptr, fct);
            }
        }

        public void Dispose()
        {
            _handle.Dispose();
        }

        public unsafe void Forward(Span<Complex> c, double fct)
        {
            fixed (Complex* ptr = c)
            {
                NativePocketFFT.cfft_forward(_handle, ptr, fct);
            }
        }
    }
}