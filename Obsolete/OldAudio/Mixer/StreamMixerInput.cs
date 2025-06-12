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
    public class StreamMixerInput : IMixerInput
    {
        private ChunkedAudioStream _stream;
        private AudioChunk _currentChunk = null;
        private int _cursor = 0;
        private object _channelToken;
        private int _disposed = 0;

        public StreamMixerInput(ChunkedAudioStream stream, object channelToken)
        {
            PlaybackFinishedEvent = new AsyncEvent<ChannelFinishedEventArgs>();
            _stream = stream;
            _channelToken = channelToken;
            _cursor = 0;
            _currentChunk = _stream.Read();
        }

        ~StreamMixerInput()
        {
            Dispose(false);
        }

        public bool Finished
        {
            get
            {
                return _stream.EndOfStream && _currentChunk == null;
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
            int samplesRead = 0;

            // Attempt to re-prime a struttering stream
            if (_currentChunk == null && !_stream.EndOfStream)
            {
                _currentChunk = _stream.Read(1);
            }

            while (_currentChunk != null && samplesRead < count && !_stream.EndOfStream)
            {
                // Read from the current chunk if possible
                if (_cursor < _currentChunk.DataLength)
                {
                    int samplesRemaining = _currentChunk.DataLength - _cursor;
                    int samplesToWrite = Math.Min(count, samplesRemaining);
                    Array.Copy(_currentChunk.Data, _cursor, target, offset, samplesToWrite);
                    _cursor += samplesToWrite;
                    samplesRead += samplesToWrite;
                }

                if (_cursor == _currentChunk.DataLength && !_stream.EndOfStream)
                {
                    // Queue up a new chunk if needed
                    // This can return NULL in which case we are forced to return zeroes for this frame, and we'll have to "re-prime" the chunk later
                    _currentChunk = _stream.Read(1);
                    _cursor = 0;
                    
                    if (_stream.EndOfStream)
                    {
                        // Did we just reach the end of the stream? Raise an event to indicate
                        OnPlaybackFinished(realTime);
                    }
                }
            }

            if (samplesRead < count)
            {
                 // Pad the rest with zeroes if the stream is over or has no data
                 for (int c = samplesRead; c < count; c++)
                {
                    target[offset + c] = 0;
                }
            }

            return count;
        }

        private void OnPlaybackFinished(IRealTimeProvider realTime)
        {
            PlaybackFinishedEvent.FireInBackground(this, new ChannelFinishedEventArgs(_channelToken, true, realTime), NullLogger.Singleton, realTime);
        }

        public AsyncEvent<ChannelFinishedEventArgs> PlaybackFinishedEvent { get; private set; }
    }
}

