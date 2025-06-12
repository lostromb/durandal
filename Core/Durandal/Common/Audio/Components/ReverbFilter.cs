using Durandal.Common.Collections;
using Durandal.Common.IO;
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
    /// Audio graph component which implements a basic reverb filter.
    /// </summary>
    public sealed class ReverbFilter : AbstractAudioSampleFilter
    {
        private readonly float[] _reverbBuffer;
        private readonly MovingAverageFloat[] _perChannelLag;
        private readonly int _bufferLengthPerChannel;
        private readonly float _reflectivity;
        private readonly float _hardness;
        private int _bufferIdxPerChannel = 0;

        /// <summary>
        /// Constructs a new reverb filter
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="format"></param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="delay">The echo delay on the reverb. Around 50-100ms is typical.</param>
        /// <param name="reflectivity">The parameter which defines the relative loudness of each echo, between 0 and 1.. Low reflectivity = low echo &amp; high decay</param>
        /// <param name="hardness">The parameter which defines the high-frequency absorption behavior of the simulated chamber, between 0 and 1. Low hardness = muffled echo and stronger HF suppression</param>
        public ReverbFilter(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, TimeSpan delay, float reflectivity = 0.6f, float hardness = 0.2f)
            : base(graph, nameof(ReverbFilter), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            if (delay <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }
            if (reflectivity < 0 || reflectivity > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(reflectivity));
            }
            if (_hardness < 0 || _hardness > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(hardness));
            }

            _bufferLengthPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, delay);
            _reverbBuffer = new float[_bufferLengthPerChannel * format.NumChannels];
            _perChannelLag = new MovingAverageFloat[format.NumChannels];
            for (int c = 0; c < format.NumChannels; c++)
            {
                _perChannelLag[c] = new MovingAverageFloat(Math.Max(10, (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, TimeSpan.FromMilliseconds(5))), 0);
            }

            _reflectivity = reflectivity;
            _hardness = hardness;
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] targetBuffer, int offset, int samplesPerChannelToRead, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            using (PooledBuffer<float> inputAudioBuffer = BufferPool<float>.Rent(samplesPerChannelToRead * OutputFormat.NumChannels))
            {
                int samplesPerChannelReadFromSource = await Input.ReadAsync(inputAudioBuffer.Buffer, 0, samplesPerChannelToRead, cancelToken, realTime).ConfigureAwait(false);
                int samplesPerChannelProcessed = 0;
                while (samplesPerChannelProcessed < samplesPerChannelReadFromSource)
                {
                    int samplesPerChannelCanProcessInBuffer = FastMath.Min(_bufferLengthPerChannel - _bufferIdxPerChannel, samplesPerChannelReadFromSource - samplesPerChannelProcessed);
                    if (samplesPerChannelCanProcessInBuffer > 0)
                    {
                        // Mix the incoming audio (full volume) into the reverb buffer
                        for (int sample = 0; sample < samplesPerChannelCanProcessInBuffer * OutputFormat.NumChannels; sample++)
                        {
                            _reverbBuffer[(_bufferIdxPerChannel * OutputFormat.NumChannels) + sample] +=
                                inputAudioBuffer.Buffer[(samplesPerChannelProcessed * OutputFormat.NumChannels) + sample];
                        }

                        // Copy to caller
                        ArrayExtensions.MemCopy(
                            _reverbBuffer,
                            _bufferIdxPerChannel * OutputFormat.NumChannels,
                            targetBuffer,
                            (offset + (samplesPerChannelProcessed * OutputFormat.NumChannels)),
                            samplesPerChannelCanProcessInBuffer * OutputFormat.NumChannels);

                        // Decay the reverb buffer and apply a rudimentary smoothing / lowpass filter
                        for (int chan = 0; chan < OutputFormat.NumChannels; chan++)
                        {
                            MovingAverageFloat lag = _perChannelLag[chan];
                            for (int sample = 0; sample < samplesPerChannelCanProcessInBuffer * OutputFormat.NumChannels; sample += OutputFormat.NumChannels)
                            {
                                float current = _reverbBuffer[(_bufferIdxPerChannel * OutputFormat.NumChannels) + chan + sample];
                                lag.Add(current);
                                _reverbBuffer[(_bufferIdxPerChannel * OutputFormat.NumChannels) + chan + sample] = ((current * _hardness) + (lag.Average * (1.0f - _hardness))) * _reflectivity;
                            }
                        }

                        samplesPerChannelProcessed += samplesPerChannelCanProcessInBuffer;
                        _bufferIdxPerChannel = (_bufferIdxPerChannel + samplesPerChannelCanProcessInBuffer) % _bufferLengthPerChannel;
                    }
                }

                return samplesPerChannelProcessed;
            }
        }

        protected override async ValueTask WriteAsyncInternal(float[] sourceBuffer, int offset, int samplesPerChannelToWrite, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelProcessed = 0;
            while (samplesPerChannelProcessed < samplesPerChannelToWrite)
            {
                int samplesPerChannelCanProcessInBuffer = FastMath.Min(_bufferLengthPerChannel - _bufferIdxPerChannel, samplesPerChannelToWrite - samplesPerChannelProcessed);
                if (samplesPerChannelCanProcessInBuffer > 0)
                {
                    // Mix the incoming audio (full volume) into the reverb buffer
                    for (int sample = 0; sample < samplesPerChannelCanProcessInBuffer * OutputFormat.NumChannels; sample++)
                    {
                        _reverbBuffer[(_bufferIdxPerChannel * OutputFormat.NumChannels) + sample] +=
                            sourceBuffer[(samplesPerChannelProcessed * OutputFormat.NumChannels) + offset + sample];
                    }

                    // Copy to caller
                    await Output.WriteAsync(
                        _reverbBuffer,
                        _bufferIdxPerChannel * OutputFormat.NumChannels,
                        samplesPerChannelCanProcessInBuffer,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    // Decay the reverb buffer and apply a rudimentary smoothing / lowpass filter
                    for (int chan = 0; chan < OutputFormat.NumChannels; chan++)
                    {
                        MovingAverageFloat lag = _perChannelLag[chan];
                        for (int sample = 0; sample < samplesPerChannelCanProcessInBuffer * OutputFormat.NumChannels; sample += OutputFormat.NumChannels)
                        {
                            float current = _reverbBuffer[(_bufferIdxPerChannel * OutputFormat.NumChannels) + chan + sample];
                            lag.Add(current);
                            _reverbBuffer[(_bufferIdxPerChannel * OutputFormat.NumChannels) + chan + sample] = ((current * _hardness) + (lag.Average * (1.0f - _hardness))) * _reflectivity;
                        }
                    }

                    samplesPerChannelProcessed += samplesPerChannelCanProcessInBuffer;
                    _bufferIdxPerChannel = (_bufferIdxPerChannel + samplesPerChannelCanProcessInBuffer) % _bufferLengthPerChannel;
                }
            }
        }
    }
}
