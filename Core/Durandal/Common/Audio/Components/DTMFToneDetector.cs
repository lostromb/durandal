using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Audio.Dsp;
using Durandal.Common.Events;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio filter which detects DTMF tones (dial tones) and fires events when tones are detected.
    /// </summary>
    public sealed class DTMFToneDetector : AbstractAudioSampleFilter
    {
        /// <summary>
        /// The array of tone detectors. This either has one detector for each channel of input, or just a single detector if the
        /// caller does not care about channels.
        /// </summary>
        private readonly SingleChannelToneDetector[] _detectors;

        /// <summary>
        /// A logger mostly for development.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// This event gets fired whenever a dial tone is detected in the audio stream.
        /// </summary>
        public AsyncEvent<DialToneEventArgs> ToneDetectedEvent
        {
            get; private set;
        }

        /// <summary>
        /// Constructs a new <see cref="DTMFToneDetector"/>.
        /// </summary>
        /// <param name="graph">The audio graph this component will be part of.</param>
        /// <param name="format">The format of the audio to be processed.</param>
        /// <param name="logger">A logger for debugging</param>
        /// <param name="nodeCustomName">A name for this audio component, or null.</param>
        /// <param name="processEachChannel">If true, set up a detector for each individual channel of the input. Otherwise, only analyze channel 0.</param>
        public DTMFToneDetector(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, ILogger logger, string nodeCustomName = null, bool processEachChannel = false)
            : base(graph, nameof(DTMFToneDetector), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            ToneDetectedEvent = new AsyncEvent<DialToneEventArgs>();
            if (processEachChannel)
            {
                _detectors = new SingleChannelToneDetector[format.NumChannels];
            }
            else
            {
                _detectors = new SingleChannelToneDetector[1];
            }

            _logger = logger;
            for (int channel = 0; channel < _detectors.Length; channel++)
            {
                _detectors[channel] = new SingleChannelToneDetector(format.SampleRateHz, logger);
                _detectors[channel].ToneDetectedEvent.Subscribe(ToneDetectedEvent.Fire);
            }
        }

        /// <inheritdoc />
        protected override async ValueTask<int> ReadAsyncInternal(float[] targetBuffer, int offset, int samplesPerChannelToRead, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelReadFromSource = await Input.ReadAsync(targetBuffer, offset, samplesPerChannelToRead, cancelToken, realTime).ConfigureAwait(false);
            if (samplesPerChannelReadFromSource > 0)
            {
                for (int channel = 0; channel < _detectors.Length; channel++)
                {
                    await _detectors[channel].Process(
                        targetBuffer,
                        offset,
                        samplesPerChannelReadFromSource,
                        channel,
                        InputFormat.NumChannels,
                        realTime).ConfigureAwait(false);
                }
            }

            return samplesPerChannelReadFromSource;
        }

        /// <inheritdoc />
        protected override async ValueTask WriteAsyncInternal(float[] sourceBuffer, int offset, int samplesPerChannelToWrite, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            for (int channel = 0; channel < _detectors.Length; channel++)
            {
                await _detectors[channel].Process(
                    sourceBuffer,
                    offset,
                    samplesPerChannelToWrite,
                    channel,
                    InputFormat.NumChannels,
                    realTime).ConfigureAwait(false);
            }

            await Output.WriteAsync(sourceBuffer, offset, samplesPerChannelToWrite, cancelToken, realTime).ConfigureAwait(false);
        }

        /// <summary>
        /// Handles the dial tone detection for a single audio channel.
        /// </summary>
        private class SingleChannelToneDetector
        {
            private const float DETECTION_THRESHOLD = 0.8f; // Response divided by Energy must exceed this value for 100ms to count as a tone
            private static readonly TimeSpan ACCUMULATION_INCREMENT = TimeSpan.FromMilliseconds(25);
            private static readonly TimeSpan MIN_TONE_LENGTH = TimeSpan.FromMilliseconds(100);
            private readonly GoertzelArray _filter;
            private readonly int _samplesToAccumulate;
            private readonly ILogger _logger;
            private readonly float[] _normalizedR;
            private int _samplesAccumulated;
            private DialTone _currentlyDetectedTone;
            private TimeSpan _lengthOfCurrentDetection;
            private bool _hasFiredEventForThisTone;

            public SingleChannelToneDetector(int inputSampleRate, ILogger logger)
            {
                _samplesToAccumulate = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(inputSampleRate, ACCUMULATION_INCREMENT);
                _filter = new GoertzelArray(_samplesToAccumulate, inputSampleRate);
                // LF
                _filter.AddFilter(697);
                _filter.AddFilter(770);
                _filter.AddFilter(852);
                _filter.AddFilter(941);
                // HF
                _filter.AddFilter(1209);
                _filter.AddFilter(1336);
                _filter.AddFilter(1477);
                _filter.AddFilter(1633);
                _samplesAccumulated = 0;
                _logger = logger;
                _currentlyDetectedTone = DialTone.SILENCE;
                _lengthOfCurrentDetection = TimeSpan.Zero;
                _hasFiredEventForThisTone = false;
                _normalizedR = new float[_filter.NumFilters];
                ToneDetectedEvent = new AsyncEvent<DialToneEventArgs>();
            }

            public AsyncEvent<DialToneEventArgs> ToneDetectedEvent
            {
                get; private set;
            }

            public async ValueTask Process(float[] inputSamples, int inputSamplesOffset, int samplesPerChannel, int channelIdx, int channelStride, IRealTimeProvider realTime)
            {
                if (samplesPerChannel == 0)
                {
                    return;
                }

                int sampleIdx = inputSamplesOffset + channelIdx;
                int samplesProcessed = 0;
                while (samplesProcessed < samplesPerChannel)
                {
                    _filter.AddSample(inputSamples[sampleIdx]);
                    _samplesAccumulated++;

                    if (_samplesAccumulated == _samplesToAccumulate)
                    {
                        // Evaluate current tones
                        _filter.Response.CopyTo(_normalizedR.AsSpan());
                        float E = Math.Max(1, _filter.SignalEnergy);

                        // Normalize responses
                        for (int c = 0; c < _normalizedR.Length; c++)
                        {
                            // divide by energy to account for volume
                            // dividing by accumulator size accounts for different sample rates
                            _normalizedR[c] = (_normalizedR[c] / E) * 10.0f / (float)_filter.AccumulatorSizeSamples;
                        }

                        //if (channelIdx == 0)
                        //{
                        //    using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
                        //    {
                        //        StringBuilder s = new StringBuilder();
                        //        s.AppendFormat("E: {0:F1} R: ", E);
                        //        foreach (float r in normalizedR)
                        //        {
                        //            s.AppendFormat("{0:F1} / ", r);
                        //        }

                        //        _logger.Log(s.ToString());
                        //    }
                        //}

                        float sumLow = _normalizedR[0] + _normalizedR[1] + _normalizedR[2] + _normalizedR[3];
                        float sumHigh = _normalizedR[4] + _normalizedR[5] + _normalizedR[6] + _normalizedR[7];

                        DialTone currentTone = DialTone.SILENCE;
                        if (sumLow > DETECTION_THRESHOLD && sumHigh > DETECTION_THRESHOLD)
                        {
                            // We may have something. Make sure only one frequency is dominant in each band
                            float uniqueThreshLow = sumLow * 0.75f;
                            float uniqueThreshHigh = sumHigh * 0.75f;
                            if ((_normalizedR[0] > uniqueThreshLow ||
                                _normalizedR[1] > uniqueThreshLow ||
                                _normalizedR[2] > uniqueThreshLow ||
                                _normalizedR[3] > uniqueThreshLow) &&
                                (_normalizedR[4] > uniqueThreshHigh ||
                                _normalizedR[5] > uniqueThreshHigh ||
                                _normalizedR[6] > uniqueThreshHigh ||
                                _normalizedR[7] > uniqueThreshHigh))
                            {
                                // Find the corresponding tone
                                if (_normalizedR[0] > DETECTION_THRESHOLD)
                                {
                                    if (_normalizedR[4] > DETECTION_THRESHOLD)      currentTone = DialTone.TONE_1;
                                    else if (_normalizedR[5] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_2;
                                    else if (_normalizedR[6] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_3;
                                    else if (_normalizedR[7] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_A;
                                }
                                else if (_normalizedR[1] > DETECTION_THRESHOLD)
                                {
                                    if (_normalizedR[4] > DETECTION_THRESHOLD)      currentTone = DialTone.TONE_4;
                                    else if (_normalizedR[5] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_5;
                                    else if (_normalizedR[6] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_6;
                                    else if (_normalizedR[7] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_B;
                                }
                                else if (_normalizedR[2] > DETECTION_THRESHOLD)
                                {
                                    if (_normalizedR[4] > DETECTION_THRESHOLD)      currentTone = DialTone.TONE_7;
                                    else if (_normalizedR[5] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_8;
                                    else if (_normalizedR[6] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_9;
                                    else if (_normalizedR[7] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_C;
                                }
                                else if (_normalizedR[3] > DETECTION_THRESHOLD)
                                {
                                    if (_normalizedR[4] > DETECTION_THRESHOLD)      currentTone = DialTone.TONE_STAR;
                                    else if (_normalizedR[5] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_0;
                                    else if (_normalizedR[6] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_POUND;
                                    else if (_normalizedR[7] > DETECTION_THRESHOLD) currentTone = DialTone.TONE_D;
                                }
                            }
                        }

                        // New tone found (or possibly silence). Transition to new detection
                        if (currentTone != _currentlyDetectedTone)
                        {
                            _lengthOfCurrentDetection = TimeSpan.Zero;
                            _currentlyDetectedTone = currentTone;
                            _hasFiredEventForThisTone = false;
                        }

                        _lengthOfCurrentDetection += ACCUMULATION_INCREMENT;
                        if (_currentlyDetectedTone != DialTone.SILENCE &&
                            !_hasFiredEventForThisTone &&
                            _lengthOfCurrentDetection >= MIN_TONE_LENGTH)
                        {
                            // Have to fire the event synchronously so processors that
                            // are working in non-real-time will get the tone events in a deterministic order
                            await ToneDetectedEvent.Fire(
                                this,
                                new DialToneEventArgs()
                                {
                                    ChannelIdx = channelIdx,
                                    Tone = _currentlyDetectedTone,
                                },
                                realTime).ConfigureAwait(false);
                            _hasFiredEventForThisTone = true;
                        }

                        _samplesAccumulated = 0;
                        _filter.Reset();
                    }

                    samplesProcessed++;
                    sampleIdx += channelStride;
                }
            }
        }
    }
}
