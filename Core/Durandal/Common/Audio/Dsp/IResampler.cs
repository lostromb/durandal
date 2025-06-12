using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Represents an audio signal resampler which operates on interleaved floating-point data.
    /// </summary>
    public interface IResampler : IDisposable
    {
        /// <summary>
        /// Resamples an interleaved float array. The stride is automatically determined by the number of channels of the resampler.
        /// </summary>
        /// <param name="input">Input buffer, represented as 32-bit floats ranging from -1.0 to 1.0, interleaved to the number of channels</param>
        /// <param name="input_ptr">Offset to start from when reading input, in total samples</param>
        /// <param name="in_len">The number of samples *PER-CHANNEL* in the input buffer. After this function returns, this
        /// value will be set to the number of input samples actually processed</param>
        /// <param name="output">Output buffer</param>
        /// <param name="output_ptr">Offset to start from when writing output</param>
        /// <param name="out_len">The size of the output buffer in samples-per-channel. After this function returns, this value
        /// will be set to the number of samples per channel actually produced</param>
        void ProcessInterleaved(float[] input, int input_ptr, ref int in_len, float[] output, int output_ptr, ref int out_len);

        /// <summary>
        /// Gets the algorithmic delay introduced by the resampler in its current configuration.
        /// </summary>
        TimeSpan OutputLatency { get; }
    }
}
