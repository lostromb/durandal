using System;
using Durandal.Common.Audio;
using System.Threading;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Logger;

namespace Durandal.Common.Audio.Mixer
{
    public class SampleMixerInput : IMixerInput
    {
        private AudioChunk _sample;
        private int _cursor = 0;
        private object _channelToken;
        private int _disposed = 0;

        public SampleMixerInput(AudioChunk sample, object channelToken)
        {
            PlaybackFinishedEvent = new AsyncEvent<ChannelFinishedEventArgs>();
            _sample = sample;
            _channelToken = channelToken;
        }

        ~SampleMixerInput()
        {
            Dispose(false);
        }

        public bool Finished
        {
            get
            {
                return _cursor == _sample.DataLength;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            if (!disposing) Durandal.Common.Utils.DebugMemoryLeaktracer.TraceDisposableItemFinalized(this.GetType());

            if (disposing)
            {
            }
        }

        public int Read(short[] target, int offset, int count, IRealTimeProvider realTime)
        {
            // Has the sample finished? Return zeroes
            if (Finished)
            {
                for (int c = 0; c < count; c++)
                {
                    target[offset + c] = 0;
                }

                return count;
            }

            int samplesRemaining = _sample.DataLength - _cursor;
            int samplesToWrite = Math.Min(count, samplesRemaining);
            Array.Copy(_sample.Data, _cursor, target, offset, samplesToWrite);
            _cursor += samplesToWrite;

            // Fill the rest with zeroes if needed
            if (samplesToWrite > samplesRemaining)
            {
                for (int c = samplesRemaining; c < samplesToWrite; c++)
                {
                    target[offset + c] = 0;
                }
            }

            // Did we just reach the end of the stream? Raise an event to indicate
            if (_cursor == _sample.DataLength)
            {
                OnPlaybackFinished(realTime);
            }

            return count;
        }
        
        private void OnPlaybackFinished(IRealTimeProvider realTime)
        {
            PlaybackFinishedEvent.FireInBackground(this, new ChannelFinishedEventArgs(_channelToken, false, realTime), NullLogger.Singleton, realTime);
        }

        public AsyncEvent<ChannelFinishedEventArgs> PlaybackFinishedEvent { get; private set; }
    }
}

