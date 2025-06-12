using Durandal.Common.Audio.Codecs.Opus.Common;
using Durandal.Common.Audio.Mixer;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.NAudio
{
    public class MixerSampleProvider : ISampleProvider
    {
        private BasicAudioMixer _source;
        private WaveFormat _format;

        public MixerSampleProvider(ILogger logger, int sampleRate)
        {
            _source = new BasicAudioMixer(logger);
            _format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        }

        public WaveFormat WaveFormat => _format;
        public BasicAudioMixer Mixer => _source;

        public int Read(float[] buffer, int offset, int count)
        {
            return _source.ReadSamples(buffer, offset, count, DefaultRealTimeProvider.Singleton).Await();
        }
    }
}
