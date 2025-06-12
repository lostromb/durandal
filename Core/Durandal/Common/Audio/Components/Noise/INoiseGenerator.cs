using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Components.Noise
{
    /// <summary>
    /// Defines a (potentially stateful) class which can generate noise audio.
    /// </summary>
    public  interface INoiseGenerator
    {
        /// <summary>
        /// Fills the target buffer with noise.
        /// </summary>
        /// <param name="target">The buffer to write to (potentially interleaved channels)</param>
        /// <param name="offset">The absolute offset to use when writing to buffer</param>
        /// <param name="samplesPerChannelToGenerate">The number of samples per channel of noise to generate</param>
        void GenerateNoise(float[] target, int offset, int samplesPerChannelToGenerate);
    }
}
