using Durandal.Common.Collections;
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
    /// The purpose of this audio graph filter is to normalize the block size of every read/write that passes through it,
    /// without modifying the data. This is achieved using a small buffer, so latency increase is possible.
    /// This can be used to tweak processes that depend on monotonous audio samples, for example, you want to make sure
    /// that writes to a buffer happen in exactly 10ms increments for low latency.
    /// Reads/writes smaller than the configured block size are buffered and do not cross the boundary at that size.
    /// This behavior is slightly different from <see cref="AudioBlockRectifier"/> which will allow blocks below the max size.
    /// </summary>
    public sealed class AudioBlockRectifierBuffer : AbstractAudioSampleFilter, IAudioDelayingFilter
    {
        private readonly int _bufferLengthSamplesPerChannel;
        private readonly float[] _buffer;
        private int _samplesPerChannelInBuffer = 0;

        public AudioBlockRectifierBuffer(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, TimeSpan blockLength) : base(graph, nameof(AudioBlockRectifierBuffer), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;

            if (blockLength <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("BlockLength must be a positive time span");
            }

            _bufferLengthSamplesPerChannel = Math.Max(1, (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, blockLength));
            _buffer = new float[_bufferLengthSamplesPerChannel * format.NumChannels];
        }

        public TimeSpan AlgorithmicDelay => AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, _samplesPerChannelInBuffer);

        protected override async ValueTask FlushAsyncInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_samplesPerChannelInBuffer > 0)
            {
                await Output.WriteAsync(_buffer, 0, _samplesPerChannelInBuffer, cancelToken, realTime).ConfigureAwait(false);
                _samplesPerChannelInBuffer = 0;
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Attempt to fill the buffer
            int samplesPerChannelCanReadFromInput = Math.Min(_bufferLengthSamplesPerChannel - _samplesPerChannelInBuffer, count);
            if (samplesPerChannelCanReadFromInput > 0)
            {
                int samplesPerChannelReadFromInput = await Input.ReadAsync(
                    _buffer,
                    _samplesPerChannelInBuffer * InputFormat.NumChannels,
                    samplesPerChannelCanReadFromInput,
                    cancelToken,
                    realTime).ConfigureAwait(false);
                if (samplesPerChannelReadFromInput < 0)
                {
                    if (_samplesPerChannelInBuffer == 0)
                    {
                        // End of stream and nothing in buffer
                        return -1;
                    }
                }
                else
                {
                    _samplesPerChannelInBuffer += samplesPerChannelReadFromInput;
                }
            }

            // If we can satisfy either a read of the entire buffer, or the entire count (if it is smaller than the block size), then return a full block.
            int outputBlockSizePerChannel = Math.Min(count, _bufferLengthSamplesPerChannel);
            if (_samplesPerChannelInBuffer >= outputBlockSizePerChannel)
            {
                ArrayExtensions.MemCopy(
                    _buffer,
                    0,
                    buffer,
                    offset,
                    outputBlockSizePerChannel * InputFormat.NumChannels);

                _samplesPerChannelInBuffer -= outputBlockSizePerChannel;
                if (_samplesPerChannelInBuffer > 0)
                {
                    // If there's left over in the buffer, we need to move it left
                    ArrayExtensions.MemMove(_buffer, outputBlockSizePerChannel * InputFormat.NumChannels, 0, _samplesPerChannelInBuffer * InputFormat.NumChannels);
                }

                return outputBlockSizePerChannel;
            }
            else
            {
                return 0;
            }
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelReadFromInput = 0;
            while (samplesPerChannelReadFromInput < count)
            {
                // Fill the buffer as much as possible
                int samplesPerChannelCanReadFromInput = Math.Min(_bufferLengthSamplesPerChannel - _samplesPerChannelInBuffer, count - samplesPerChannelReadFromInput);
                if (samplesPerChannelCanReadFromInput > 0)
                {
                    ArrayExtensions.MemCopy(
                        buffer,
                        (offset + (samplesPerChannelReadFromInput * InputFormat.NumChannels)),
                        _buffer,
                        _samplesPerChannelInBuffer * InputFormat.NumChannels,
                        samplesPerChannelCanReadFromInput * InputFormat.NumChannels);
                    samplesPerChannelReadFromInput += samplesPerChannelCanReadFromInput;
                    _samplesPerChannelInBuffer += samplesPerChannelCanReadFromInput;
                }

                // Then write out from buffer if possible
                if (_samplesPerChannelInBuffer == _bufferLengthSamplesPerChannel)
                {
                    await Output.WriteAsync(_buffer, 0, _bufferLengthSamplesPerChannel, cancelToken, realTime).ConfigureAwait(false);
                    _samplesPerChannelInBuffer = 0;
                }
            }
        }
    }
}
