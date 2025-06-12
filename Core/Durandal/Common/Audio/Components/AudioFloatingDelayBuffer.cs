using Durandal.Common.Audio.Codecs.Opus.Common;
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
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio graph component which introduces a time delay which can be dynamically altered
    /// without significantly affecting the continuity of the audio (however, pitch will be shifted
    /// during the adjustment)
    /// </summary>
    public sealed class AudioFloatingDelayBuffer : AbstractAudioSampleFilter, IAudioDelayingFilter
    {
        /// <summary>
        /// Amount of the excess buffer that is reserved to stage audio data before it gets written to output.
        /// </summary>
        private static readonly TimeSpan SCRATCH_SPACE_TIME = TimeSpan.FromMilliseconds(100);
        private readonly int _scratchSpaceSamplesPerChannel;

        /// <summary>
        /// Amount of speedup / slowdown to apply when changing delay. This also determines how much the pitch gets shifted.
        /// </summary>
        private const float ADJUSTMENT_SPEED = 1.01f;

        private readonly int _inputBufferCapacitySamplesPerChannel;
        private readonly int _outputBufferCapacitySamplesPerChannel;

        // Buffer for staging unaltered input. Always aligned so the data begins at 0
        private readonly float[] _inputBuffer;

        // Buffer for staging potentially stretched output before writing it downstream. Always aligned so the data begins at 0.
        private readonly float[] _outputBuffer;
        private readonly int _maxDelaySamplesPerChannel;

        private int _samplesPerChannelInInputBuffer;
        private int _samplesPerChannelInOutputBuffer;

        // the actual amount of delay currently being applied
        private int _currentDelaySamplesPerChannel;

        // the target amount of delay, if we are in the process of gradually adjusting
        private int _targetDelaySamplesPerChannel;

        /// <summary>
        /// Constructs a new floating delay buffer.
        /// </summary>
        /// <param name="graph">The audio graph to associate with this component</param>
        /// <param name="format">The format of the audio signal</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="initialDelay">The initial delay to apply.</param>
        /// <param name="maxDelay">The maximum amount of delay that can be applied over the lifetime of this component.</param>
        public AudioFloatingDelayBuffer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            TimeSpan initialDelay,
            TimeSpan maxDelay) : base(graph, nameof(AudioFloatingDelayBuffer), nodeCustomName)
        {
            if (maxDelay <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("Delay buffer length must be a positive timespan");
            }
            if (maxDelay > TimeSpan.FromMinutes(1))
            {
                throw new ArgumentOutOfRangeException("Delay buffer length of " + maxDelay.PrintTimeSpan() + " is ridiculously large");
            }
            if (initialDelay < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("Initial delay cannot be negative");
            }
            if (initialDelay > maxDelay)
            {
                throw new ArgumentOutOfRangeException("Initial delay cannot be greater than maximum delay");
            }

            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            _scratchSpaceSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, SCRATCH_SPACE_TIME);
            _inputBufferCapacitySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, maxDelay + SCRATCH_SPACE_TIME);
            _maxDelaySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, maxDelay);
            _outputBufferCapacitySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, TimeSpan.FromTicks((long)((double)(SCRATCH_SPACE_TIME.Ticks + TimeSpan.FromMilliseconds(1).Ticks) * ADJUSTMENT_SPEED)));
            _inputBuffer = new float[_inputBufferCapacitySamplesPerChannel * format.NumChannels];
            _outputBuffer = new float[_outputBufferCapacitySamplesPerChannel * format.NumChannels];
            _currentDelaySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, initialDelay);
            _targetDelaySamplesPerChannel = _currentDelaySamplesPerChannel;
            _samplesPerChannelInInputBuffer = _currentDelaySamplesPerChannel; // the buffer is marked as initially filled with silence
            _samplesPerChannelInOutputBuffer = 0;
        }
        
        /// <summary>
        /// Gets or sets the current amount of time delay that is being applied by this buffer.
        /// Changes to the delay are not reflected instantly, the adjustment is done gradually to avoid discontinuities in the audio signal.
        /// </summary>
        public TimeSpan AlgorithmicDelay
        {
            get
            {
                return AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, FastMath.Max(0, _currentDelaySamplesPerChannel));
            }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException("Buffer delay cannot be negative");
                }

                _targetDelaySamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(OutputFormat.SampleRateHz, value);
                if (_targetDelaySamplesPerChannel > _maxDelaySamplesPerChannel)
                {
                    throw new ArgumentOutOfRangeException("Buffer delay cannot exceed maximum configured delay");
                }
            }
        }

        /// <summary>
        /// Returns the maximum time delay that this buffer is configured to allow.
        /// </summary>
        public TimeSpan MaximumDelay
        {
            get
            {
                return AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, _maxDelaySamplesPerChannel);
            }
        }

        protected override async ValueTask FlushAsyncInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Write output and then excess input buffer directly to output
            if (_samplesPerChannelInOutputBuffer > 0)
            {
                await Output.WriteAsync(_outputBuffer, 0, _samplesPerChannelInOutputBuffer, cancelToken, realTime);
                _samplesPerChannelInOutputBuffer = 0;
            }

            int currentDelay = FastMath.Max(0, _currentDelaySamplesPerChannel);
            if (_samplesPerChannelInInputBuffer > currentDelay)
            {
                await Output.WriteAsync(_inputBuffer, 0, _samplesPerChannelInInputBuffer - currentDelay, cancelToken, realTime);

                ArrayExtensions.MemMove(
                    _inputBuffer,
                    (_samplesPerChannelInInputBuffer - currentDelay) * InputFormat.NumChannels,
                    0,
                    currentDelay * InputFormat.NumChannels);
                _samplesPerChannelInInputBuffer = currentDelay;
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] outputBuffer, int outputBufferOffset, int samplesPerChannelRequested, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (Input.PlaybackFinished)
            {
                return -1;
            }

            int infiniteReadLoopBreakThreshold = 0; // used to prevent infinite loops with small reads where we keep reading zero samples at a time
            int samplesPerChannelWrittenToOutput = 0;
            while (samplesPerChannelWrittenToOutput < samplesPerChannelRequested)
            {
                cancelToken.ThrowIfCancellationRequested();

                // If we are currently delay-shifting, find out how many samples of input until it ends.
                int samplesPerChannelOfInputUntilShiftingFinishes;
                float rate;
                if (_currentDelaySamplesPerChannel == _targetDelaySamplesPerChannel)
                {
                    rate = 1.0f;
                    samplesPerChannelOfInputUntilShiftingFinishes = 0;
                }
                else if (_currentDelaySamplesPerChannel < _targetDelaySamplesPerChannel)
                {
                    // Slowing down (delay is increasing, rate is below 1, input samples < output samples)
                    rate = 1.0f / ADJUSTMENT_SPEED;
                    samplesPerChannelOfInputUntilShiftingFinishes = (int)Math.Round((float)(_targetDelaySamplesPerChannel - _currentDelaySamplesPerChannel) / (ADJUSTMENT_SPEED - 1.0f));
                }
                else
                {
                    // Speeding up (delay is decreasing, rate is above 1, input samples > output samples)
                    rate = ADJUSTMENT_SPEED;
                    samplesPerChannelOfInputUntilShiftingFinishes = (int)Math.Round((float)(_targetDelaySamplesPerChannel - _currentDelaySamplesPerChannel) / ((1.0f / ADJUSTMENT_SPEED) - 1.0f));
                }

                int samplesPerChannelShouldReadFromInput;
                if (_currentDelaySamplesPerChannel == _targetDelaySamplesPerChannel)
                {
                    samplesPerChannelShouldReadFromInput = FastMath.Min(
                        _inputBufferCapacitySamplesPerChannel - _samplesPerChannelInInputBuffer, // total capacity of input buffer
                        samplesPerChannelRequested - samplesPerChannelWrittenToOutput - _samplesPerChannelInInputBuffer + _currentDelaySamplesPerChannel); // number of samples we would like to have in input buffer, including delay buffer
                }
                else
                {
                    samplesPerChannelShouldReadFromInput = FastMath.Min(
                        samplesPerChannelOfInputUntilShiftingFinishes, // how long until the next shifting boundary, so a single read doesn't cross the boundary
                        FastMath.Min(
                        _inputBufferCapacitySamplesPerChannel - _samplesPerChannelInInputBuffer, // total capacity of input buffer
                        // number of samples we would like to have in input buffer, scaled by rate, including delay buffer
                        (int)Math.Round((samplesPerChannelRequested - samplesPerChannelWrittenToOutput - _samplesPerChannelInInputBuffer) / rate) + _currentDelaySamplesPerChannel));
                }

                if (samplesPerChannelShouldReadFromInput > 0)
                {
                    // Try reading from input
                    int samplesPerChannelActuallyReadThisTime = await Input.ReadAsync(
                        _inputBuffer,
                        _samplesPerChannelInInputBuffer * InputFormat.NumChannels,
                        samplesPerChannelShouldReadFromInput,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    if (samplesPerChannelActuallyReadThisTime <= 0)
                    {
                        // Input stream has finished completely
                        if (samplesPerChannelWrittenToOutput == 0 && samplesPerChannelActuallyReadThisTime < 0)
                        {
                            return -1;
                        }

                        // or it has returned 0 and we should just return what we have
                        return samplesPerChannelWrittenToOutput;
                    }

                    _samplesPerChannelInInputBuffer += samplesPerChannelActuallyReadThisTime;
                }

                // See if we are currently time-shifting
                if (_currentDelaySamplesPerChannel == _targetDelaySamplesPerChannel)
                {
                    // Delay is not changing.
                    int samplesPerChannelCanProcessFromInput = FastMath.Min(
                        _scratchSpaceSamplesPerChannel - _samplesPerChannelInOutputBuffer, // output buffer capacity
                            FastMath.Min(
                            _samplesPerChannelInInputBuffer - _currentDelaySamplesPerChannel, // available in input buffer
                            samplesPerChannelRequested - samplesPerChannelWrittenToOutput)); // remaining samples requested by caller

                    // Copy from input to output buffer
                    ArrayExtensions.MemCopy(
                        _inputBuffer,
                        0,
                        _outputBuffer,
                        _samplesPerChannelInOutputBuffer * InputFormat.NumChannels,
                        samplesPerChannelCanProcessFromInput * InputFormat.NumChannels);
                    _samplesPerChannelInOutputBuffer += samplesPerChannelCanProcessFromInput;

                    // Shift input buffer left
                    if (samplesPerChannelCanProcessFromInput < _samplesPerChannelInInputBuffer)
                    {
                        ArrayExtensions.MemMove(
                            _inputBuffer,
                            samplesPerChannelCanProcessFromInput * InputFormat.NumChannels,
                            0,
                            (_samplesPerChannelInInputBuffer - samplesPerChannelCanProcessFromInput) * InputFormat.NumChannels);
                    }

                    _samplesPerChannelInInputBuffer -= samplesPerChannelCanProcessFromInput;
                }
                else
                {
                    // Delay IS changing. We need to be very careful about our input/output lengths here because they are slightly different
                    int resamplerInputLength =
                        FastMath.Min(
                            samplesPerChannelOfInputUntilShiftingFinishes, // time until the next shifting boundary
                            FastMath.Min(
                            (int)Math.Round((_scratchSpaceSamplesPerChannel - _samplesPerChannelInOutputBuffer) / rate), // output buffer capacity scaled to inverse rate
                                FastMath.Min(
                                _samplesPerChannelInInputBuffer - _currentDelaySamplesPerChannel, // available in input buffer
                                (int)Math.Round((samplesPerChannelRequested - samplesPerChannelWrittenToOutput) * rate)))); // remaining samples requested by caller scaled to rate

                    if (resamplerInputLength > 0)
                    {
                        int resamplerOutputLength = _outputBufferCapacitySamplesPerChannel - _samplesPerChannelInOutputBuffer;

                        HermitianInterpolation.ResampleInterleavedByRate(
                            _inputBuffer,
                            0,
                            ref resamplerInputLength,
                            _outputBuffer,
                            _samplesPerChannelInOutputBuffer * InputFormat.NumChannels,
                            ref resamplerOutputLength,
                            (double)rate,
                            InputFormat.NumChannels);

                        _samplesPerChannelInOutputBuffer += resamplerOutputLength;

                        // Alter the target delay based on the time differential of the resampling
                        int resamplerDelayDelta = resamplerOutputLength - resamplerInputLength;
                        int samplesPerChannelProcessedFromInput = resamplerInputLength;

                        // Shift input buffer left
                        if (samplesPerChannelProcessedFromInput < _samplesPerChannelInInputBuffer)
                        {
                            ArrayExtensions.MemMove(
                                _inputBuffer,
                                samplesPerChannelProcessedFromInput * InputFormat.NumChannels,
                                0,
                                (_samplesPerChannelInInputBuffer - samplesPerChannelProcessedFromInput) * InputFormat.NumChannels);
                        }

                        _samplesPerChannelInInputBuffer -= samplesPerChannelProcessedFromInput;
                        _currentDelaySamplesPerChannel = FastMath.Max(0, FastMath.Min(_samplesPerChannelInInputBuffer, _currentDelaySamplesPerChannel + resamplerDelayDelta));
                        if (samplesPerChannelOfInputUntilShiftingFinishes - samplesPerChannelProcessedFromInput <= 0)
                        {
                            Debug.Assert(Math.Abs(_targetDelaySamplesPerChannel - _currentDelaySamplesPerChannel) < 2); // If we just reached the end of sampling, assert that the current delay has also come near the target
                        }

                        // Also check if we've hit the current delay target. It's possible we've overshot it, in which case
                        // just alter the target a bit to prevent us from teetering back and forth
                        if (Math.Abs(_targetDelaySamplesPerChannel - _currentDelaySamplesPerChannel) < 2)
                        {
                            _targetDelaySamplesPerChannel = _currentDelaySamplesPerChannel;
                        }
                    }
                }

                // Copy output buffer to caller
                int samplesPerChannelCanCopyToOutput = FastMath.Min(_samplesPerChannelInOutputBuffer, samplesPerChannelRequested - samplesPerChannelWrittenToOutput);
                if (samplesPerChannelCanCopyToOutput > 0)
                {
                    ArrayExtensions.MemCopy(
                        _outputBuffer,
                        0,
                        outputBuffer,
                        (outputBufferOffset + (samplesPerChannelWrittenToOutput * InputFormat.NumChannels)),
                        samplesPerChannelCanCopyToOutput * InputFormat.NumChannels);
                    samplesPerChannelWrittenToOutput += samplesPerChannelCanCopyToOutput;

                    // Shift output buffer left
                    if (samplesPerChannelCanCopyToOutput < _samplesPerChannelInOutputBuffer)
                    {
                        ArrayExtensions.MemMove(
                            _outputBuffer,
                            samplesPerChannelCanCopyToOutput * InputFormat.NumChannels,
                            0,
                            (_samplesPerChannelInOutputBuffer - samplesPerChannelCanCopyToOutput) * InputFormat.NumChannels);
                    }

                    _samplesPerChannelInOutputBuffer -= samplesPerChannelCanCopyToOutput;
                }

                // If we have approximately satisfied the read request (to within a few samples), just go ahead and return
                if (Math.Abs(samplesPerChannelWrittenToOutput - samplesPerChannelRequested) < infiniteReadLoopBreakThreshold++)
                {
                    return samplesPerChannelWrittenToOutput;
                }
            }

            return samplesPerChannelWrittenToOutput;                
        }

        protected override async ValueTask WriteAsyncInternal(float[] inputBuffer, int inputBufferOffset, int samplesPerChannelToWrite, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelReadFromCaller = 0;
            while (samplesPerChannelReadFromCaller < samplesPerChannelToWrite)
            {
                cancelToken.ThrowIfCancellationRequested();

                // If we are currently delay-shifting, find out how many samples of input until it ends.
                int samplesPerChannelOfInputUntilShiftingFinishes;
                float rate;
                if (_currentDelaySamplesPerChannel == _targetDelaySamplesPerChannel)
                {
                    rate = 1.0f;
                    samplesPerChannelOfInputUntilShiftingFinishes = 0;
                }
                else if (_currentDelaySamplesPerChannel < _targetDelaySamplesPerChannel)
                {
                    // Slowing down (delay is increasing, rate is below 1, input samples < output samples)
                    rate = 1.0f / ADJUSTMENT_SPEED;
                    samplesPerChannelOfInputUntilShiftingFinishes = (int)Math.Round((float)(_targetDelaySamplesPerChannel - _currentDelaySamplesPerChannel) / (ADJUSTMENT_SPEED - 1.0f));
                }
                else
                {
                    // Speeding up (delay is decreasing, rate is above 1, input samples > output samples)
                    rate = ADJUSTMENT_SPEED;
                    samplesPerChannelOfInputUntilShiftingFinishes = (int)Math.Round((float)(_targetDelaySamplesPerChannel - _currentDelaySamplesPerChannel) / ((1.0f / ADJUSTMENT_SPEED) - 1.0f));
                }

                int samplesPerChannelShouldReadFromInput;
                if (_currentDelaySamplesPerChannel == _targetDelaySamplesPerChannel)
                {
                    samplesPerChannelShouldReadFromInput = FastMath.Min(
                        _inputBufferCapacitySamplesPerChannel - _samplesPerChannelInInputBuffer, // total capacity of input buffer
                        samplesPerChannelToWrite - samplesPerChannelReadFromCaller - _samplesPerChannelInInputBuffer + _currentDelaySamplesPerChannel); // number of samples we would like to have in input buffer, including delay buffer
                }
                else
                {
                    samplesPerChannelShouldReadFromInput =
                        FastMath.Min(_inputBufferCapacitySamplesPerChannel - _samplesPerChannelInInputBuffer, // total capacity of input buffer
                        samplesPerChannelToWrite - samplesPerChannelReadFromCaller); // amount still remaining to copy from caller
                }

                if (samplesPerChannelShouldReadFromInput > 0)
                {
                    // Try reading from input
                    ArrayExtensions.MemCopy(
                        inputBuffer,
                        (inputBufferOffset + (samplesPerChannelReadFromCaller * InputFormat.NumChannels)),
                        _inputBuffer,
                        _samplesPerChannelInInputBuffer * InputFormat.NumChannels,
                        samplesPerChannelShouldReadFromInput * InputFormat.NumChannels);

                    samplesPerChannelReadFromCaller += samplesPerChannelShouldReadFromInput;
                    _samplesPerChannelInInputBuffer += samplesPerChannelShouldReadFromInput;
                }

                // See if we are currently time-shifting
                if (_currentDelaySamplesPerChannel == _targetDelaySamplesPerChannel)
                {
                    // Delay is not changing.
                    int samplesPerChannelCanProcessFromInput = FastMath.Min(
                        _scratchSpaceSamplesPerChannel - _samplesPerChannelInOutputBuffer, // output buffer capacity
                            _samplesPerChannelInInputBuffer - _currentDelaySamplesPerChannel); // available in input buffer

                    // Copy from input to output buffer
                    ArrayExtensions.MemCopy(
                        _inputBuffer,
                        0,
                        _outputBuffer,
                        _samplesPerChannelInOutputBuffer * InputFormat.NumChannels,
                        samplesPerChannelCanProcessFromInput * InputFormat.NumChannels);
                    _samplesPerChannelInOutputBuffer += samplesPerChannelCanProcessFromInput;

                    // Shift input buffer left
                    if (samplesPerChannelCanProcessFromInput < _samplesPerChannelInInputBuffer)
                    {
                        ArrayExtensions.MemMove(
                            _inputBuffer,
                            samplesPerChannelCanProcessFromInput * InputFormat.NumChannels,
                            0,
                            (_samplesPerChannelInInputBuffer - samplesPerChannelCanProcessFromInput) * InputFormat.NumChannels);
                    }

                    _samplesPerChannelInInputBuffer -= samplesPerChannelCanProcessFromInput;
                }
                else
                {
                    // Delay IS changing. We need to be very careful about our input/output lengths here because they are slightly different
                    int resamplerInputLength =
                        FastMath.Min(
                            samplesPerChannelOfInputUntilShiftingFinishes, // time until the next shifting boundary
                            FastMath.Min(
                                (int)Math.Round((_scratchSpaceSamplesPerChannel - _samplesPerChannelInOutputBuffer) / rate), // output buffer capacity scaled to inverse rate
                                _samplesPerChannelInInputBuffer - _currentDelaySamplesPerChannel)); // available in input buffer

                    int resamplerOutputLength = _outputBufferCapacitySamplesPerChannel - _samplesPerChannelInOutputBuffer;

                    HermitianInterpolation.ResampleInterleavedByRate(
                        _inputBuffer,
                        0,
                        ref resamplerInputLength,
                        _outputBuffer,
                        _samplesPerChannelInOutputBuffer * InputFormat.NumChannels,
                        ref resamplerOutputLength,
                        (double)rate,
                        InputFormat.NumChannels);

                    _samplesPerChannelInOutputBuffer += resamplerOutputLength;

                    // Alter the target delay based on the time differential of the resampling
                    int resamplerDelayDelta = resamplerOutputLength - resamplerInputLength;
                    int samplesPerChannelProcessedFromInput = resamplerInputLength;

                    // Shift input buffer left
                    if (samplesPerChannelProcessedFromInput < _samplesPerChannelInInputBuffer)
                    {
                        ArrayExtensions.MemMove(
                            _inputBuffer,
                            samplesPerChannelProcessedFromInput * InputFormat.NumChannels,
                            0,
                            (_samplesPerChannelInInputBuffer - samplesPerChannelProcessedFromInput) * InputFormat.NumChannels);
                    }

                    _samplesPerChannelInInputBuffer -= samplesPerChannelProcessedFromInput;
                    _currentDelaySamplesPerChannel = FastMath.Max(0, FastMath.Min(_samplesPerChannelInInputBuffer, _currentDelaySamplesPerChannel + resamplerDelayDelta));
                    if (samplesPerChannelOfInputUntilShiftingFinishes - samplesPerChannelProcessedFromInput <= 0)
                    {
                        Debug.Assert(Math.Abs(_targetDelaySamplesPerChannel - _currentDelaySamplesPerChannel) < 2); // If we just reached the end of sampling, assert that the current delay has also come near the target
                    }

                    // Also check if we've hit the current delay target. It's possible we've overshot it, in which case
                    // just alter the target a bit to prevent us from teetering back and forth
                    if (Math.Abs(_targetDelaySamplesPerChannel - _currentDelaySamplesPerChannel) < 2)
                    {
                        _targetDelaySamplesPerChannel = _currentDelaySamplesPerChannel;
                    }
                }

                // Copy output buffer to caller
                int samplesPerChannelCanCopyToOutput = _samplesPerChannelInOutputBuffer;
                if (samplesPerChannelCanCopyToOutput > 0)
                {
                    await Output.WriteAsync(
                        _outputBuffer,
                        0,
                        samplesPerChannelCanCopyToOutput,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    // Shift output buffer left
                    if (samplesPerChannelCanCopyToOutput < _samplesPerChannelInOutputBuffer)
                    {
                        ArrayExtensions.MemMove(
                            _outputBuffer,
                            samplesPerChannelCanCopyToOutput * InputFormat.NumChannels,
                            0,
                            (_samplesPerChannelInOutputBuffer - samplesPerChannelCanCopyToOutput) * InputFormat.NumChannels);
                    }

                    _samplesPerChannelInOutputBuffer -= samplesPerChannelCanCopyToOutput;
                }
            }
        }
    }
}
