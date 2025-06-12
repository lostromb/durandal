using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio sample target which does nothing to audio samples given to it.
    /// </summary>
    public class NullAudioSampleTarget : AbstractAudioSampleTarget
    {
        public NullAudioSampleTarget(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName) : base(graph, nameof(NullAudioSampleTarget), nodeCustomName)
        {
            InputFormat = format;
        }

        /// <summary>
        /// Reads the specified number of samples per channel from the input and discards them.
        /// </summary>
        /// <param name="samplesPerChannelToRead">The maximum number of samples to read from input</param>
        /// <param name="cancelToken">Cancel token for the read</param>
        /// <param name="realTime">Real time definition</param>
        /// <returns>The number of samples per channel actually read</returns>
        public async Task<int> ReadSamplesFromInput(int samplesPerChannelToRead, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await InputGraph.LockGraphAsync(cancelToken, realTime).ConfigureAwait(false);
            try
            {
                InputGraph.BeginInstrumentedScope(realTime, NodeFullName);
                if (Input == null)
                {
                    return 0;
                }

                using (PooledBuffer<float> pooledBuffer = BufferPool<float>.Rent(samplesPerChannelToRead * InputFormat.NumChannels))
                {
                    return await Input.ReadAsync(
                        pooledBuffer.Buffer,
                        0,
                        samplesPerChannelToRead,
                        cancelToken,
                        realTime).ConfigureAwait(false);
                }
            }
            finally
            {
                InputGraph.EndInstrumentedScope(realTime, AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, samplesPerChannelToRead));
                InputGraph.UnlockGraph();
            }
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            InputGraph.BeginComponentInclusiveScope(realTime, NodeFullName);
            InputGraph.EndComponentInclusiveScope(realTime);
            return new ValueTask();
        }
    }
}
