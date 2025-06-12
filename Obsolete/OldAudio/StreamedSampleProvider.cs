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
    public class StreamedSampleProvider : ISampleProvider
    {
        private ChunkedAudioStream _stream;
        private AudioChunk _nextChunk;
        private int _inCursor = 0;
        private int _outputSampleRate;
        private bool _streamFinished = false;
        private ILogger _logger;
        private object _channelToken;

        public StreamedSampleProvider(ChunkedAudioStream stream, int outputSampleRate, ILogger logger, object channelToken = null)
        {
            _stream = stream;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(outputSampleRate, 1);
            _outputSampleRate = outputSampleRate;
            _channelToken = channelToken;
            _logger = logger;
        }

        public bool Finished
        {
            get
            {
                return _streamFinished;
            }
        }

        public WaveFormat WaveFormat
        {
            get;
            private set;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (_streamFinished)
            {
                _logger.Log("Attempted to read from finished stream", LogLevel.Wrn);
                return 0;
            }

            if (_nextChunk == null)
            {
                if (_stream.EndOfStream)
                {
                    if (!_streamFinished)
                    {
                        _streamFinished = true;
                        OnStreamFinished();
                    }

                    return 0;
                }

                AudioChunk next = _stream.Read(10);
                if (next != null)
                {
                    _nextChunk = next/*.ResampleTo(_outputSampleRate)*/;
                }
                else
                {
                    // _logger.Log("Buffer underrun of " + count + " samples", LogLevel.Wrn);
                    // Serious buffer underrun. In this case, just return silence instead of stuttering
                    for (int c = 0; c < count; c++)
                    {
                        buffer[c + offset] = 0.0f;
                    }

                    return count;
                }
            }

            int samplesWritten = 0;
            short[] returnVal = new short[count];

            while (samplesWritten < count && _nextChunk != null)
            {
                int remainingInThisChunk = _nextChunk.DataLength - _inCursor;
                int remainingToWrite = (count - samplesWritten);
                int chunkSize = Math.Min(remainingInThisChunk, remainingToWrite);
                Array.Copy(_nextChunk.Data, _inCursor, returnVal, samplesWritten, chunkSize);
                _inCursor += chunkSize;
                samplesWritten += chunkSize;

                if (_inCursor >= _nextChunk.DataLength)
                {
                    _inCursor = 0;

                    if (_stream.EndOfStream)
                    {
                        _nextChunk = null;
                    }
                    else
                    {
                        AudioChunk next = _stream.Read(10);
                        if (next != null)
                        {
                            _nextChunk = next/*.ResampleTo(_outputSampleRate)*/;
                        }
                    }
                }
            }

            for (int c = 0; c < samplesWritten; c++)
            {
                buffer[c + offset] = ((float)returnVal[c]) / ((float)short.MaxValue);
            }

            if (_nextChunk == null && !_streamFinished)
            {
                _streamFinished = true;
                OnStreamFinished();
            }
            
            return samplesWritten;
        }

        public AsyncEvent<ChannelFinishedEventArgs> StreamFinishedEvent { get; }

        private void OnStreamFinished()
        {
            StreamFinishedEvent.FireInBackground(
                this,
                new ChannelFinishedEventArgs(_channelToken, true, DefaultRealTimeProvider.Singleton),
                NullLogger.Singleton,
                DefaultRealTimeProvider.Singleton);
        }
    }
}
