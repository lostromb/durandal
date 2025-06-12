using Durandal.Common.Audio.Codecs.Opus.Common;
using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio filter which alters the sample rate of a signal
    /// </summary>
    public sealed class ResamplingFilter : AbstractAudioSampleFilter, IAudioDelayingFilter
    {
        private static readonly TimeSpan SCRATCH_SPACE_LENGTH = TimeSpan.FromMilliseconds(50);

        private readonly IResampler _resampler;
        private readonly int _sourceSampleRate;
        private readonly int _targetSampleRate;

        private int _samplesPerChannelInInBuffer = 0;
        private readonly int _inBufCapacitySamplesPerChannel;
        private readonly float[] _inBuffer;

        private int _samplesPerChannelInOutBuffer = 0;
        private readonly int _outBufCapacitySamplesPerChannel;
        private readonly float[] _outBuffer;

        private readonly int _scratchBufCapacitySamplesPerChannel;
        private readonly float[] _scratch;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="ResamplingFilter"/>.
        /// </summary>
        /// <param name="graph">The audio graph this component will be a part of</param>
        /// <param name="nodeCustomName">The name of this node in the audio graph, or null</param>
        /// <param name="numChannels">The number of input channels</param>
        /// <param name="channelLayout">The channel layout of input and output</param>
        /// <param name="inputSampleRate">The input sample rate</param>
        /// <param name="outputSampleRate">The output (resampled) sample rate</param>
        /// <param name="logger">A logger</param>
        /// <param name="quality">The resampler quality</param>
        public ResamplingFilter(
            WeakPointer<IAudioGraph> graph,
            string nodeCustomName,
            int numChannels,
            MultiChannelMapping channelLayout,
            int inputSampleRate,
            int outputSampleRate,
            ILogger logger,
            AudioProcessingQuality quality = AudioProcessingQuality.Balanced) : base(graph, nameof(ResamplingFilter), nodeCustomName)
        {
            _sourceSampleRate = inputSampleRate;
            _targetSampleRate = outputSampleRate;

            if (inputSampleRate != outputSampleRate)
            {
                _resampler = ResamplerFactory.Create(numChannels, inputSampleRate, outputSampleRate, quality, logger);

                _inBufCapacitySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(_sourceSampleRate, SCRATCH_SPACE_LENGTH);
                _outBufCapacitySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(_targetSampleRate, SCRATCH_SPACE_LENGTH);
                _scratchBufCapacitySamplesPerChannel = _inBufCapacitySamplesPerChannel;
                _inBuffer = new float[_inBufCapacitySamplesPerChannel * numChannels];
                _outBuffer = new float[_outBufCapacitySamplesPerChannel * numChannels];
                _scratch = new float[_scratchBufCapacitySamplesPerChannel * numChannels];
            }

            InputFormat = new AudioSampleFormat(inputSampleRate, numChannels, channelLayout);
            OutputFormat = new AudioSampleFormat(outputSampleRate, numChannels, channelLayout);
        }

        /// <summary>
        /// Gets the algorithmic delay introduced by this resampler
        /// </summary>
        public TimeSpan AlgorithmicDelay
        {
            get
            {
                if (_sourceSampleRate == _targetSampleRate)
                {
                    return TimeSpan.Zero;
                }

                return _resampler.OutputLatency;
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_sourceSampleRate == _targetSampleRate)
            {
                // Passthrough mode, no resampling needed
                return await Input.ReadAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
            }

            int samplesPerChannelWrittenToOutput = 0;
            while (samplesPerChannelWrittenToOutput < count)
            {
                // Find the maximum amount of samples that we can process with the current scratch buffer
                // -10 in order to round down, so we don't overflow the output buffer
                int totalOutputSamplesPerChannelCanProcess = _outBufCapacitySamplesPerChannel - _samplesPerChannelInOutBuffer - 10;
                int totalInputSamplesPerChannelCanProcess = FastMath.Max(1, (int)(((long)totalOutputSamplesPerChannelCanProcess) * _sourceSampleRate / _targetSampleRate));
                int maxSamplesPerChannelCanReadFromInput = FastMath.Max(1, (int)((long)(count - samplesPerChannelWrittenToOutput) * _sourceSampleRate / _targetSampleRate) + 10); // again, +10 so we don't either overflow the buffer or end up 1 sample short

                // Find the minimum amount of input samples needed to either fill the scratch buffer or satisfy the count requested by the caller
                int thisInputMaxBatchSizePerChannel = FastMath.Min(totalInputSamplesPerChannelCanProcess, maxSamplesPerChannelCanReadFromInput);
                int inBufferReadCursorAbsolute = _samplesPerChannelInInBuffer * InputFormat.NumChannels;

                // Actually read from input
                int thisInputActualBatchSizePerChannel = await Input.ReadAsync(_inBuffer, inBufferReadCursorAbsolute, thisInputMaxBatchSizePerChannel, cancelToken, realTime).ConfigureAwait(false);
                if (thisInputActualBatchSizePerChannel < 0)
                {
                    // End of input. Maybe we could flush out a few orphaned samples at this point, but otherwise we're pretty much done.
                    return samplesPerChannelWrittenToOutput == 0 ? -1 : samplesPerChannelWrittenToOutput;
                }
                else if (thisInputActualBatchSizePerChannel == 0)
                {
                    return samplesPerChannelWrittenToOutput;
                }

                _samplesPerChannelInInBuffer += thisInputActualBatchSizePerChannel;

                // Now resample
                int inLen = _samplesPerChannelInInBuffer;
                int outCur = _samplesPerChannelInOutBuffer * InputFormat.NumChannels;
                int outLen = _outBufCapacitySamplesPerChannel - _samplesPerChannelInOutBuffer;
                _resampler.ProcessInterleaved(_inBuffer, 0, ref inLen, _outBuffer, outCur, ref outLen);

                _samplesPerChannelInOutBuffer += outLen;


                // Shrink the input buffer
                int newStartOfInBufferPerChannel = inLen;
                int inBufferSamplesOrphaned = _samplesPerChannelInInBuffer - newStartOfInBufferPerChannel;
                if (inBufferSamplesOrphaned > 0)
                {
                    ArrayExtensions.MemMove(_inBuffer, newStartOfInBufferPerChannel * InputFormat.NumChannels, 0, inBufferSamplesOrphaned * InputFormat.NumChannels);
                }

                _samplesPerChannelInInBuffer = inBufferSamplesOrphaned;

                // Copy from output buffer to output
                int samplesPerChannelCanCopyToOutput = FastMath.Min(_samplesPerChannelInOutBuffer, count - samplesPerChannelWrittenToOutput);

                ArrayExtensions.MemCopy(
                    _outBuffer,
                    0,
                    buffer,
                    offset + (samplesPerChannelWrittenToOutput * InputFormat.NumChannels),
                    samplesPerChannelCanCopyToOutput * InputFormat.NumChannels);

                // Shrink the output buffer
                int newStartOfOutBufferPerChannel = samplesPerChannelCanCopyToOutput;
                int outBufferSamplesOrphaned = _samplesPerChannelInOutBuffer - samplesPerChannelCanCopyToOutput;
                if (outBufferSamplesOrphaned > 0)
                {
                    ArrayExtensions.MemMove(_outBuffer, newStartOfOutBufferPerChannel * InputFormat.NumChannels, 0, outBufferSamplesOrphaned * InputFormat.NumChannels);
                }

                _samplesPerChannelInOutBuffer = outBufferSamplesOrphaned;
                samplesPerChannelWrittenToOutput += samplesPerChannelCanCopyToOutput;
            }

            return samplesPerChannelWrittenToOutput;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_sourceSampleRate == _targetSampleRate)
            {
                // Passthrough mode, no resampling needed
                await Output.WriteAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
                return;
            }

            int samplesPerChannelReadFromInput = 0;
            while (samplesPerChannelReadFromInput < count)
            {
                // Find the maximum amount of samples that we can process with the current scratch buffer
                // -10 in order to round down, so we don't overflow the output buffer
                int totalOutputSamplesPerChannelCanProcess = _outBufCapacitySamplesPerChannel - _samplesPerChannelInOutBuffer - 10;
                int totalInputSamplesPerChannelCanProcess = FastMath.Max(1, (int)(((long)totalOutputSamplesPerChannelCanProcess) * _sourceSampleRate / _targetSampleRate));
                int maxSamplesPerChannelCanReadFromInput = count - samplesPerChannelReadFromInput;

                // Find the minimum amount of input samples needed to either fill the scratch buffer or satisfy the count requested by the caller
                int thisInputActualBatchSizePerChannel = FastMath.Min(totalInputSamplesPerChannelCanProcess, maxSamplesPerChannelCanReadFromInput);
                int inBufferReadCursorAbsolute = _samplesPerChannelInInBuffer * InputFormat.NumChannels;

                // Actually read from input
                ArrayExtensions.MemCopy(
                    buffer,
                    (offset + (samplesPerChannelReadFromInput * InputFormat.NumChannels)),
                    _inBuffer,
                    inBufferReadCursorAbsolute,
                    thisInputActualBatchSizePerChannel * InputFormat.NumChannels);

                _samplesPerChannelInInBuffer += thisInputActualBatchSizePerChannel;

                // Now resample
                int inLen = _samplesPerChannelInInBuffer;
                int outCur = _samplesPerChannelInOutBuffer * InputFormat.NumChannels;
                int outLen = _outBufCapacitySamplesPerChannel - _samplesPerChannelInOutBuffer;
                _resampler.ProcessInterleaved(_inBuffer, 0, ref inLen, _outBuffer, outCur, ref outLen);

                _samplesPerChannelInOutBuffer += outLen;

                // Shrink the input buffer
                int newStartOfInBufferPerChannel = inLen;
                int inBufferSamplesOrphaned = _samplesPerChannelInInBuffer - newStartOfInBufferPerChannel;
                if (inBufferSamplesOrphaned > 0)
                {
                    ArrayExtensions.MemMove(_inBuffer, newStartOfInBufferPerChannel * InputFormat.NumChannels, 0, inBufferSamplesOrphaned * InputFormat.NumChannels);
                }

                _samplesPerChannelInInBuffer = inBufferSamplesOrphaned;

                // Copy from output buffer to output
                await Output.WriteAsync(
                    _outBuffer,
                    0,
                    _samplesPerChannelInOutBuffer,
                    cancelToken,
                    realTime).ConfigureAwait(false);
                
                // Shrink the output buffer
                _samplesPerChannelInOutBuffer = 0;
                samplesPerChannelReadFromInput += thisInputActualBatchSizePerChannel;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (disposing)
                {
                    _resampler?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
