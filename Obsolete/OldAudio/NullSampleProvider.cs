using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Sample provider which always returns silence
    /// </summary>
    public class NullSampleProvider : IAudioSampleProvider
    {
        public static readonly NullSampleProvider Singleton = new NullSampleProvider();

        private NullSampleProvider() { }

        public Task<int> ReadSamples(float[] target, int offset, int count, IRealTimeProvider realTime)
        {
            for (int c = 0; c < count; c++)
            {
                target[c + offset] = 0;
            }

            return Task.FromResult(count);
        }
    }
}
