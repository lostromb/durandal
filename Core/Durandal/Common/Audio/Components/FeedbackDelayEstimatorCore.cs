using Durandal.Common.Collections;
using Durandal.Common.Compression.Zip;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Statistics;
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
    public sealed class FeedbackDelayEstimatorCore : AbstractAudioSampleFilter
    {
        private readonly int _peekScratchBufferLengthSamplesPerChannel;
        private readonly float[] _peekScratchBuffer;
        private readonly int _speakerBufferLengthSamples;
        private readonly float[] _speakerBufferMono;
        private readonly int _micBufferLengthSamples;
        private readonly float[] _micBufferMono;
        private readonly WeakPointer<AudioPeekBuffer> _peekBuffer;
        private readonly FastRandom _rand;
        private readonly ILogger _debugLogger;
        private readonly int _maxObservableDelaySamples; // maximum samples of delay that this filter can process
        private long _micSampleCounterPerChannel; // absolute timestamp of mic channel
        private long _speakerSampleCounterPerChannel; // absolute timestamp of speaker channel
        private long _speakerSampleCounterOfLastCorrelation; // used to measure how much time has passed since the last correlation step
        private float[] _xCorrLikelihood; // running average correlations of all frequencies in the scanning space
        private float _xCorrCenterOfMass; // Median point of all cross correlations. Corresponds to the number of samples of detected delay, in the microphone's sample rate
        private int _xCorrHighestCorrelationIdx; // Index of the strongest cross correlation, very similar to xCorrCenterOfMass
        private float _xCorrDeviation; // Standard deviation of the mass of cross correlations. Higher number = wider distribution of likely correlations, therefore less certainty
        private float _xCorrHighestCorrelation; // The highest measured cross correlation. Determines how certain the hypothesis is at the center of mass
        private float _correlationConfidence; // Current confidence of estimate from 0 to 1
        private int _initialCorrsRemaining; // Used to make sure we scan the full frequency space before trying to cut down and optimize which correlations get performed
        private MovingAverageFloat _micDampingFilter; // damping filters to smooth out mic and speaker signals
        private MovingAverageFloat _speakerDampingFilter;
        private const long DAMPING_FILTER_HZ = 2000; // damping filter frequency. lower = more smoothing applied

        public FeedbackDelayEstimatorCore(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            TimeSpan matchPatternLength,
            TimeSpan maxEstimatedDelay,
            WeakPointer<AudioPeekBuffer> peekBuffer,
            ILogger logger = null) : base(graph, nameof(FeedbackDelayEstimatorCore), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;

            _peekBuffer = peekBuffer.AssertNonNull(nameof(peekBuffer));

            if (format.NumChannels != 1)
            {
                throw new ArgumentException("Delay estimation can only be performed on mono input/output signals");
            }

            if (peekBuffer.Value.OutputFormat.NumChannels != 1)
            {
                throw new ArgumentException("Delay estimation requires a monaural peek buffer");
            }

            if (matchPatternLength <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(matchPatternLength));
            }

            if (peekBuffer.Value.PeekBufferLength < maxEstimatedDelay + matchPatternLength)
            {
                throw new ArgumentException("Predicting feedback delays up to " + maxEstimatedDelay.TotalMilliseconds + "ms requires a peek buffer with length of at least " + (maxEstimatedDelay + matchPatternLength).TotalMilliseconds + "ms");
            }

            _peekScratchBufferLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, peekBuffer.Value.PeekBufferLength);
            _micBufferLengthSamples = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, matchPatternLength);
            _speakerBufferLengthSamples = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, maxEstimatedDelay + matchPatternLength);
            _maxObservableDelaySamples = _speakerBufferLengthSamples - _micBufferLengthSamples;

            _speakerBufferMono = new float[_speakerBufferLengthSamples];
            _peekScratchBuffer = new float[_peekScratchBufferLengthSamplesPerChannel];
            _micBufferMono = new float[_micBufferLengthSamples];
            _micSampleCounterPerChannel = 0 - _micBufferLengthSamples;
            _speakerSampleCounterPerChannel = 0 - _peekScratchBufferLengthSamplesPerChannel;
            _xCorrLikelihood = new float[_maxObservableDelaySamples];
            _rand = new FastRandom();
            _debugLogger = logger;
            _initialCorrsRemaining = _maxObservableDelaySamples;
            int dampingFilterLength = Math.Max(1, (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, TimeSpan.FromTicks(TimeSpan.TicksPerSecond / (long)DAMPING_FILTER_HZ)));
            _micDampingFilter = new MovingAverageFloat(dampingFilterLength, 0);
            _speakerDampingFilter = new MovingAverageFloat(dampingFilterLength, 0);
        }

        public Hypothesis<TimeSpan> GetEstimatedDelay()
        {
            if (_xCorrDeviation == 0 || _xCorrHighestCorrelation == 0)
            {
                return new Hypothesis<TimeSpan>(TimeSpan.Zero, 0.0f);
            }

            // If the deviation of the set is low, use highest correlation index. If deviation is high,
            // use center of mass.
            // This is to try and get more reliable behavior if there are multiple sharp peaks or large,
            // vague curves in the correlation vector.
            long delaySamples;
            if (_xCorrDeviation < 10)
            {
                delaySamples = _xCorrHighestCorrelationIdx;
            }
            else
            {
                delaySamples = (long)_xCorrCenterOfMass;
            }

            TimeSpan estimatedDelay = AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, delaySamples);
            return new Hypothesis<TimeSpan>(estimatedDelay, _correlationConfidence);
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int readSize = await Input.ReadAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);

            if (readSize > 0)
            {
                // Shift mic buffer left
                int samplesToKeepInBuffer = FastMath.Max(0, _micBufferLengthSamples - readSize);
                if (samplesToKeepInBuffer > 0)
                {
                    ArrayExtensions.MemCopy(
                        _micBufferMono,
                        (_micBufferLengthSamples - samplesToKeepInBuffer),
                        _micBufferMono,
                        0,
                        samplesToKeepInBuffer);
                }

                // Write new data
                int samplesPerChannelToCopyFromInput = FastMath.Min(readSize, _micBufferLengthSamples);

                // Mono audio is guaranteed, so just copy straight across
                CopyAudioSignalUsingDampingFilter(
                    _micDampingFilter,
                    buffer,
                    (offset + (readSize - samplesPerChannelToCopyFromInput)),
                    _micBufferMono,
                    samplesToKeepInBuffer,
                    samplesPerChannelToCopyFromInput);

                _micSampleCounterPerChannel += readSize;
                Correlate();
            }

            return readSize;
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (count > 0)
            {
                // Shift mic buffer left
                int samplesToKeepInBuffer = FastMath.Max(0, _micBufferLengthSamples - count);
                if (samplesToKeepInBuffer > 0)
                {
                    ArrayExtensions.MemCopy(
                        _micBufferMono,
                        (_micBufferLengthSamples - samplesToKeepInBuffer),
                        _micBufferMono,
                        0,
                        samplesToKeepInBuffer);
                }

                // Write new data
                int samplesPerChannelToCopyFromInput = FastMath.Min(count, _micBufferLengthSamples);

                CopyAudioSignalUsingDampingFilter(
                    _micDampingFilter,
                    buffer,
                    (offset + (count - samplesPerChannelToCopyFromInput)),
                    _micBufferMono,
                    samplesToKeepInBuffer,
                    samplesPerChannelToCopyFromInput);

                _micSampleCounterPerChannel += count;
                Correlate();
            }

            return Output.WriteAsync(buffer, offset, count, cancelToken, realTime);
        }

        private static void CopyAudioSignalUsingDampingFilter(
            MovingAverageFloat dampingFilter,
            float[] input,
            int inputOffsetSamples,
            float[] output,
            int outputOffsetSamples,
            int samplesToCopy)
        {
            for (int sample = 0; sample < samplesToCopy; sample++)
            {
                dampingFilter.Add(input[inputOffsetSamples + sample]);
                output[outputOffsetSamples + sample] = dampingFilter.Average;
            }
        }

        private string PrintHistogram(int charWidth, bool useUnicode)
        {
            TinyHistogram hist = new TinyHistogram();
            for (int c = 0; c < _maxObservableDelaySamples; c++)
            {
                hist.AddValue(c, _xCorrLikelihood[c]);
            }

            return hist.RenderAsOneLine(charWidth, useUnicode);
        }

        private void Correlate()
        {
            // Read data from peek buffer
            int actualPeekLengthSamplesPerChannel;
            long peekBufferStartTimestamp;
            _peekBuffer.Value.PeekAtBuffer(_peekScratchBuffer, 0, _peekScratchBufferLengthSamplesPerChannel, out actualPeekLengthSamplesPerChannel, out peekBufferStartTimestamp);
            
            if (actualPeekLengthSamplesPerChannel <= 0)
            {
                // No data returned from peek. Should only be possible if we've messed up our buffer lengths somewhere, so throw an exception
                throw new ArgumentOutOfRangeException("Peek data should never have zero length; something is wrong with your buffer sizes");
            }

            // Calculate peek buffer bounds
            long peekBufferValidDataStartTimestamp = Math.Max(_micSampleCounterPerChannel - _maxObservableDelaySamples, peekBufferStartTimestamp);
            long peekBufferValidDataEndTimestamp = Math.Min(peekBufferStartTimestamp + actualPeekLengthSamplesPerChannel, _micSampleCounterPerChannel + _micBufferLengthSamples);
            int usablePeekBufferSamples = (int)(peekBufferValidDataEndTimestamp - peekBufferValidDataStartTimestamp);
            int samplesToDiscardFromStartOfPeekBuffer = FastMath.Max(0, (int)(peekBufferValidDataStartTimestamp - peekBufferStartTimestamp));

            // If speaker data hasn't changed since last check, do nothing
            if (usablePeekBufferSamples > _micBufferLengthSamples && _speakerSampleCounterPerChannel != peekBufferValidDataStartTimestamp)
            {
                _speakerSampleCounterPerChannel = peekBufferValidDataStartTimestamp;

                int speakerValidDataStartIndex = (int)(peekBufferValidDataStartTimestamp - _micSampleCounterPerChannel) + _maxObservableDelaySamples;
                int speakerValidDataEndIndex = speakerValidDataStartIndex + usablePeekBufferSamples;

                // Don't assume that there is any continuity between what was previously in the peek buffer (the old data in _speakerBufferMono)
                // and the new peek data. So we don't care about any data that was previously peeked, we just overwrite the whole block.
                CopyAudioSignalUsingDampingFilter(
                    _speakerDampingFilter,
                    _peekScratchBuffer,
                    samplesToDiscardFromStartOfPeekBuffer,
                    _speakerBufferMono,
                    speakerValidDataStartIndex,
                    usablePeekBufferSamples);

                // Calculate RMS volume of both signals. Use this to detect likelihood that we will even get a usable correlation
                float speakerMaxAmplitude = GetMaxAmplitude(_speakerBufferMono, speakerValidDataStartIndex, usablePeekBufferSamples);
                float micMaxAmplitude = GetMaxAmplitude(_micBufferMono, 0, _micBufferLengthSamples);

                long samplesSinceLastCorrelation = _speakerSampleCounterPerChannel - _speakerSampleCounterOfLastCorrelation;
                _speakerSampleCounterOfLastCorrelation = _speakerSampleCounterPerChannel;

                // No sound on either one of the channels, so there's nothing we can correlate.
                if (speakerMaxAmplitude < 0.001f || micMaxAmplitude < 0.001f)
                {
                    return;
                }

                // Figure out the range of possible cross correlations and how many we should run
                int xCorrStartIndex = FastMath.Max(0, _maxObservableDelaySamples + _micBufferLengthSamples - speakerValidDataEndIndex);
                int xCorrEndIndex = FastMath.Min(_maxObservableDelaySamples, _maxObservableDelaySamples + _micBufferLengthSamples - speakerValidDataStartIndex);

                // Run fewer correlations when the deviation is low;
                //int comperehensiveXcorrs = _maxObservableDelaySamples * _micBufferLengthSamples;
                int maxXCorrsToRun = (int)(samplesSinceLastCorrelation * 5); // Maximum correlations we can run is capped by the amount of time that's passed since the last check
                int minXCorrsToRun = FastMath.Max(10, maxXCorrsToRun / 40);
                float xCorrRatio = Math.Max(0.0f, Math.Min(1.0f, (1.0f - _correlationConfidence) * speakerMaxAmplitude));
                if (_initialCorrsRemaining > 0)
                {
                    xCorrRatio = 1.0f;
                }

                int desiredXCorrsToRun = FastMath.Max(minXCorrsToRun, FastMath.Min(maxXCorrsToRun, (int)(maxXCorrsToRun * xCorrRatio)));

                // Now run xcorrelations
                float mixWet = 0.1f; Math.Max(0.01f, Math.Min(1.0f, xCorrRatio * 0.1f));
                float mixDry = 1.0f - mixWet;
                for (int correlation = 0; correlation < desiredXCorrsToRun; correlation++)
                {
                    int xCorrSlot = _rand.NextInt(xCorrStartIndex, xCorrEndIndex);
                    float corr = Correlation.NormalizedCrossCorrelationOfAbsoluteVector(
                        _micBufferMono,
                        0,
                        _speakerBufferMono,
                        _maxObservableDelaySamples - xCorrSlot,
                        _micBufferLengthSamples);
                    _xCorrLikelihood[xCorrSlot] = (_xCorrLikelihood[xCorrSlot] * mixDry) + (corr * mixWet);
                }

                CalculateXCorrStatistics();

                if (_initialCorrsRemaining > 0)
                {
                    _initialCorrsRemaining -= desiredXCorrsToRun;
                }

                // And output stuff to the debug logger
                if (_debugLogger != null)
                {
                    _debugLogger.Log(string.Format("RT {0:F2} HC {1:F2}@{2:F2} SD {3:F2} CF {4:F2} CM {5:F2} GS {6:F2} SA {7} MA {8} H {9}",
                        xCorrRatio,
                        _xCorrHighestCorrelation,
                        AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, (long)_xCorrHighestCorrelationIdx).TotalMilliseconds,
                        _xCorrDeviation,
                        _correlationConfidence,
                        AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, (long)_xCorrCenterOfMass).TotalMilliseconds,
                        GetEstimatedDelay().Value.TotalMilliseconds,
                        speakerMaxAmplitude,
                        micMaxAmplitude,
                        PrintHistogram(100, false)));

                    //_debugLogger.Log(string.Empty);
                    //_debugLogger.Log("speakerAheadOfMicSamples " + speakerAheadOfMicSamples + " numCrossCorrelations " + numCrossCorrelations);
                    //TinyHistogram hist = new TinyHistogram();
                    //for (int c = 0; c < _xCorrLikelihoodLength; c++)
                    //{
                    //    hist.AddValue(c, _xCorrLikelihood[c]);
                    //}

                    //_debugLogger.Log("XCORR " + hist.RenderAsOneLine(200 * numCrossCorrelations / _speakerBufferLengthSamples, false));
                    //hist.Reset();

                    //for (int c = 0; c < _micBufferLengthSamples; c++)
                    //{
                    //    hist.AddValue(c, Math.Abs(_micBufferMono[c]));
                    //}
                    //StringBuilder x = new StringBuilder();
                    //x.Append("MIC   ");
                    //x.Append(' ', (200 * speakerAheadOfMicSamples / _speakerBufferLengthSamples) - 5);
                    //x.Append(hist.RenderAsOneLine(200 * _micBufferLengthSamples / _speakerBufferLengthSamples, false));
                    ////_debugLogger.Log(x.ToString());
                    //hist.Reset();

                    //for (int c = 0; c < _speakerBufferLengthSamples; c++)
                    //{
                    //    hist.AddValue(c, Math.Abs(_speakerBufferMono[c]));
                    //}
                    ////_debugLogger.Log("SPEAK " + hist.RenderAsOneLine(200, false));
                }
            }
        }

        private void CalculateXCorrStatistics()
        {
            // Calculate center of mass and deviation, which will tell us the detected delay + confidence
            // Also find the absolute highest correlation value while we're here
            float xCorrTotalMass = 0;
            _xCorrHighestCorrelation = 0;
            int xCorrHighestCorrelationIdx = 0;
            for (int idx = 0; idx < _maxObservableDelaySamples; idx++)
            {
                float t = _xCorrLikelihood[idx];
                xCorrTotalMass += t;
                if (t > _xCorrHighestCorrelation)
                {
                    _xCorrHighestCorrelation = t;
                    xCorrHighestCorrelationIdx = idx;
                }
            }

            if (xCorrTotalMass == 0)
            {
                return;
            }

            int xCorrCenterOfMass = 0;
            float runningTotalMass = 0;
            float xCorrHalfMass = xCorrTotalMass / 2.0f;
            for (int idx = 0; idx < _maxObservableDelaySamples; idx++)
            {
                runningTotalMass += _xCorrLikelihood[idx];
                if (runningTotalMass >= xCorrHalfMass)
                {
                    xCorrCenterOfMass = idx;
                    break;
                }
            }

            // Calculate weighted deviation. Normal standard deviation assumes each deviation from mean is square.
            // Here, the deviation is a rectangle with width = diff from mean, height = xcorr weight
            float sigmaVariance = 0;
            for (int idx = 0; idx < _maxObservableDelaySamples; idx++)
            {
                float distanceFromMean = xCorrCenterOfMass - idx;
                float thisSampleVariance = distanceFromMean * (_xCorrLikelihood[idx] / _xCorrHighestCorrelation);
                if (thisSampleVariance < 0)
                {
                    thisSampleVariance = 0 - thisSampleVariance;
                }

                sigmaVariance += thisSampleVariance;
            }

            float variance = sigmaVariance / (float)_maxObservableDelaySamples / _xCorrHighestCorrelation;

            // Calculate standard deviation. Units don't really have a definite meaning here, but we do convert from "samples" to "milliseconds"
            _xCorrDeviation = (float)(Math.Sqrt(variance) * 1000 /* ms per second */ / (double)OutputFormat.SampleRateHz * 30.0f /* approximate conversion to milliseconds of standard deviation*/);
            _xCorrCenterOfMass = (float)xCorrCenterOfMass;
            _xCorrHighestCorrelationIdx = xCorrHighestCorrelationIdx;

            // scale the highest correlation so that 1.0 means "a very strong correlation"
            _xCorrHighestCorrelation = _xCorrHighestCorrelation * 3.0f;

            // After all of this, a "good match" will have about >0.5 correlation, <20.0 deviation
            // Sigmoid() returns values between 0.5 and 1.0 for positive inputs, so we have to scale that as well.
            _correlationConfidence = (FastMath.Sigmoid(2.0f * (_xCorrHighestCorrelation / 0.5f) / (_xCorrDeviation / 20.0f)) - 0.5f) * 2.0f;
        }

        /// <summary>
        /// Gets the maximum amplitude of an waveform
        /// </summary>
        /// <param name="buffer">The buffer to look at</param>
        /// <param name="start">The start index to process</param>
        /// <param name="end">The end index to process (exclusive)</param>
        /// <returns></returns>
        private static float GetMaxAmplitude(float[] buffer, int start, int end)
        {
            float returnVal = 0;
            for (int c = start; c < end; c++)
            {
                float m = buffer[c];
                if (m < 0) m = 0 - m;
                if (m > returnVal)
                {
                    returnVal = m;
                }
            }

            return returnVal;
        }
    }
}
