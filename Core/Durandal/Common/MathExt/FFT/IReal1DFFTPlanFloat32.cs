using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Common.MathExt.FFT
{
    /// <summary>
    /// Represents a precomputed state engine for running 1-dimensional float32 real to real Fourier transforms.
    /// </summary>
    public interface IReal1DFFTPlanFloat32 : I1DFFTPlan
    {
        /// <summary>
        /// Performs a forward Fourier transform on a real input array.
        /// The input is a set of real values. The output is the lower Nyquist half of the magnitudes of the transformed output, from low frequencies to high
        /// </summary>
        /// <param name="c">The array of real numbers to be transformed in-place.</param>
        /// <param name="fct">A scaling parameter to scale the results of the transform.</param>
        void Forward(Span<float> c, float fct);

        /// <summary>
        /// Performs a reverse Fourier transform on a real input array.
        /// The input is a set of values in Fourier space. The output is the reconstructed signal.
        /// </summary>
        /// <param name="c">The array of real numbers to be transformed in-place.</param>
        /// <param name="fct">A scaling parameter to scale the results of the transform.</param>
        void Backward(Span<float> c, float fct);
    }
}
