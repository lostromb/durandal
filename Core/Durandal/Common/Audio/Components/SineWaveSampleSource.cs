using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio sample source which produces a constant sine wave forever.
    /// </summary>
    public sealed class SineWaveSampleSource : AbstractAudioSampleSource
    {
        private const double TWOPI = Math.PI * 2.0;
        private readonly double _phaseIncrement = 0;
        private readonly float _amplitude;
        private double _phase = 0;

        public override bool PlaybackFinished => false;

        /// <summary>
        /// Constructs a new <see cref="SineWaveSampleSource"/>  with the specified frequency / amplitude.
        /// </summary>
        /// <param name="graph">The graph that this component will be part of.</param>
        /// <param name="format">The format of the output audio.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="frequencyHz">The frequency of the wave to produce.</param>
        /// <param name="amplitude">The wave's amplitude, from 0.0 to 1.0</param>
        public SineWaveSampleSource(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, float frequencyHz, float amplitude) : base(graph, nameof(SineWaveSampleSource), nodeCustomName)
        {
            OutputFormat = format.AssertNonNull(nameof(format));
            if (amplitude < 0 || amplitude > 1.0f)
            {
                throw new ArgumentOutOfRangeException(nameof(amplitude));
            }
            if (frequencyHz < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frequencyHz));
            }

            _amplitude = amplitude;
            _phaseIncrement = (double)frequencyHz * TWOPI / (double)format.SampleRateHz;
        }

        /// <summary>
        /// Advances this source by the specified number of samples per channel, writing them to the output
        /// </summary>
        /// <param name="samplesPerChannelToWrite">The number of samples per channel to write</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        public async Task WriteSamplesToOutput(int samplesPerChannelToWrite, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await OutputGraph.LockGraphAsync(cancelToken, realTime).ConfigureAwait(false);
            OutputGraph.BeginInstrumentedScope(realTime, NodeFullName);
            try
            {
                if (Output == null)
                {
                    return;
                }

                using (PooledBuffer<float> pooledBuffer = BufferPool<float>.Rent(samplesPerChannelToWrite * OutputFormat.NumChannels))
                {
                    int writeIdx = 0;
                    float[] buffer = pooledBuffer.Buffer;
                    for (int c = 0; c < samplesPerChannelToWrite; c++)
                    {
                        float sin = ((float)Math.Sin(_phase) * _amplitude);
                        for (int chan = 0; chan < OutputFormat.NumChannels; chan++)
                        {
                            buffer[writeIdx++] = sin;
                        }

                        _phase += _phaseIncrement;
                        if (_phase > TWOPI)
                        {
                            _phase -= TWOPI;
                        }
                    }

                    await Output.WriteAsync(
                        buffer,
                        0,
                        samplesPerChannelToWrite,
                        cancelToken,
                        realTime).ConfigureAwait(false);
                }
            }
            finally
            {
                OutputGraph.EndInstrumentedScope(realTime, AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, samplesPerChannelToWrite));
                OutputGraph.UnlockGraph();
            }
        }

        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int writeIdx = bufferOffset;
            for (int c = 0; c < numSamplesPerChannel; c++)
            {
                float sin = ((float)Math.Sin(_phase) * _amplitude);
                for (int chan = 0; chan < OutputFormat.NumChannels; chan++)
                {
                    buffer[writeIdx++] = sin;
                }

                _phase += _phaseIncrement;
                if (_phase > TWOPI)
                {
                    _phase -= TWOPI;
                }
            }

            return new ValueTask<int>(numSamplesPerChannel);
        }
    }
}
