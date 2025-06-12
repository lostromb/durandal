using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.NAudio
{
    public class AudioChunkWaveProvider : IWaveProvider
    {
        private int _cursor;
        private AudioChunk _sourceChunk;
        private bool _sampleFinished = false;
        private object _channelToken;

        public AudioChunkWaveProvider(AudioChunk chunk, object channelToken = null)
        {
            WaveFormat = new WaveFormat(chunk.SampleRate, 1);
            _sourceChunk = chunk;
            _cursor = 0;
            _channelToken = channelToken;
            SampleFinishedEvent = new AsyncEvent<ChannelFinishedEventArgs>();
        }

        public bool Finished
        {
            get
            {
                return _sampleFinished;
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int remaining = _sourceChunk.DataLength - _cursor;

            if (remaining == 0)
            {
                if (!_sampleFinished)
                {
                    OnSampleFinished();
                    _sampleFinished = true;
                }

                return 0;
            }

            int dataSize = Math.Min(remaining, count / 2);
            AudioMath.ShortsToBytes(_sourceChunk.Data, _cursor, buffer, offset, dataSize);
            _cursor += dataSize;
            return dataSize * 2;
        }

        public WaveFormat WaveFormat
        {
            get;
            private set;
        }

        public AsyncEvent<ChannelFinishedEventArgs> SampleFinishedEvent { get; private set; }

        private void OnSampleFinished()
        {
            ChannelFinishedEventArgs args = new ChannelFinishedEventArgs(_channelToken, false, DefaultRealTimeProvider.Singleton);
            SampleFinishedEvent.FireInBackground(this, args, NullLogger.Singleton, DefaultRealTimeProvider.Singleton);
        }
    }
}
