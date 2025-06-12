namespace Durandal.Extensions.NativeAudio.FFT
{
    using Durandal.Common.MathExt;
    using Durandal.Common.MathExt.FFT;
    using System;
    using System.Buffers;
    using System.Runtime.InteropServices;

    public class PlanNativeComplexFloat32 : IComplex1DFFTPlanFloat32
    {
        private int _length;
        private NativePocketFFT.NativeCFFTPlanHandle _handle;

        public PlanNativeComplexFloat32(int length)
        {
            _length = length;
            _handle = NativePocketFFT.make_cfft_plan(length);
        }

        public int Length => _length;

        public unsafe void Forward(Span<ComplexF> c, float fct)
        {
            // literally just faster to convert float32 -> float64 and run it through the native code
            Complex[] scratch = ArrayPool<Complex>.Shared.Rent(c.Length);
            Span<float> singleSpan = MemoryMarshal.Cast<ComplexF, float>(c);
            Span<double> doubleSpan = MemoryMarshal.Cast<Complex, double>(scratch.AsSpan(0, c.Length));
            FFTIntrinsics.CastSingleToDouble(singleSpan, doubleSpan);

            fixed (Complex* ptr = scratch)
            {
                NativePocketFFT.cfft_forward(_handle, ptr, fct);
            }

            FFTIntrinsics.CastDoubleToSingle(doubleSpan, singleSpan);
            //for (int i = 0; i < c.Length; i++)
            //{
            //    c[i] = new ComplexF((float)scratch[i].r, (float)scratch[i].i);
            //}

            ArrayPool<Complex>.Shared.Return(scratch);
        }

        public unsafe void Backward(Span<ComplexF> c, float fct)
        {
            Complex[] scratch = ArrayPool<Complex>.Shared.Rent(c.Length);
            Span<float> singleSpan = MemoryMarshal.Cast<ComplexF, float>(c);
            Span<double> doubleSpan = MemoryMarshal.Cast<Complex, double>(scratch.AsSpan(0, c.Length));
            FFTIntrinsics.CastSingleToDouble(singleSpan, doubleSpan);

            fixed (Complex* ptr = scratch)
            {
                NativePocketFFT.cfft_backward(_handle, ptr, fct);
            }

            FFTIntrinsics.CastDoubleToSingle(doubleSpan, singleSpan);
            //for (int i = 0; i < c.Length; i++)
            //{
            //    c[i] = new ComplexF((float)scratch[i].r, (float)scratch[i].i);
            //}

            ArrayPool<Complex>.Shared.Return(scratch);
        }

        public void Dispose()
        {
            _handle.Dispose();
        }
    }
}