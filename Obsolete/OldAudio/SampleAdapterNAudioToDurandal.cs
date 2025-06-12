using Durandal.Common.AudioV2;
using Durandal.Common.Time;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.NAudio
{
    public class SampleAdapterNAudioToDurandal : IAudioSampleProvider
    {
        private readonly ISampleProvider _nAudioSampleProvider;

        public SampleAdapterNAudioToDurandal(ISampleProvider nAudioSampleProvider)
        {
            _nAudioSampleProvider = nAudioSampleProvider;
        }

        public Task<int> ReadSamples(float[] buffer, int offset, int count, IRealTimeProvider realTime)
        {
            return Task.FromResult(_nAudioSampleProvider.Read(buffer, offset, count));
        }
    }
}
