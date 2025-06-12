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
    /// Implements a buffer which attempts to maintain a certain amount of audio in storage at any given time.
    /// This can be used to smooth out reads of audio from unreliable sources, such as downloading a stream from the network, etc.
    /// However, it also introduces a delay equal to the buffer length for real-time sources.
    /// </summary>
    public sealed class AudioSampleBuffer : AbstractAudioSampleFilter
    {
        private readonly int _bufferLengthSamplesPerChannel;

        // This will be pooled if the buffer is small enough, or newly allocated, depending on how the pool decides
        private readonly PooledBuffer<float> _buffer;
        private bool _waitForFullBuffer;
        private int _bufferReadCursorPerChannel = 0;
        private int _bufferWriteCursorPerChannel = 0;
        private int _samplesPerChannelInBuffer = 0;
        private int _disposed;

        public AudioSampleBuffer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            TimeSpan bufferLength,
            bool fullyPrebufferBeforehand = false) : base(graph, nameof(AudioSampleBuffer), nodeCustomName)
        {
            if (bufferLength <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("Buffer length must be a positive timespan");
            }
            if (bufferLength > TimeSpan.FromMinutes(1))
            {
                throw new ArgumentOutOfRangeException("Buffer length of " + bufferLength.PrintTimeSpan() + " is ridiculously large");
            }

            _waitForFullBuffer = fullyPrebufferBeforehand;
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            _bufferLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, bufferLength);
            _buffer = BufferPool<float>.Rent(_bufferLengthSamplesPerChannel * format.NumChannels);
            PrebufferingStartedEvent = new AsyncEvent<EventArgs>();
            PrebufferingFinishedEvent = new AsyncEvent<EventArgs>();
        }

        /// <summary>
        /// Volatile count of current number of samples (per channel) currently stored in the buffer
        /// </summary>
        public int SamplesPerChannelCurrentlyBuffered => _samplesPerChannelInBuffer;

        public AsyncEvent<EventArgs> PrebufferingStartedEvent { get; private set; }
        public AsyncEvent<EventArgs> PrebufferingFinishedEvent { get; private set; }

        protected override async ValueTask FlushAsyncInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int segmentOneSize = FastMath.Min(_samplesPerChannelInBuffer, _bufferLengthSamplesPerChannel - _bufferReadCursorPerChannel);
            if (segmentOneSize > 0)
            {
                await Output.WriteAsync(_buffer.Buffer, _bufferReadCursorPerChannel * InputFormat.NumChannels, segmentOneSize, cancelToken, realTime).ConfigureAwait(false);
                _bufferReadCursorPerChannel = 0;
                _samplesPerChannelInBuffer -= segmentOneSize;
                if (_samplesPerChannelInBuffer > 0)
                {
                    await Output.WriteAsync(_buffer.Buffer, 0, _samplesPerChannelInBuffer, cancelToken, realTime).ConfigureAwait(false);
                }
            }

            _bufferReadCursorPerChannel = 0;
            _bufferWriteCursorPerChannel = 0;
            _samplesPerChannelInBuffer = 0;
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (Input.PlaybackFinished && _samplesPerChannelInBuffer == 0)
            {
                return -1;
            }

            int totalSamplesPerChannelWrittenToOutput = 0;
            while (totalSamplesPerChannelWrittenToOutput < count)
            {
                // First, attempt to fill the buffer as much as possible
                bool continueReadingFromInput =
                    _samplesPerChannelInBuffer < _bufferLengthSamplesPerChannel &&
                    !Input.PlaybackFinished;

                while (continueReadingFromInput)
                {
                    int samplesPerChannelCanWriteIntoBufferTotal = _bufferLengthSamplesPerChannel - _samplesPerChannelInBuffer;
                    int samplesPerChannelCanWriteIntoBufferAtOnce = FastMath.Min(_bufferLengthSamplesPerChannel - _bufferWriteCursorPerChannel, samplesPerChannelCanWriteIntoBufferTotal);
                    if (samplesPerChannelCanWriteIntoBufferAtOnce > 0)
                    {
                        int samplesPerChannelActuallyReadFromInput = await Input.ReadAsync(
                            _buffer.Buffer, _bufferWriteCursorPerChannel * InputFormat.NumChannels, samplesPerChannelCanWriteIntoBufferAtOnce, cancelToken, realTime).ConfigureAwait(false);
                        if (samplesPerChannelActuallyReadFromInput > 0)
                        {
                            if (_waitForFullBuffer && _samplesPerChannelInBuffer == 0)
                            {
                                await PrebufferingStartedEvent.Fire(this, new EventArgs(), realTime).ConfigureAwait(false);
                            }

                            _bufferWriteCursorPerChannel = (_bufferWriteCursorPerChannel + samplesPerChannelActuallyReadFromInput) % _bufferLengthSamplesPerChannel;
                            _samplesPerChannelInBuffer += samplesPerChannelActuallyReadFromInput;
                            if (_waitForFullBuffer && _samplesPerChannelInBuffer == _bufferLengthSamplesPerChannel)
                            {
                                _waitForFullBuffer = false;
                                await PrebufferingFinishedEvent.Fire(this, new EventArgs(), realTime).ConfigureAwait(false);
                            }
                        }
                        else if (_waitForFullBuffer && samplesPerChannelActuallyReadFromInput < 0)
                        {
                            _waitForFullBuffer = false;
                            await PrebufferingFinishedEvent.Fire(this, new EventArgs(), realTime).ConfigureAwait(false);
                        }

                        // Continue reading as long as the input provides the number of samples that we request, or until the buffer is full
                        continueReadingFromInput =
                            (samplesPerChannelActuallyReadFromInput == samplesPerChannelCanWriteIntoBufferAtOnce &&
                            _samplesPerChannelInBuffer < _bufferLengthSamplesPerChannel);
                    }
                    else
                    {
                        continueReadingFromInput = false;
                    }
                }

                // Catch an edge case where input stream ends while we are prebuffering
                if (_waitForFullBuffer && Input.PlaybackFinished)
                {
                    _waitForFullBuffer = false;
                    await PrebufferingFinishedEvent.Fire(this, new EventArgs(), realTime).ConfigureAwait(false);
                }

                if (_samplesPerChannelInBuffer == 0)
                {
                    if (totalSamplesPerChannelWrittenToOutput == 0 &&
                        Input.PlaybackFinished)
                    {
                        // Input finished playback while we were buffering
                        return -1;
                    }

                    // If we can't get anything in the buffer now, just return what we have
                    return totalSamplesPerChannelWrittenToOutput;
                }

                if (!_waitForFullBuffer)
                {
                    // Now write output to caller, potentially in two parts
                    int samplesPerChannelToReadFromBufferTotal = FastMath.Min(count - totalSamplesPerChannelWrittenToOutput, _samplesPerChannelInBuffer);
                    int samplesPerChannelToReadFromBufferFirstSegment = FastMath.Min(_bufferLengthSamplesPerChannel - _bufferReadCursorPerChannel, samplesPerChannelToReadFromBufferTotal);
                    int samplesPerChannelToReadFromBufferSecondSegment = FastMath.Max(0, samplesPerChannelToReadFromBufferTotal - samplesPerChannelToReadFromBufferFirstSegment);
                    if (samplesPerChannelToReadFromBufferFirstSegment > 0)
                    {
                        ArrayExtensions.MemCopy(
                            _buffer.Buffer,
                            _bufferReadCursorPerChannel * InputFormat.NumChannels,
                            buffer,
                            (offset + (totalSamplesPerChannelWrittenToOutput * InputFormat.NumChannels)),
                            samplesPerChannelToReadFromBufferFirstSegment * InputFormat.NumChannels);
                        _bufferReadCursorPerChannel = (_bufferReadCursorPerChannel + samplesPerChannelToReadFromBufferFirstSegment) % _bufferLengthSamplesPerChannel;
                        totalSamplesPerChannelWrittenToOutput += samplesPerChannelToReadFromBufferFirstSegment;
                    }

                    if (samplesPerChannelToReadFromBufferSecondSegment > 0)
                    {
                        ArrayExtensions.MemCopy(
                            _buffer.Buffer,
                            _bufferReadCursorPerChannel * InputFormat.NumChannels,
                            buffer,
                            (offset + (totalSamplesPerChannelWrittenToOutput * InputFormat.NumChannels)),
                            samplesPerChannelToReadFromBufferSecondSegment * InputFormat.NumChannels);
                        _bufferReadCursorPerChannel = (_bufferReadCursorPerChannel + samplesPerChannelToReadFromBufferSecondSegment) % _bufferLengthSamplesPerChannel;
                        totalSamplesPerChannelWrittenToOutput += samplesPerChannelToReadFromBufferSecondSegment;
                    }

                    _samplesPerChannelInBuffer -= samplesPerChannelToReadFromBufferTotal;
                }
                else
                {
                    // Still prebuffering and buffer is not full. Can't return any data
                    return 0;
                }
            }
            
            return totalSamplesPerChannelWrittenToOutput;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelReadFromInput = 0;
            int samplesPerChannelWrittenToOutput = 0;

            // First we want to fill the buffer entirely, and then any excess above that will be written to output
            int targetSamplesPerChannelToWriteToOutput = FastMath.Max(0, count - (_bufferLengthSamplesPerChannel - _samplesPerChannelInBuffer));

            while (samplesPerChannelReadFromInput < count ||
                samplesPerChannelWrittenToOutput < targetSamplesPerChannelToWriteToOutput)
            {
                // Read from input to buffer, as much as possible
                int samplesPerChannelCanWriteIntoBufferTotal = FastMath.Min(count - samplesPerChannelReadFromInput, _bufferLengthSamplesPerChannel - _samplesPerChannelInBuffer);
                int samplesPerChannelCanWriteIntoBufferInOneSegment = FastMath.Min(_bufferLengthSamplesPerChannel - _bufferWriteCursorPerChannel, samplesPerChannelCanWriteIntoBufferTotal);
                if (samplesPerChannelCanWriteIntoBufferInOneSegment > 0)
                {
                    ArrayExtensions.MemCopy(
                        buffer,
                        (offset + (samplesPerChannelReadFromInput * InputFormat.NumChannels)),
                        _buffer.Buffer,
                        _bufferWriteCursorPerChannel * InputFormat.NumChannels,
                        samplesPerChannelCanWriteIntoBufferInOneSegment * InputFormat.NumChannels);
                    _bufferWriteCursorPerChannel = (_bufferWriteCursorPerChannel + samplesPerChannelCanWriteIntoBufferInOneSegment) % _bufferLengthSamplesPerChannel;
                    _samplesPerChannelInBuffer += samplesPerChannelCanWriteIntoBufferInOneSegment;
                    samplesPerChannelReadFromInput += samplesPerChannelCanWriteIntoBufferInOneSegment;
                }

                if (_samplesPerChannelInBuffer == _bufferLengthSamplesPerChannel)
                {
                    _waitForFullBuffer = false;
                    await PrebufferingFinishedEvent.Fire(this, new EventArgs(), realTime).ConfigureAwait(false);
                }

                if (!_waitForFullBuffer)
                {
                    // Copy from buffer to output, as much as possible as long as the buffer is full
                    int samplesPerChannelCanReadFromBufferTotal = FastMath.Min(targetSamplesPerChannelToWriteToOutput - samplesPerChannelWrittenToOutput, _samplesPerChannelInBuffer);
                    int samplesPerChannelCanReadFromBufferInOneSegment = FastMath.Min(_bufferLengthSamplesPerChannel - _bufferReadCursorPerChannel, samplesPerChannelCanReadFromBufferTotal);
                    if (samplesPerChannelCanReadFromBufferInOneSegment > 0)
                    {
                        await Output.WriteAsync(
                            _buffer.Buffer,
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

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            if (disposing)
            {
                _buffer?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
