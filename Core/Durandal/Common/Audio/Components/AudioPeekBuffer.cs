using Durandal.Common.Collections;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio graph filter which stores a snapshot of data passing through it without modifying
    /// or buffering anything. Useful if you want to do something like create a live visualization
    /// of the audio, or perform approximate analysis like peak volume or fourier distribution.
    /// </summary>
    public sealed class AudioPeekBuffer : AbstractAudioSampleFilter
    {
        private readonly TimeSpan _peekBufferLength;
        private readonly int _bufferLengthSamplesPerChannel;
        private readonly float[] _buffer;
        private readonly object _lock = new object();
        private readonly IRandom _debugJitter; // used to randomize output when we want to do unit tests
        private long _sampleCounterPerChannel;

        public AudioPeekBuffer(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, TimeSpan bufferLength)
            : base(graph, nameof(AudioPeekBuffer), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;

            if (bufferLength <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferLength));
            }

            _peekBufferLength = bufferLength;
            _bufferLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, bufferLength);
            _buffer = new float[_bufferLengthSamplesPerChannel * format.NumChannels];
            _sampleCounterPerChannel = 0 - _bufferLengthSamplesPerChannel;
            _debugJitter = null;
        }

        public AudioPeekBuffer(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, TimeSpan bufferLength, IRandom randomJitterForDebugging)
            : base(graph, nameof(AudioPeekBuffer), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;

            if (bufferLength <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferLength));
            }

            _peekBufferLength = bufferLength;
            _bufferLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, bufferLength);
            _buffer = new float[_bufferLengthSamplesPerChannel * format.NumChannels];
            _sampleCounterPerChannel = 0 - _bufferLengthSamplesPerChannel;
            _debugJitter = randomJitterForDebugging;
        }

        public TimeSpan PeekBufferLength => _peekBufferLength;

        /// <summary>
        /// Peeks at the buffer data, copying it atomically to a separate caller-provided array.
        /// </summary>
        /// <param name="output">The array to write the peeked data to</param>
        /// <param name="outputOffset">The absolute offset to use when writing to the output buffer</param>
        /// <param name="maxOutputLengthSamplesPerChannel">The maximum number of samples per channel to peek, or in other words, the capacity of the target array</param>
        /// <param name="actualOutputLengthSamplesPerChannel">(out) The actual number of samples per channel peeked from the buffer</param>
        /// <param name="bufferStartTimestampSamplesPerChannel">(out) The "timestamp" of the first sample of the peeked data, in units of samples
        /// per channel since the buffer began reading data.</param>
        public void PeekAtBuffer(float[] output,
            int outputOffset,
            int maxOutputLengthSamplesPerChannel,
            out int actualOutputLengthSamplesPerChannel,
            out long bufferStartTimestampSamplesPerChannel)
        {
            output = output.AssertNonNull(nameof(output));
            if (maxOutputLengthSamplesPerChannel <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxOutputLengthSamplesPerChannel));
            }

            lock (_lock)
            {
                if (_debugJitter == null)
                {
                    bufferStartTimestampSamplesPerChannel = _sampleCounterPerChannel;
                    actualOutputLengthSamplesPerChannel = FastMath.Min(maxOutputLengthSamplesPerChannel, _bufferLengthSamplesPerChannel);
                    ArrayExtensions.MemCopy(
                        _buffer,
                        0,
                        output,
                        outputOffset,
                        actualOutputLengthSamplesPerChannel * OutputFormat.NumChannels);
                }
                else
                {
                    int maxOutputLength = FastMath.Min(maxOutputLengthSamplesPerChannel, _bufferLengthSamplesPerChannel);
                    int startOffset = _debugJitter.NextInt(0, maxOutputLength / 2);
                    int samplesPerChannelToCopy = _debugJitter.NextInt(1, maxOutputLength - startOffset);
                    bufferStartTimestampSamplesPerChannel = _sampleCounterPerChannel + startOffset;
                    actualOutputLengthSamplesPerChannel = samplesPerChannelToCopy;
                    ArrayExtensions.MemCopy(
                        _buffer,
                        startOffset * OutputFormat.NumChannels,
                        output,
                        outputOffset,
                        samplesPerChannelToCopy * OutputFormat.NumChannels);
                }
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int readSize = await Input.ReadAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);

            if (readSize > 0)
            {
                lock (_lock)
                {
                    // Shift buffer left
                    int samplesPerChannelToKeepInBuffer = FastMath.Max(0, _bufferLengthSamplesPerChannel - readSize);
                    if (samplesPerChannelToKeepInBuffer > 0)
                    {
                        ArrayExtensions.MemMove(
                            _buffer,
                            (_bufferLengthSamplesPerChannel - samplesPerChannelToKeepInBuffer) * InputFormat.NumChannels,
                            0,
                            samplesPerChannelToKeepInBuffer * InputFormat.NumChannels);
                    }

                    // Write new data
                    int samplesPerChannelToCopyFromInput = FastMath.Min(readSize, _bufferLengthSamplesPerChannel);
                    ArrayExtensions.MemCopy(
                        buffer,
                        (offset + ((readSize - samplesPerChannelToCopyFromInput) * InputFormat.NumChannels)),
                        _buffer,
                        samplesPerChannelToKeepInBuffer * InputFormat.NumChannels,
                        samplesPerChannelToCopyFromInput * InputFormat.NumChannels);

                    _sampleCounterPerChannel += readSize;
                }
            }

            return readSize;
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (count > 0)
            {
                lock (_lock)
                {
                    // Shift buffer left
                    int samplesPerChannelToKeepInBuffer = FastMath.Max(0, _bufferLengthSamplesPerChannel - count);
                    if (samplesPerChannelToKeepInBuffer > 0)
                    {
                        ArrayExtensions.MemMove(
                            _buffer,
                            (_bufferLengthSamplesPerChannel - samplesPerChannelToKeepInBuffer) * InputFormat.NumChannels,
                            0,
                            samplesPerChannelToKeepInBuffer * InputFormat.NumChannels);
                    }

                    // Write new data
                    int samplesPerChannelToCopyFromInput = FastMath.Min(count, _bufferLengthSamplesPerChannel);
                    ArrayExtensions.MemCopy(
                        buffer,
                        (offset + ((count - samplesPerChannelToCopyFromInput) * InputFormat.NumChannels)),
                        _buffer,
                        samplesPerChannelToKeepInBuffer * InputFormat.NumChannels,
                        samplesPerChannelToCopyFromInput * InputFormat.NumChannels);

                    _sampleCounterPerChannel += count;
                }
            }

            return Output.WriteAsync(buffer, offset, count, cancelToken, realTime);
        }
    }
}
