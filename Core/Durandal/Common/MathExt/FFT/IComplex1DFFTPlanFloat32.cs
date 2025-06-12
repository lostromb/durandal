using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.MathExt.FFT
{
    /// <summary>
    /// Represents a precomputed state engine for running 1-dimensional float32 complex to complex Fourier transforms.
    /// </summary>
    public interface IComplex1DFFTPlanFloat32 : I1DFFTPlan
    {
        /// <summary>
        /// Performs a forward Fourier transform on a complex input array.
        /// </summary>
        /// <param name="c">The array of complex numbers to be transformed in-place.</param>
        /// <param name="fct">A scaling parameter to scale the results of the transform.</param>
        void Forward(Span<ComplexF> c, float fct);

        /// <summary>
        /// Performs a reverse Fourier transform on a complex input array.
        /// </summary>
        /// <param name="c">The array of complex numbers to be transformed in-place.</param>
        /// <param name="fct">A scaling parameter to scale the results of the transform.</param>
        void Backward(Span<ComplexF> c, float fct);
    }
}
