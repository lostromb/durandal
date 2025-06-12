using Durandal.Common.Collections;
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
    /// Audio filter graph component which applies dynamic volume attenuation to prevent the input signal from peaking
    /// </summary>
    public sealed class AudioGate : AbstractAudioSampleFilter, IAudioDelayingFilter
    {
        private const float MIN_VOLUME_DBA = -72;
        private static readonly TimeSpan RMS_RESPONSE_TIME = TimeSpan.FromMilliseconds(10); // Moving average window to use when calculating RMS
        private static readonly TimeSpan RMS_UPDATE_INTERVAL = TimeSpan.FromMilliseconds(5); // Interval between RMS (compression level) updates. Spaced out in intervals to improve performance.

        private static readonly TimeSpan DEFAULT_ATTACK = TimeSpan.FromMilliseconds(20);
        private static readonly TimeSpan DEFAULT_SUSTAIN = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan DEFAULT_RELEASE = TimeSpan.FromMilliseconds(1000);

        private readonly MovingAverageRmsVolume[] _meanSquareVolumePerChannel; // moving average squared volume per channel (not yet RMS)
        private readonly float[] _processingBuffer; // scratch buffer for calculating sample volume and applying filter
        private readonly int _processingBufferTotalSizePerChannel; // total size of processing buffer
        private readonly int _processingBufferTotalSizeAllChannels; // total size of processing buffer * number of channels
        private readonly float _gateThresholdDba; // (PARAM) The threshold RMS volume to enable/disable gate
        private readonly int _attackLengthSamplesPerChannel; // (PARAM) maximum allowed dB volume change per segment
        private readonly int _sustainLengthSamplesPerChannel; // (PARAM) number of samples to hold "sustain" for
        private readonly int _releaseLengthSamplesPerChannel; // (PARAM) number of samples to hold "release" for
        private readonly int _lookaheadLengthSamplesPerChannel; // (PARAM) number of samples to prebuffer before sending to output
        private readonly int _samplesBetweenRmsUpdates; // Interval between each update to the volume slope based on RMS. This is just for performance to avoid recalculating everything per-sample
        private readonly int _samplesBetweenVolumeSlopeUpdates;

        private int _processingBufferReadCursorPerChannel = 0; // read cursor into processing buffer. Measured as an interleaved offset, so multiply by # channels to get actual array offset
        private int _processingBufferWriteCursorPerChannel = 0; // write cursor into processing buffer
        private int _samplesSinceRmsUpdate = 0; // number of samples elapsed since RMS was calculated and new peaks detected
        private CompressorState _currentState = CompressorState.Neutral; // Current state of gate
        private int _processingBufferTotalSamplesPerChannelStored = 0; // total samples per channel available in processing buffer
        private int _samplesSinceLastStateChange = 0; // number of samples since we last peaked; used to determine whether to attack / sustain / release
        private int _samplesSinceLastVolumeSlopeUpdate = 0;
        // If lookahead is enabled, this tracks how many samples we need to update compressor states internally without outputting anything at the very beginning of the signal
        private int _samplesPerChannelOfPreprocessingRemaining = 0;

        // Volume attenuation parameters used for smooth transitions
        private float _currentFadeSlopeDba = 0;
        private float _currentFadeSlopeLinear = 0;
        private float _targetCompressionVolumeDba = 0; // The target amount of compression, in dBA, used as the value for "sustain" and inital gradient value of "release"

        private float _currentVolumeLinear = 0.0f; // Current linear volume attenuation being applied
        private float _currentVolumeDBA = MIN_VOLUME_DBA; // Current dB volume attenuation being applied

        /// <summary>
        /// Constructs a new audio gate. The purpose of a gate is to completely silence audio signals during periods of faint noise, but allow non-noise signals to pass through.
        /// It's intended to clean up things like voice signals with faint mic hiss and intermittent pauses.
        /// </summary>
        /// <param name="graph">The audio graph that this component will be a part of</param>
        /// <param name="format">The format of the audio to be processed. The gate applies to all channels of the input indiscriminately.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="gateThresholdRmsDba">The gate's threshold, expressed in units of root-mean-square volume measured in decibels. A signal with loudness above this threshold will be passed through.</param>
        /// <param name="lookAhead">The time, in milliseconds, that the gate should look ahead when processing incoming signals.
        /// This is helpful for avoid losing the first part of a transient.
        /// Since this involves buffering, output latency will increase by the amount specified here.</param>
        /// <param name="attack">The speed at which the gate reacts to peaks, interpreted as the time it takes to go from zero to full compression. Default 20ms</param>
        /// <param name="sustain">The minimum duration that compression is sustained after a peak. Default 500ms</param>
        /// <param name="release">The duration of the transition back to neutral attenuation after compression has been sustained. Default 1000ms</param>
        public AudioGate(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            float gateThresholdRmsDba = -40.0f,
            TimeSpan? lookAhead = null,
            TimeSpan? attack = null,
            TimeSpan? sustain = null,
            TimeSpan? release = null) : base(graph, nameof(AudioGate), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            _gateThresholdDba = gateThresholdRmsDba;
            TimeSpan actualAttack = attack.GetValueOrDefault(DEFAULT_ATTACK);
            TimeSpan actualSustain = sustain.GetValueOrDefault(DEFAULT_SUSTAIN);
            TimeSpan actualRelease = release.GetValueOrDefault(DEFAULT_RELEASE);
            TimeSpan actualLookahead = lookAhead.GetValueOrDefault(TimeSpan.Zero);
            
            if (actualAttack.Ticks < 0)
            {
                throw new ArgumentOutOfRangeException("Attack cannot be negative duration");
            }
            if (actualSustain.Ticks < 0)
            {
                throw new ArgumentOutOfRangeException("Sustain cannot be negative duration");
            }
            if (actualRelease.Ticks < 0)
            {
                throw new ArgumentOutOfRangeException("Release cannot be negative duration");
            }
            if (actualLookahead.Ticks < 0)
            {
                throw new ArgumentOutOfRangeException("Lookahead cannot be negative duration");
            }

            _attackLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, actualAttack);
            _sustainLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, actualSustain);
            _releaseLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, actualRelease);
            _samplesBetweenRmsUpdates = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, RMS_UPDATE_INTERVAL);
            _samplesBetweenVolumeSlopeUpdates = _samplesBetweenRmsUpdates;

            // Allocate a processing buffer to do the actual work in.
            // When lookahead is disabled, this buffer is just scratch space with length equal to RMS_UPDATE_INTERVAL
            // When lookahead is enabled, there is extra space added on with length == lookahead length (in addition to the base scratch area)
            _lookaheadLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, actualLookahead);
            _processingBufferTotalSizePerChannel = _samplesBetweenRmsUpdates + _lookaheadLengthSamplesPerChannel;
            _processingBufferTotalSizeAllChannels = _processingBufferTotalSizePerChannel * format.NumChannels;
            _processingBuffer = new float[_processingBufferTotalSizeAllChannels];
            _samplesPerChannelOfPreprocessingRemaining = _lookaheadLengthSamplesPerChannel;

            _meanSquareVolumePerChannel = new MovingAverageRmsVolume[format.NumChannels];
            int movingAverageLength = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, RMS_RESPONSE_TIME);
            for (int chan = 0; chan < format.NumChannels; chan++)
            {
                _meanSquareVolumePerChannel[chan] = new MovingAverageRmsVolume(movingAverageLength, 0.0f);
            }
        }

        public override bool PlaybackFinished
        {
            get
            {
                IAudioSampleSource source = Input;
                return source == null ? false : source.PlaybackFinished && _processingBufferTotalSamplesPerChannelStored == 0;
            }
        }

        /// <summary>
        /// Gets the current amount of compression being applied, in units of decibels of amplification (always negative).
        /// </summary>
        public float CurrentVolumeAttenuationDecibels => _currentVolumeDBA;

        public TimeSpan AlgorithmicDelay => AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, _lookaheadLengthSamplesPerChannel);

        protected override async ValueTask FlushAsyncInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Preprocess if we are still in the phase (this can lead to rough output but it's better than nothing)
            if (_samplesPerChannelOfPreprocessingRemaining > 0)
            {
                // Preprocess beginning of signal if we are in preprocessing
                int samplesPerChannelCanPreprocess = FastMath.Min(_samplesPerChannelOfPreprocessingRemaining, _processingBufferTotalSamplesPerChannelStored);
                ProcessOutputSamples(samplesPerChannelCanPreprocess, true);
                _samplesPerChannelOfPreprocessingRemaining -= samplesPerChannelCanPreprocess;
            }

            // Write the entire processing buffer to the output
            int segmentOneLength = FastMath.Min(_processingBufferTotalSamplesPerChannelStored, _processingBufferTotalSizePerChannel - _processingBufferReadCursorPerChannel);
            if (segmentOneLength > 0)
            {
                ProcessOutputSamples(segmentOneLength, false);
                await Output.WriteAsync(_processingBuffer, _processingBufferReadCursorPerChannel * InputFormat.NumChannels, segmentOneLength, cancelToken, realTime).ConfigureAwait(false);
                _processingBufferTotalSamplesPerChannelStored -= segmentOneLength;
                _processingBufferReadCursorPerChannel = 0;
                if (_processingBufferTotalSamplesPerChannelStored > 0)
                {
                    ProcessOutputSamples(_processingBufferTotalSamplesPerChannelStored, false);
                    await Output.WriteAsync(_processingBuffer, _processingBufferReadCursorPerChannel * InputFormat.NumChannels, _processingBufferTotalSamplesPerChannelStored, cancelToken, realTime).ConfigureAwait(false);
                }
            }

            // Set all cursors to zero so the buffer is entirely restarted
            _processingBufferReadCursorPerChannel = 0;
            _processingBufferWriteCursorPerChannel = 0;
            _processingBufferTotalSamplesPerChannelStored = 0;
        }

        /// <summary>
        /// Updates the moving mean-square average values using data in the specified range of the processing buffer
        /// </summary>
        /// <param name="offset">Processing buffer array offset (per-channel offset)</param>
        /// <param name="count">Number of samples per channel to update</param>
        private void UpdateAverages(int offset, int count)
        {
            for (int channel = 0; channel < InputFormat.NumChannels; channel++)
            {
                MovingAverageRmsVolume avg = _meanSquareVolumePerChannel[channel];
                int idx = (offset * InputFormat.NumChannels) + channel;
                float value = 0;
                for (int sample = 0; sample < count; sample++)
                {
                    value = _processingBuffer[idx];
                    avg.Add(value);
                    idx += InputFormat.NumChannels;
                }
            }
        }

        /// <summary>
        /// Calculates the current RMS per-channel (based on moving averages from UpdateAverages()) and if a peak is found, puts the gate into attack state.
        /// </summary>
        private void CheckForPeaks(int samplesElapsed)
        {
            _samplesSinceRmsUpdate += samplesElapsed;
            while (_samplesSinceRmsUpdate >= _samplesBetweenRmsUpdates)
            {
                float maxRmsVolume = 0;
                foreach (MovingAverageRmsVolume avg in _meanSquareVolumePerChannel)
                {
                    float rms = avg.RmsVolume;
                    if (rms > maxRmsVolume)
                    {
                        maxRmsVolume = rms;
                    }
                }
                
                if (_currentState != CompressorState.Attack &&
                    AudioMath.LinearToDecibels(maxRmsVolume) > _gateThresholdDba)
                {
                    // Hit a new peak.
                    // Set the new target volume and slope, such that we reach the target volume after the attack period has elapsed.
                    _targetCompressionVolumeDba = 0.0f;
                    _samplesSinceLastStateChange = 0;
                    _samplesSinceLastVolumeSlopeUpdate = 0;
                    _currentState = CompressorState.Attack;
                    _currentFadeSlopeDba = (_targetCompressionVolumeDba - _currentVolumeDBA) / (float)_attackLengthSamplesPerChannel;

                    int samplesInFirstFadeSegment = FastMath.Min(_attackLengthSamplesPerChannel, _samplesBetweenVolumeSlopeUpdates);
                    float newLinearVolumeTarget = AudioMath.DecibelsToLinear(_currentVolumeDBA + (_currentFadeSlopeDba * samplesInFirstFadeSegment));
                    _currentFadeSlopeLinear = (newLinearVolumeTarget - _currentVolumeLinear) / (float)samplesInFirstFadeSegment;
                    _samplesSinceLastVolumeSlopeUpdate = 0;
                }

                _samplesSinceRmsUpdate -= _samplesBetweenRmsUpdates;
            }
        }

        private void ProcessOutputSamples(int samplesToProcess, bool lookaheadOnly)
        {
            int cursor = lookaheadOnly ? -1 : _processingBufferReadCursorPerChannel * InputFormat.NumChannels;
            if (_currentState == CompressorState.Neutral)
            {
                if (cursor >= 0)
                {
                    for (int sample = 0; sample < samplesToProcess; sample++)
                    {
                        // Write silence samples if we are in neutral state.
                        for (int chan = 0; chan < InputFormat.NumChannels; chan++)
                        {
                            _processingBuffer[cursor++] = 0.0f;
                        }
                        if (cursor >= _processingBufferTotalSizeAllChannels)
                        {
                            cursor = 0;
                        }
                    }
                }
            }
            else
            {
                if (_currentState == CompressorState.Attack && samplesToProcess > 0)
                {
                    int attackLength = FastMath.Min(samplesToProcess, _attackLengthSamplesPerChannel - _samplesSinceLastStateChange);
                    ProcessAttackPhase(ref cursor, attackLength);
                    samplesToProcess -= attackLength;
                }
                if (_currentState == CompressorState.Sustain && samplesToProcess > 0)
                {
                    int sustainLength = FastMath.Min(samplesToProcess, _sustainLengthSamplesPerChannel - _samplesSinceLastStateChange);
                    ProcessSustainPhase(ref cursor, sustainLength);
                    samplesToProcess -= sustainLength;
                }
                if (_currentState == CompressorState.Release && samplesToProcess > 0)
                {
                    int releaseLength = FastMath.Min(samplesToProcess, _releaseLengthSamplesPerChannel - _samplesSinceLastStateChange);
                    ProcessReleasePhase(ref cursor, releaseLength);
                    samplesToProcess -= releaseLength;
                }
                if (_currentState == CompressorState.Neutral && samplesToProcess > 0)
                {
                    if (cursor >= 0)
                    {
                        for (int sample = 0; sample < samplesToProcess; sample++)
                        {
                            // If we just transitioned from release to neutral, make sure we silence the last portion of this read block
                            for (int chan = 0; chan < InputFormat.NumChannels; chan++)
                            {
                                _processingBuffer[cursor++] = 0.0f;
                            }
                            if (cursor >= _processingBufferTotalSizeAllChannels)
                            {
                                cursor = 0;
                            }
                        }
                    }
                }
            }
        }

        private void ProcessAttackPhase(ref int cursor, int samplesToProcess)
        {
            for (int sample = 0; sample < samplesToProcess; sample++)
            {
                // OPT these for loops are ineffecient - wish they could be ordered the other way around
                if (cursor >= 0) // Don't actually update samples if we are only preprocessing
                {
                    for (int chan = 0; chan < InputFormat.NumChannels; chan++)
                    {
                        _processingBuffer[cursor++] *= _currentVolumeLinear;
                    }
                }

                _currentVolumeLinear += _currentFadeSlopeLinear;
                _currentVolumeDBA += _currentFadeSlopeDba;
                if (cursor >= _processingBufferTotalSizeAllChannels)
                {
                    cursor = 0;
                }
                
                if (++_samplesSinceLastVolumeSlopeUpdate >= _samplesBetweenVolumeSlopeUpdates)
                {
                    int samplesOfFadeRemaining = _attackLengthSamplesPerChannel - _samplesSinceLastStateChange;
                    if (samplesOfFadeRemaining > 0)
                    {
                        int samplesInNextSegment = FastMath.Min(samplesOfFadeRemaining, _samplesBetweenVolumeSlopeUpdates);
                        float newLinearVolumeTarget = AudioMath.DecibelsToLinear(_currentVolumeDBA + (_currentFadeSlopeDba * samplesInNextSegment));
                        _currentFadeSlopeLinear = (newLinearVolumeTarget - _currentVolumeLinear) / (float)samplesInNextSegment;
                        _samplesSinceLastVolumeSlopeUpdate = 0;
                    }
                }
            }

            _samplesSinceLastStateChange += samplesToProcess;
            if (_samplesSinceLastStateChange >= _attackLengthSamplesPerChannel)
            {
                // Attack -> Sustain
                _samplesSinceLastStateChange = 0;
                _currentState = CompressorState.Sustain;
                _currentVolumeDBA = 0.0f;
                _currentVolumeLinear = 1.0f;
                _currentFadeSlopeDba = 0;
                _currentFadeSlopeLinear = 0;
                _samplesSinceLastVolumeSlopeUpdate = 0;
            }
        }

        private void ProcessSustainPhase(ref int cursor, int samplesToProcess)
        {
            if (cursor >= 0) // Don't actually update samples if we are only preprocessing
            {
                for (int sample = 0; sample < samplesToProcess; sample++)
                {
                    for (int chan = 0; chan < InputFormat.NumChannels; chan++) // opt: remove this loop?
                    {
                        _processingBuffer[cursor++] *= _currentVolumeLinear;
                    }

                    if (cursor >= _processingBufferTotalSizeAllChannels)
                    {
                        cursor = 0;
                    }
                }
            }

            _samplesSinceLastStateChange += samplesToProcess;
            if (_samplesSinceLastStateChange >= _sustainLengthSamplesPerChannel)
            {
                // Sustain -> Release
                _samplesSinceLastStateChange = 0;
                _currentState = CompressorState.Release;
                _currentVolumeDBA = 0.0f;
                _currentVolumeLinear = 1.0f;
                _targetCompressionVolumeDba = MIN_VOLUME_DBA;
                _currentFadeSlopeDba = MIN_VOLUME_DBA / (float)_releaseLengthSamplesPerChannel;
                _currentFadeSlopeLinear = -1.0f / (float)_releaseLengthSamplesPerChannel;
                _samplesSinceLastVolumeSlopeUpdate = 0;
            }
        }

        private void ProcessReleasePhase(ref int cursor, int samplesToProcess)
        {
            for (int sample = 0; sample < samplesToProcess; sample++)
            {
                if (cursor >= 0) // Don't actually update samples if we are only preprocessing
                {
                    for (int chan = 0; chan < InputFormat.NumChannels; chan++)
                    {
                        _processingBuffer[cursor++] *= _currentVolumeLinear;
                    }
                }

                _currentVolumeLinear += _currentFadeSlopeLinear;
                _currentVolumeDBA += _currentFadeSlopeDba;
                if (cursor >= _processingBufferTotalSizeAllChannels)
                {
                    cursor = 0;
                }

                if (++_samplesSinceLastVolumeSlopeUpdate >= _samplesBetweenVolumeSlopeUpdates)
                {
                    int samplesOfFadeRemaining = _releaseLengthSamplesPerChannel - _samplesSinceLastStateChange;
                    if (samplesOfFadeRemaining > 0)
                    {
                        int samplesInNextSegment = FastMath.Min(samplesOfFadeRemaining, _samplesBetweenVolumeSlopeUpdates);
                        float newLinearVolumeTarget = AudioMath.DecibelsToLinear(_currentVolumeDBA + (_currentFadeSlopeDba * samplesInNextSegment));
                        _currentFadeSlopeLinear = (newLinearVolumeTarget - _currentVolumeLinear) / (float)samplesInNextSegment;
                        _samplesSinceLastVolumeSlopeUpdate = 0;
                    }
                }
            }

            _samplesSinceLastStateChange += samplesToProcess;
            if (_samplesSinceLastStateChange >= _releaseLengthSamplesPerChannel)
            {
                // Release -> Neutral
                _samplesSinceLastStateChange = 0;
                _currentState = CompressorState.Neutral;
                _currentVolumeDBA = MIN_VOLUME_DBA;
                _currentVolumeLinear = 0.0f;
                _currentFadeSlopeDba = 0.0f;
                _currentFadeSlopeLinear = 0.0f;
                _samplesSinceLastVolumeSlopeUpdate = 0;
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int totalSamplesPerChannelWrittenToOutput = 0;
            while (totalSamplesPerChannelWrittenToOutput < count)
            {
                int samplesPerChannelActuallyReadFromInput;
                if (Input.PlaybackFinished)
                {
                    samplesPerChannelActuallyReadFromInput = -1;
                }
                else
                {
                    int samplesPerChannelUntilNextRmsUpdate = _samplesBetweenRmsUpdates - _samplesSinceRmsUpdate;
                    int samplesPerChannelUntilBufferIsFull = _processingBufferTotalSizePerChannel - _processingBufferTotalSamplesPerChannelStored;
                    int samplesPerChannelUntilEndOfWriteBuffer = _processingBufferTotalSizePerChannel - _processingBufferWriteCursorPerChannel;
                    int samplesPerChannelCanReadFromInput = FastMath.Min(FastMath.Min(samplesPerChannelUntilEndOfWriteBuffer, samplesPerChannelUntilNextRmsUpdate), samplesPerChannelUntilBufferIsFull);
                    samplesPerChannelActuallyReadFromInput = await Input.ReadAsync(
                        _processingBuffer,
                        _processingBufferWriteCursorPerChannel * InputFormat.NumChannels,
                        samplesPerChannelCanReadFromInput,
                        cancelToken,
                        realTime).ConfigureAwait(false);
                }

                int samplesPerChannelCanProcessNow;
                if (samplesPerChannelActuallyReadFromInput < 0)
                {
                    // Input playback has finished. Mark the entire buffer as eligible for processing.
                    samplesPerChannelCanProcessNow = _processingBufferTotalSamplesPerChannelStored;
                }
                else if (samplesPerChannelActuallyReadFromInput == 0)
                {
                    // Input is temporarily exhausted, so just return what we have
                    return totalSamplesPerChannelWrittenToOutput;
                }
                else
                {
                    UpdateAverages(_processingBufferWriteCursorPerChannel, samplesPerChannelActuallyReadFromInput);
                    _processingBufferTotalSamplesPerChannelStored += samplesPerChannelActuallyReadFromInput;
                    _processingBufferWriteCursorPerChannel = (_processingBufferWriteCursorPerChannel + samplesPerChannelActuallyReadFromInput) % _processingBufferTotalSizePerChannel;

                    // Check for peaks intermittently (every few ms)
                    CheckForPeaks(samplesPerChannelActuallyReadFromInput);
                    samplesPerChannelCanProcessNow = _processingBufferTotalSamplesPerChannelStored - _lookaheadLengthSamplesPerChannel;
                }

                int samplesPerChannelStillRequestedByCaller = count - totalSamplesPerChannelWrittenToOutput;
                int samplesPerChannelUntilEndOfReadBuffer = _processingBufferTotalSizePerChannel - _processingBufferReadCursorPerChannel;
                int samplesPerChannelToWriteToOutput = FastMath.Min(FastMath.Min(samplesPerChannelUntilEndOfReadBuffer, samplesPerChannelStillRequestedByCaller), samplesPerChannelCanProcessNow);

                bool stillPreprocessing = false;
                if (_samplesPerChannelOfPreprocessingRemaining > 0)
                {
                    // Preprocess beginning of signal if we are in preprocessing
                    stillPreprocessing = true;
                    int samplesPerChannelCanPreprocess = FastMath.Min(_samplesPerChannelOfPreprocessingRemaining, samplesPerChannelActuallyReadFromInput);
                    ProcessOutputSamples(samplesPerChannelCanPreprocess, true);
                    _samplesPerChannelOfPreprocessingRemaining -= samplesPerChannelCanPreprocess;
                }

                if (samplesPerChannelToWriteToOutput > 0)
                {
                    ProcessOutputSamples(samplesPerChannelToWriteToOutput, false);

                    ArrayExtensions.MemCopy(
                        _processingBuffer,
                        _processingBufferReadCursorPerChannel * InputFormat.NumChannels,
                        buffer,
                        (offset + (totalSamplesPerChannelWrittenToOutput * InputFormat.NumChannels)),
                        samplesPerChannelToWriteToOutput * InputFormat.NumChannels);

                    totalSamplesPerChannelWrittenToOutput += samplesPerChannelToWriteToOutput;
                    _processingBufferTotalSamplesPerChannelStored -= samplesPerChannelToWriteToOutput;
                    _processingBufferReadCursorPerChannel = (_processingBufferReadCursorPerChannel + samplesPerChannelToWriteToOutput) % _processingBufferTotalSizePerChannel;
                }
                else
                {
                    // If this is still the very beginning of the signal and we are preprocessing, make sure we don't
                    // return -1 for end of stream
                    if (stillPreprocessing)
                    {
                        return 0;
                    }
                    else
                    {
                        // End of input and there's nothing left in the buffer.
                        return totalSamplesPerChannelWrittenToOutput == 0 ? -1 : totalSamplesPerChannelWrittenToOutput;
                    }
                }
            }

            return totalSamplesPerChannelWrittenToOutput;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int totalSamplesPerChannelReadFromInput = 0;
            while (totalSamplesPerChannelReadFromInput < count)
            {
                int samplesPerChannelUntilNextRmsUpdate = _samplesBetweenRmsUpdates - _samplesSinceRmsUpdate;
                int samplesPerChannelUntilBufferIsFull = _processingBufferTotalSizePerChannel - _processingBufferTotalSamplesPerChannelStored;
                int samplesPerChannelUntilEndOfWriteBuffer = _processingBufferTotalSizePerChannel - _processingBufferWriteCursorPerChannel;
                int samplesPerChannelUntilInputIsExhausted = count - totalSamplesPerChannelReadFromInput;
                int samplesPerChanneToReadFromInput =
                    FastMath.Min(
                        FastMath.Min(
                            FastMath.Min(
                                samplesPerChannelUntilInputIsExhausted,
                                samplesPerChannelUntilEndOfWriteBuffer),
                            samplesPerChannelUntilNextRmsUpdate),
                        samplesPerChannelUntilBufferIsFull);

                ArrayExtensions.MemCopy(
                    buffer,
                    (offset + (totalSamplesPerChannelReadFromInput * InputFormat.NumChannels)),
                    _processingBuffer,
                    _processingBufferWriteCursorPerChannel * InputFormat.NumChannels,
                    samplesPerChanneToReadFromInput * InputFormat.NumChannels);

                totalSamplesPerChannelReadFromInput += samplesPerChanneToReadFromInput;
                UpdateAverages(_processingBufferWriteCursorPerChannel, samplesPerChanneToReadFromInput);
                _processingBufferTotalSamplesPerChannelStored += samplesPerChanneToReadFromInput;
                _processingBufferWriteCursorPerChannel = (_processingBufferWriteCursorPerChannel + samplesPerChanneToReadFromInput) % _processingBufferTotalSizePerChannel;

                // Check for peaks intermittently (every few ms)
                CheckForPeaks(samplesPerChanneToReadFromInput);
                int samplesPerChannelCanProcessNow = _processingBufferTotalSamplesPerChannelStored - _lookaheadLengthSamplesPerChannel;

                int samplesPerChannelUntilEndOfReadBuffer = _processingBufferTotalSizePerChannel - _processingBufferReadCursorPerChannel;
                int samplesPerChannelToWriteToOutput = FastMath.Min(samplesPerChannelUntilEndOfReadBuffer, samplesPerChannelCanProcessNow);

                if (_samplesPerChannelOfPreprocessingRemaining > 0)
                {
                    // Preprocess beginning of signal if we are in preprocessing
                    int samplesPerChannelCanPreprocess = FastMath.Min(_samplesPerChannelOfPreprocessingRemaining, samplesPerChanneToReadFromInput);
                    ProcessOutputSamples(samplesPerChannelCanPreprocess, true);
                    _samplesPerChannelOfPreprocessingRemaining -= samplesPerChannelCanPreprocess;
                }

                if (samplesPerChannelToWriteToOutput > 0)
                {
                    ProcessOutputSamples(samplesPerChannelToWriteToOutput, false);

                    await Output.WriteAsync(
                        _processingBuffer,
                        _processingBufferReadCursorPerChannel * InputFormat.NumChannels,
                        samplesPerChannelToWriteToOutput,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    _processingBufferTotalSamplesPerChannelStored -= samplesPerChannelToWriteToOutput;
                    _processingBufferReadCursorPerChannel = (_processingBufferReadCursorPerChannel + samplesPerChannelToWriteToOutput) % _processingBufferTotalSizePerChannel;
                }
            }
        }
    }
}
