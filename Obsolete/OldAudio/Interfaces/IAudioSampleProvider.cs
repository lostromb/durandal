using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Interfaces
{
    /// <summary>
    /// Defines an interface that provides audio samples to a hardware device or pipeline object (a mixer, etc.)
    /// </summary>
    public interface IAudioSampleProvider
    {
        /// <summary>
        /// Reads samples from the audio source.
        /// </summary>
        /// <param name="target">The buffer to write samples to.</param>
        /// <param name="offset">The offset to use when writing to buffer</param>
        /// <param name="count">The number of samples requested</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The actual number of samples that were read.</returns>
        Task<int> ReadSamples(float[] target, int offset, int count, IRealTimeProvider realTime);
    }
}
