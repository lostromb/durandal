using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Collections;
using Durandal.Common.Tasks;
using Durandal.Common.Audio.Dsp;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio sample source which produces standard DTMF tones for telephony.
    /// </summary>
    public sealed class DTMFToneGenerator : AbstractAudioSampleSource
    {
        private const float TWOPI = FastMath.PI * 2.0f;
        private readonly object _thisComponentLock;
        private readonly int _toneLengthSamplesPerChannel;
        private readonly int _toneGapSamplesPerChannel;
        private readonly int _envelopeFadeTimeSamplesPerChannel;
        private readonly Queue<QueuedDialTone> _queuedTones;
        private readonly float _halfAmplitude;
        private QueuedDialTone _currentTone;
        private float _phaseIncrementA = 0;
        private float _phaseIncrementB = 0;
        private float _phaseA = 0;
        private float _phaseB = 0;
        private int _envelopeFadeInRemaining;

        /// <summary>
        /// Constructs a new <see cref="DTMFToneGenerator"/>.
        /// </summary>
        /// <param name="graph">The graph that this component will be part of.</param>
        /// <param name="format">The format of the output audio.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="toneLength">The length of each tone (default 300ms)</param>
        /// <param name="toneGap">The gap between each tone (default 50ms)</param>
        /// <param name="toneAmplitude">The tone's amplitude, from 0.0 to 1.0</param>
        public DTMFToneGenerator(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            TimeSpan? toneLength = null,
            TimeSpan? toneGap = null,
            float toneAmplitude = 0.6f) : base(graph, nameof(DTMFToneGenerator), nodeCustomName)
        {
            OutputFormat = format.AssertNonNull(nameof(format));
            if (toneAmplitude < 0 || toneAmplitude > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(toneAmplitude));
            }
            if (toneLength.HasValue && toneLength.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(toneLength));
            }
            if (toneGap.HasValue && toneGap.Value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(toneGap));
            }

            _halfAmplitude = toneAmplitude * 0.5f;
            _toneLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, toneLength.GetValueOrDefault(TimeSpan.FromMilliseconds(300)));
            _toneGapSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, toneGap.GetValueOrDefault(TimeSpan.FromMilliseconds(50)));
            _envelopeFadeTimeSamplesPerChannel = _toneLengthSamplesPerChannel / 10;
            _thisComponentLock = new object();
            _queuedTones = new Queue<QueuedDialTone>();
        }

        /// <summary>
        /// Enqueues the specified tone to the tone generator.
        /// Small gaps are automatically inserted between tones.
        /// </summary>
        /// <param name="tone">The DTMF tone to queue</param>
        public void QueueTone(DialTone tone)
        {
            if (tone == DialTone.SILENCE)
            {
                throw new ArgumentException($"Use {nameof(QueueSilence)} to enqueue silence");
            }

            lock (_thisComponentLock)
            {
                _queuedTones.Enqueue(new QueuedDialTone()
                {
                    Tone = tone,
                    SamplesPerChannelRemaining = _toneLengthSamplesPerChannel
                });
                _queuedTones.Enqueue(new QueuedDialTone()
                {
                    Tone = DialTone.SILENCE,
                    SamplesPerChannelRemaining = _toneGapSamplesPerChannel
                });
            }
        }

        /// <summary>
        /// Enqueues the specified amount of silence to the tone generator.
        /// Small gaps are automatically inserted between tones.
        /// </summary>
        /// <param name="length">The amount of silence to queue.</param>
        public void QueueSilence(TimeSpan length)
        {
            if (length < TimeSpan.Zero)
            {
                throw new ArgumentException(nameof(length));
            }

            lock (_thisComponentLock)
            {
                _queuedTones.Enqueue(new QueuedDialTone()
                {
                    Tone = DialTone.SILENCE,
                    SamplesPerChannelRemaining = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(OutputFormat.SampleRateHz, length)
                });
            }
        }

        /// <summary>
        /// Gets the length of all tones (or silences) currently queued to the generator.
        /// </summary>
        public TimeSpan QueuedToneLength
        {
            get
            {
                lock (_thisComponentLock)
                {
                    long samplesPerChannelQueued = 0;
                    if (_currentTone != null)
                    {
                        samplesPerChannelQueued += _currentTone.SamplesPerChannelRemaining;
                    }

                    foreach (QueuedDialTone queued in _queuedTones)
                    {
                        samplesPerChannelQueued += queued.SamplesPerChannelRemaining;
                    }

                    return AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.NumChannels, samplesPerChannelQueued);
                }
            }
        }

        /// <inheritdoc />
        public override bool PlaybackFinished => false;

        /// <inheritdoc />
        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            lock (_thisComponentLock)
            {
                int samplesPerChannelWrittenToCaller = 0;
                while (samplesPerChannelWrittenToCaller < numSamplesPerChannel)
                {
                    if (_currentTone == null && _queuedTones.Count > 0)
                    {
                        _currentTone = _queuedTones.Dequeue();
                        if (_currentTone.Tone != DialTone.SILENCE)
                        {
                            DecodeTone(_currentTone.Tone);
                            _envelopeFadeInRemaining = _envelopeFadeTimeSamplesPerChannel;
                        }
                    }

                    int samplesPerChannelCanProcessFromThisTone = numSamplesPerChannel - samplesPerChannelWrittenToCaller;
                    if (_currentTone != null)
                    {
                        samplesPerChannelCanProcessFromThisTone = FastMath.Min(samplesPerChannelCanProcessFromThisTone, _currentTone.SamplesPerChannelRemaining);
                    }

                    if (_currentTone == null || _currentTone.Tone == DialTone.SILENCE)
                    {
                        // Write silence
                        ArrayExtensions.WriteZeroes(
                            buffer,
                            bufferOffset + (samplesPerChannelWrittenToCaller * OutputFormat.NumChannels),
                            samplesPerChannelCanProcessFromThisTone * OutputFormat.NumChannels);
                    }
                    else
                    {
                        // Write a tone waveform
                        int outIdx = bufferOffset + (samplesPerChannelWrittenToCaller * OutputFormat.NumChannels);
                        int fadeInStopTime = _envelopeFadeInRemaining;
                        int fadeOutStartTime = _currentTone.SamplesPerChannelRemaining - _envelopeFadeTimeSamplesPerChannel;
                        int fadeOutEndTime = _currentTone.SamplesPerChannelRemaining;
                        for (int c = 0; c < samplesPerChannelCanProcessFromThisTone; c++)
                        {
                            float waveform =
                                (FastMath.Sin(_phaseA) * _halfAmplitude) +
                                (FastMath.Cos(_phaseB) * _halfAmplitude);
                            if (c < fadeInStopTime)
                            {
                                // Fade in
                                waveform *= 1.0f - ((float)(_envelopeFadeInRemaining - c) / (float)_envelopeFadeTimeSamplesPerChannel);
                            }
                            else if (c > fadeOutStartTime)
                            {
                                // Fade out
                                waveform *= 1.0f - ((float)(c - fadeOutStartTime) / (float)(fadeOutEndTime - fadeOutStartTime));
                            }

                            for (int chan = 0; chan < OutputFormat.NumChannels; chan++)
                            {
                                buffer[outIdx++] = waveform;
                            }

                            _phaseA += _phaseIncrementA;
                            if (_phaseA > TWOPI)
                            {
                                _phaseA -= TWOPI;
                            }

                            _phaseB += _phaseIncrementB;
                            if (_phaseB > TWOPI)
                            {
                                _phaseB -= TWOPI;
                            }
                        }

                        _envelopeFadeInRemaining = FastMath.Max(0, _envelopeFadeInRemaining - samplesPerChannelCanProcessFromThisTone);
                    }

                    if (_currentTone != null)
                    {
                        _currentTone.SamplesPerChannelRemaining -= samplesPerChannelCanProcessFromThisTone;
                        if (_currentTone.SamplesPerChannelRemaining == 0)
                        {
                            _currentTone = null;
                        }
                    }

                    samplesPerChannelWrittenToCaller += samplesPerChannelCanProcessFromThisTone;
                }

                return new ValueTask<int>(samplesPerChannelWrittenToCaller);
            }
        }

        /// <summary>
        /// Decodes a tone into a pair of frequencies and sets parameters.
        /// </summary>
        /// <param name="tone">The tone to decode</param>
        private void DecodeTone(DialTone tone)
        {
            if (tone == DialTone.SILENCE)
            {
                return;
            }

            float freqA = 0;
            float freqB = 0;

            switch (tone)
            {
                case DialTone.TONE_1:
                case DialTone.TONE_2:
                case DialTone.TONE_3:
                case DialTone.TONE_A:
                    freqA = 697;
                    break;
                case DialTone.TONE_4:
                case DialTone.TONE_5:
                case DialTone.TONE_6:
                case DialTone.TONE_B:
                    freqA = 770;
                    break;
                case DialTone.TONE_7:
                case DialTone.TONE_8:
                case DialTone.TONE_9:
                case DialTone.TONE_C:
                    freqA = 852;
                    break;
                case DialTone.TONE_STAR:
                case DialTone.TONE_0:
                case DialTone.TONE_POUND:
                case DialTone.TONE_D:
                    freqA = 941;
                    break;
            }

            switch (tone)
            {
                case DialTone.TONE_1:
                case DialTone.TONE_4:
                case DialTone.TONE_7:
                case DialTone.TONE_STAR:
                    freqB = 1209;
                    break;
                case DialTone.TONE_2:
                case DialTone.TONE_5:
                case DialTone.TONE_8:
                case DialTone.TONE_0:
                    freqB = 1336;
                    break;
                case DialTone.TONE_3:
                case DialTone.TONE_6:
                case DialTone.TONE_9:
                case DialTone.TONE_POUND:
                    freqB = 1477;
                    break;
                case DialTone.TONE_A:
                case DialTone.TONE_B:
                case DialTone.TONE_C:
                case DialTone.TONE_D:
                    freqB = 1633;
                    break;
            }

            _phaseA = 0;
            _phaseB = 0;
            _phaseIncrementA = freqA * TWOPI / OutputFormat.SampleRateHz;
            _phaseIncrementB = freqB * TWOPI / OutputFormat.SampleRateHz;
        }

        private class QueuedDialTone
        {
            public DialTone Tone { get; set; }
            public int SamplesPerChannelRemaining { get; set; }
        }
    }
}
