using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NAudio.Wave;

namespace Durandal.Extensions.NAudio
{
    using Durandal.Common.Audio.Codecs.Opus.Common;
    using Durandal.Common.AudioV2;
    using Durandal.Common.Audio.Sampling;
    using Durandal.Common.Time;
    using Durandal.Common.Logger;
    using global::NAudio.Wave.SampleProviders;
    using Durandal.Common.Audio;
    using System.Threading.Tasks;
    using Common.Utils;

    // based on http://mark-dot-net.blogspot.co.uk/2014/02/fire-and-forget-audio-playback-with.html
    // todo: use this pattern instead https://gist.github.com/markheath/8783999

    public class NAudioMixer : IAudioPlayer, IAudioSampleProvider
    {
        private readonly MixingSampleProvider _mixer;
        private readonly object _mixerMutex = new object();
        private IList<StreamedSampleProvider> _activeStreams = new List<StreamedSampleProvider>();
        private IList<AudioChunkSampleProvider> _activeSamples = new List<AudioChunkSampleProvider>();
        private ILogger _logger;
        private int _disposed = 0;

        public NAudioMixer(ILogger logger)
        {
            _logger = logger;
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE, 1));
            _mixer.ReadFully = true;
            ChannelFinishedEvent = new AsyncEvent<ChannelFinishedEventArgs>();
        }

        ~NAudioMixer()
        {
            Dispose(false);
        }

        public void PlaySound(AudioChunk sound, object channelToken = null)
        {
            PruneMixer();
            AudioChunkSampleProvider newProvider = sound.CreateSampleProvider(channelToken);
            newProvider.SampleFinishedEvent.Subscribe(OnSubChannelFinished);

            lock (_mixerMutex)
            {
                _activeSamples.Add(newProvider);
                _mixer.AddMixerInput(newProvider);
            }
        }

        //public void PlayWaveFile(string fileName)
        //{
        //    // todo: we should really really use a stream here
        //    AudioChunk sound = AudioChunkFactory.CreateFromFile(fileName);
        //    PlaySound(sound);
        //}

        public void PlayStream(ChunkedAudioStream stream, object channelToken = null)
        {
            PruneMixer();
            StreamedSampleProvider newStream = new StreamedSampleProvider(stream, AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE, _logger.Clone("NAudioStream"), channelToken);
            newStream.StreamFinishedEvent.Subscribe(OnSubChannelFinished);

            lock (_mixerMutex)
            {
                _activeStreams.Add(newStream);
                _mixer.AddMixerInput(newStream);
            }
        }

        private void PruneMixer()
        {
            lock (_mixerMutex)
            {
                IList<StreamedSampleProvider> updatedStreams = new List<StreamedSampleProvider>();
                foreach (StreamedSampleProvider provider in _activeStreams)
                {
                    if (provider.Finished)
                    {
                        _mixer.RemoveMixerInput(provider);
                        provider.StreamFinishedEvent.Unsubscribe(OnSubChannelFinished);
                    }
                    else
                    {
                        updatedStreams.Add(provider);
                    }
                }

                _activeStreams = updatedStreams;

                IList<AudioChunkSampleProvider> updatedSamplers = new List<AudioChunkSampleProvider>();
                foreach (AudioChunkSampleProvider provider in _activeSamples)
                {
                    if (provider.Finished)
                    {
                        _mixer.RemoveMixerInput(provider);
                        provider.SampleFinishedEvent.Unsubscribe(OnSubChannelFinished);
                    }
                    else
                    {
                        updatedSamplers.Add(provider);
                    }
                }

                _activeSamples = updatedSamplers;
            }
        }

        public bool IsPlaying()
        {
            lock (_mixerMutex)
            {
                return _activeStreams.Any() || _activeSamples.Any();
            }
        }

        public void StopPlaying()
        {
            lock (_mixerMutex)
            {
                _mixer.RemoveAllMixerInputs();

                // TODO: Do we fire channelfinished events on any of these?
                _activeSamples.Clear();
                _activeStreams.Clear();
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
                //_outputDevice?.Dispose();
            }
        }

        public AsyncEvent<ChannelFinishedEventArgs> ChannelFinishedEvent { get; private set; }

        private Task OnSubChannelFinished(object source, ChannelFinishedEventArgs args, IRealTimeProvider realTime)
        {
            return ChannelFinishedEvent.Fire(source, args, realTime);
        }

        public Task<int> ReadSamples(float[] target, int offset, int count, IRealTimeProvider realTime)
        {
            return Task.FromResult(_mixer.Read(target, offset, count));
        }
    }
}
