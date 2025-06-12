using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components.Noise
{
    /// <summary>
    /// Sample source which generates noise
    /// </summary>
    public sealed class NoiseSampleSource : AbstractAudioSampleSource
    {
        private readonly INoiseGenerator _generator;

        public override bool PlaybackFinished => false;

        /// <summary>
        /// Constructs a new <see cref="NoiseSampleSource"/>  with the specified characteristics.
        /// </summary>
        /// <param name="graph">The graph that this component will be part of.</param>
        /// <param name="format">The format of the output audio.</param>
        /// <param name="generator">The implementation of noise generator (for example <see cref="WhiteNoiseGenerator"/></param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        public NoiseSampleSource(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            INoiseGenerator generator,
            string nodeCustomName = null)
            : base(graph, nameof(NoiseSampleSource), nodeCustomName)
        {
            OutputFormat = format.AssertNonNull(nameof(format));
            _generator = generator.AssertNonNull(nameof(generator));
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
            try
            {
                if (Output == null)
                {
                    return;
                }

                using (PooledBuffer<float> pooledBuffer = BufferPool<float>.Rent(samplesPerChannelToWrite * OutputFormat.NumChannels))
                {
                    _generator.GenerateNoise(pooledBuffer.Buffer, 0, samplesPerChannelToWrite);
                    await Output.WriteAsync(
                        pooledBuffer.Buffer,
                        0,
                        samplesPerChannelToWrite,
                        cancelToken,
                        realTime).ConfigureAwait(false);
                }
            }
            finally
            {
                OutputGraph.UnlockGraph();
            }
        }

        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _generator.GenerateNoise(buffer, bufferOffset, numSamplesPerChannel);
            return new ValueTask<int>(numSamplesPerChannel);
        }
    }
}
