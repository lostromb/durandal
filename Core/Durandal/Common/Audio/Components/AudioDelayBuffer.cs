using Durandal.Common.Collections;
using Durandal.Common.Events;
using Durandal.Common.IO;
using Durandal.Common.Logger;
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
    /// Audio graph component which introduces a constant delay.
    /// </summary>
    public sealed class AudioDelayBuffer : AbstractAudioSampleFilter, IAudioDelayingFilter
    {
        private readonly int _bufferLengthSamplesPerChannel;
        private readonly float[] _buffer;
        private int _bufferReadCursorPerChannel = 0;
        private int _bufferWriteCursorPerChannel = 0;
        private int _samplesPerChannelInBuffer;

        public AudioDelayBuffer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            TimeSpan bufferLength) : base(graph, nameof(AudioDelayBuffer), nodeCustomName)
        {
            if (bufferLength <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("Buffer length must be a positive timespan");
            }
            if (bufferLength > TimeSpan.FromMinutes(1))
            {
                throw new ArgumentOutOfRangeException("Buffer length of " + bufferLength.PrintTimeSpan() + " is ridiculously large");
            }

            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            _bufferLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, bufferLength);
            _buffer = new float[_bufferLengthSamplesPerChannel * format.NumChannels];
            _samplesPerChannelInBuffer = _bufferLengthSamplesPerChannel;
            AlgorithmicDelay = bufferLength;
        }

        /// <inheritdoc />
        public TimeSpan AlgorithmicDelay { get; private set; }

        protected override async ValueTask<int> ReadAsyncInternal(float[] outputBuffer, int outputBufferOffset, int samplesPerChannelRequested, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (Input.PlaybackFinished)
            {
                return -1;
            }

            int samplesPerChannelWrittenToOutput = 0;

            using (PooledBuffer<float> pooledBuf = BufferPool<float>.Rent(samplesPerChannelRequested * InputFormat.NumChannels))
            {
                float[] inputSamples = pooledBuf.Buffer;

                // Read as much as we can into a temporary scratch space
                int inputDataLengthSamplesPerChannel = await Input.ReadAsync(inputSamples, 0, samplesPerChannelRequested, cancelToken, realTime).ConfigureAwait(false);
                int samplesPerChannelReadFromInput = 0;

                // First we want to fill the delay buffer entirely, and then any excess above that will be written to output
                int targetSamplesPerChannelToWriteToOutput = FastMath.Max(0, inputDataLengthSamplesPerChannel - (_bufferLengthSamplesPerChannel - _samplesPerChannelInBuffer));

                while (samplesPerChannelReadFromInput < inputDataLengthSamplesPerChannel ||
                    samplesPerChannelWrittenToOutput < targetSamplesPerChannelToWriteToOutput)
                {
                    // Read from input to buffer, as much as possible
                    int samplesPerChannelCanWriteIntoBufferTotal = FastMath.Min(inputDataLengthSamplesPerChannel - samplesPerChannelReadFromInput, _bufferLengthSamplesPerChannel - _samplesPerChannelInBuffer);
                    int samplesPerChannelCanWriteIntoBufferInOneSegment = FastMath.Min(_bufferLengthSamplesPerChannel - _bufferWriteCursorPerChannel, samplesPerChannelCanWriteIntoBufferTotal);
                    if (samplesPerChannelCanWriteIntoBufferInOneSegment > 0)
                    {
                        ArrayExtensions.MemCopy(
                            inputSamples,
                            samplesPerChannelReadFromInput * InputFormat.NumChannels,
                            _buffer,
                            _bufferWriteCursorPerChannel * InputFormat.NumChannels,
                            samplesPerChannelCanWriteIntoBufferInOneSegment * InputFormat.NumChannels);
                        _bufferWriteCursorPerChannel = (_bufferWriteCursorPerChannel + samplesPerChannelCanWriteIntoBufferInOneSegment) % _bufferLengthSamplesPerChannel;
                        _samplesPerChannelInBuffer += samplesPerChannelCanWriteIntoBufferInOneSegment;
                        samplesPerChannelReadFromInput += samplesPerChannelCanWriteIntoBufferInOneSegment;
                    }

                    if (_samplesPerChannelInBuffer == _bufferLengthSamplesPerChannel)
                    {
                        // Copy from buffer to output, as much as possible as long as the buffer is full
                        int samplesPerChannelCanReadFromBufferTotal = Math.Min(targetSamplesPerChannelToWriteToOutput - samplesPerChannelWrittenToOutput, _samplesPerChannelInBuffer);
                        int samplesPerChannelCanReadFromBufferInOneSegment = Math.Min(_bufferLengthSamplesPerChannel - _bufferReadCursorPerChannel, samplesPerChannelCanReadFromBufferTotal);
                        if (samplesPerChannelCanReadFromBufferInOneSegment > 0)
                        {
                            ArrayExtensions.MemCopy(
                                _buffer,
                                _bufferReadCursorPerChannel * InputFormat.NumChannels,
                                outputBuffer,
                                (outputBufferOffset + (samplesPerChannelWrittenToOutput * InputFormat.NumChannels)),
                                samplesPerChannelCanReadFromBufferInOneSegment * InputFormat.NumChannels);
                            _bufferReadCursorPerChannel = (_bufferReadCursorPerChannel + samplesPerChannelCanReadFromBufferInOneSegment) % _bufferLengthSamplesPerChannel;
                            _samplesPerChannelInBuffer -= samplesPerChannelCanReadFromBufferInOneSegment;
                            samplesPerChannelWrittenToOutput += samplesPerChannelCanReadFromBufferInOneSegment;
                        }
                    }
                }
            }

            return samplesPerChannelWrittenToOutput;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelReadFromInput = 0;
            int samplesPerChannelWrittenToOutput = 0;

            // First we want to fill the buffer entirely, and then any excess above that will be written to output
            int targetSamplesPerChannelToWriteToOutput = Math.Max(0, count - (_bufferLengthSamplesPerChannel - _samplesPerChannelInBuffer));

            while (samplesPerChannelReadFromInput < count ||
                samplesPerChannelWrittenToOutput < targetSamplesPerChannelToWriteToOutput)
            {
                // Read from input to buffer, as much as possible
                int samplesPerChannelCanWriteIntoBufferTotal = Math.Min(count - samplesPerChannelReadFromInput, _bufferLengthSamplesPerChannel - _samplesPerChannelInBuffer);
                int samplesPerChannelCanWriteIntoBufferInOneSegment = Math.Min(_bufferLengthSamplesPerChannel - _bufferWriteCursorPerChannel, samplesPerChannelCanWriteIntoBufferTotal);
                if (samplesPerChannelCanWriteIntoBufferInOneSegment > 0)
                {
                    ArrayExtensions.MemCopy(
                        buffer,
                        (offset + (samplesPerChannelReadFromInput * InputFormat.NumChannels)),
                        _buffer,
                        _bufferWriteCursorPerChannel * InputFormat.NumChannels,
                        samplesPerChannelCanWriteIntoBufferInOneSegment * InputFormat.NumChannels);
                    _bufferWriteCursorPerChannel = (_bufferWriteCursorPerChannel + samplesPerChannelCanWriteIntoBufferInOneSegment) % _bufferLengthSamplesPerChannel;
                    _samplesPerChannelInBuffer += samplesPerChannelCanWriteIntoBufferInOneSegment;
                    samplesPerChannelReadFromInput += samplesPerChannelCanWriteIntoBufferInOneSegment;
                }

                if (_samplesPerChannelInBuffer == _bufferLengthSamplesPerChannel)
                {
                    // Copy from buffer to output, as much as possible as long as the buffer is full
                    int samplesPerChannelCanReadFromBufferTotal = Math.Min(targetSamplesPerChannelToWriteToOutput - samplesPerChannelWrittenToOutput, _samplesPerChannelInBuffer);
                    int samplesPerChannelCanReadFromBufferInOneSegment = Math.Min(_bufferLengthSamplesPerChannel - _bufferReadCursorPerChannel, samplesPerChannelCanReadFromBufferTotal);
                    if (samplesPerChannelCanReadFromBufferInOneSegment > 0)
                    {
                        await Output.WriteAsync(
                            _buffer,
                            _bufferReadCursorPerChannel * InputFormat.NumChannels,
                            samplesPerChannelCanReadFromBufferInOneSegment,
                            cancelToken,
                            realTime).ConfigureAwait(false);
                        _bufferReadCursorPerChannel = (_bufferReadCursorPerChannel + samplesPerChannelCanReadFromBufferInOneSegment) % _bufferLengthSamplesPerChannel;
                        _samplesPerChannelInBuffer -= samplesPerChannelCanReadFromBufferInOneSegment;
                        samplesPerChannelWrittenToOutput += samplesPerChannelCanReadFromBufferInOneSegment;
                    }
                }
            }
        }
    }
}
