using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Sample source which reads from a constant <see cref="AudioSample"/>. 
    /// </summary>
    public class FixedAudioSampleSource : AbstractAudioSampleSource
    {
        private readonly AudioSample _sample;
        private int _sampleCursor = 0;

        public override bool PlaybackFinished => _sampleCursor == _sample.LengthSamplesPerChannel;

        /// <summary>
        /// Constructs an audio sample source from a fixed audio sample object.
        /// </summary>
        public FixedAudioSampleSource(WeakPointer<IAudioGraph> graph, AudioSample sample, string nodeCustomName)
            : base(graph, nameof(FixedAudioSampleSource), nodeCustomName)
        {
            _sample = sample.AssertNonNull(nameof(sample));
            OutputFormat = _sample.Format;
        }

        /// <summary>
        /// Advances this source by the specified number of samples per channel, writing them to the output
        /// </summary>
        /// <param name="samplesPerChannelToWrite"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns>The number of samples per channel actually written to output</returns>
        public async Task<int> WriteSamplesToOutput(int samplesPerChannelToWrite, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await OutputGraph.LockGraphAsync(cancelToken, realTime).ConfigureAwait(false);
            try
            {
                if (Output == null)
                {
                    return 0;
                }

                int maxReadSizePerChannel = Math.Min(samplesPerChannelToWrite, _sample.LengthSamplesPerChannel - _sampleCursor);

                if (maxReadSizePerChannel > 0)
                {
                    await Output.WriteAsync(
                        _sample.Data.Array,
                        _sample.Data.Offset + (_sampleCursor * OutputFormat.NumChannels),
                        maxReadSizePerChannel,
                        cancelToken,
                        realTime).ConfigureAwait(false);
                    _sampleCursor += maxReadSizePerChannel;
                }

                if (PlaybackFinished)
                {
                    await Output.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
                }

                return maxReadSizePerChannel;
            }
            finally
            {
                OutputGraph.UnlockGraph();
            }
        }

        public async Task WriteFully(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            while (!PlaybackFinished)
            {
                await WriteSamplesToOutput(65536, cancelToken, realTime).ConfigureAwait(false);
            }
        }

        public void Reset()
        {
            _sampleCursor = 0;
        }

        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (bufferOffset < 0) throw new ArgumentOutOfRangeException(nameof(bufferOffset));
            if (numSamplesPerChannel <= 0) throw new ArgumentOutOfRangeException("Samples requested must be a positive integer");
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            if (_sampleCursor == _sample.LengthSamplesPerChannel)
            {
                return new ValueTask<int>(-1);
            }

            int maxReadSizePerChannel = Math.Min(numSamplesPerChannel, _sample.LengthSamplesPerChannel - _sampleCursor);

            ArrayExtensions.MemCopy(
                _sample.Data.Array,
                (_sample.Data.Offset + (_sampleCursor * OutputFormat.NumChannels)),
                buffer,
                bufferOffset,
                maxReadSizePerChannel * OutputFormat.NumChannels);
            _sampleCursor += maxReadSizePerChannel;

            return new ValueTask<int>(maxReadSizePerChannel);
        }
    }
}
