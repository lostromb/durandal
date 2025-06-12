using System;
using Durandal.Common.Audio;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using System.Diagnostics;
using Durandal.Common.Audio.Codecs.Opus.Common;
using Durandal.Common.Audio.Sampling;
using Durandal.Common.Time;
using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Utils;

namespace Durandal.Common.Audio.Mixer
{
    /// <summary>
    /// Represents a very rudimentary audio mixer that can accept sampled or streamed inputs.
    /// </summary>
    public class BasicAudioMixer : IAudioPlayer, IAudioSampleProvider, IDisposable
    {
        private readonly ReaderWriterLockSlim _mutex = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private readonly ILogger _logger;
        private IList<IMixerInput> _inputs = new List<IMixerInput>();
        private int _disposed = 0;
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="outputSampleRate">The sample rate to use for the channel</param>
        /// <param name="logger">A logger</param>
        public BasicAudioMixer(ILogger logger)
        {
            _logger = logger;
            ChannelFinishedEvent = new AsyncEvent<ChannelFinishedEventArgs>();
        }

        ~BasicAudioMixer()
        {
            Dispose(false);
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
                // Free all samples
                _mutex.EnterWriteLock();
                try
                {
                    foreach (IMixerInput input in _inputs)
                    {
                        input.Dispose();
                    }

                    _inputs.Clear();
                }
                finally
                {
                    _mutex.ExitWriteLock();
                }

                _mutex.Dispose();
            }
        }

        public void PlaySound(AudioChunk chunk, object channelToken = null)
        {
            if (_disposed == 1)
            {
                throw new ObjectDisposedException("BasicAudioMixer");
            }

            _mutex.EnterWriteLock();
            try
            {
                PruneInputs();
#pragma warning disable CA2000 // Dispose objects before losing scope
                SampleMixerInput input = new SampleMixerInput(chunk, channelToken);
                input.PlaybackFinishedEvent.Subscribe(OnSubChannelFinished);
                _inputs.Add(input);
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            finally
            {
                _mutex.ExitWriteLock();
            }
        }

        public void PlayStream(ChunkedAudioStream stream, object channelToken = null)
        {
            if (_disposed == 1)
            {
                throw new ObjectDisposedException("BasicAudioMixer");
            }

            _mutex.EnterWriteLock();
            try
            {
                PruneInputs();
#pragma warning disable CA2000 // Dispose objects before losing scope
                StreamMixerInput input = new StreamMixerInput(stream, channelToken);
                input.PlaybackFinishedEvent.Subscribe(OnSubChannelFinished);
                _inputs.Add(input);
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            finally
            {
                _mutex.ExitWriteLock();
            }
        }

        public Task<int> ReadSamples(float[] target, int offset, int count, IRealTimeProvider realTime)
        {
            if (_disposed == 1)
            {
                throw new ObjectDisposedException("BasicAudioMixer");
            }

            if (count == 0)
            {
                return Task.FromResult(0);
            }

            short[] mixBuffer = new short[count];

            // Clear the output
            for (int c = offset; c < offset + count; c++)
            {
                target[c] = 0;
            }

            _mutex.EnterReadLock();
            try
            {
                foreach (IMixerInput input in _inputs)
                {
                    if (!input.Finished)
                    {
                        int readFromSource = input.Read(mixBuffer, 0, count, realTime);
                        for (int c = 0; c < readFromSource; c++)
                        {
                            // Straight mix of all inputs into a float buffer
                            // TODO apply a dynamic compressor to avoid clipping / peaking the output
                            target[c + offset] += ((float)mixBuffer[c] / 32767);
                        }
                    }
                }
            }
            finally
            {
                _mutex.ExitReadLock();
            }

            return Task.FromResult(count);
        }

        /// <summary>
        /// Indicates whether any sources are active in this mixer
        /// </summary>
        public bool IsPlaying()
        {
            _mutex.EnterReadLock();
            try
            {
                foreach (IMixerInput input in _inputs)
                {
                    if (!input.Finished)
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                _mutex.ExitReadLock();
            }
        }

        private void PruneInputs()
        {
            IList<IMixerInput> aliveInputs = new List<IMixerInput>();
            foreach (IMixerInput input in _inputs)
            {
                if (input.Finished)
                {
                    input.PlaybackFinishedEvent.Unsubscribe(OnSubChannelFinished);
                    input.Dispose();
                }
                else
                {
                    aliveInputs.Add(input);
                }
            }

            _inputs = aliveInputs;
        }
        
        public AsyncEvent<ChannelFinishedEventArgs> ChannelFinishedEvent { get; private set; }

        private Task OnSubChannelFinished(object source, ChannelFinishedEventArgs args, IRealTimeProvider realTime)
        {
            return ChannelFinishedEvent.Fire(source, args, realTime);
        }

        public void StopPlaying()
        {
            if (_disposed == 1)
            {
                throw new ObjectDisposedException("BasicAudioMixer");
            }

            _mutex.EnterWriteLock();
            try
            {
                foreach (IMixerInput input in _inputs)
                {
                    input.PlaybackFinishedEvent.Unsubscribe(OnSubChannelFinished);
                    input.Dispose();
                }

                _inputs.Clear();
            }
            finally
            {
                _mutex.ExitWriteLock();
            }
        }
    }
}

