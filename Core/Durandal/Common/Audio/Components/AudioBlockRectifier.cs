using Durandal.Common.Collections;
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
    /// The purpose of this audio graph filter is to limit the block size of every read/write that passes through it,
    /// without modifying the data. This is done by splitting up large reads/writes into smaller chunks, so for example
    /// if you request 5000 samples of data, you will get 5000 samples, but downstream that might have been done as 5 reads
    /// of 1000 samples each. Likewise with writes.
    /// This only enforces a maximum cap on individual read/write sizes, so reads or writes smaller than the block length
    /// are unaffected.
    /// </summary>
    public sealed class AudioBlockRectifier : AbstractAudioSampleFilter
    {
        private readonly int _maxBlockLengthSamplesPerChannel;

        public AudioBlockRectifier(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, TimeSpan blockLength)
            : base(graph, nameof(AudioBlockRectifier), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            if (blockLength <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("BlockLength must be a positive time span");
            }

            _maxBlockLengthSamplesPerChannel = Math.Max(1, (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, blockLength));
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelRead = 0;
            while (samplesPerChannelRead < count)
            {
                int maxReadSize = Math.Min(count - samplesPerChannelRead, _maxBlockLengthSamplesPerChannel);
                int actualReadSize = await Input.ReadAsync(
                    buffer,
                    offset + (samplesPerChannelRead * OutputFormat.NumChannels),
                    maxReadSize,
                    cancelToken,
                    realTime).ConfigureAwait(false);

                // Has input stream ended?
                if (actualReadSize < 0)
                {
                    if (samplesPerChannelRead > 0)
                    {
                        // Ended during a partial read. Return what we have
                        return samplesPerChannelRead;
                    }
                    else
                    {
                        // Stream is done and no more data to return.
                        return actualReadSize;
                    }
                }
                else
                {
                    samplesPerChannelRead += actualReadSize;
                }
            }

            return samplesPerChannelRead;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelWritten = 0;
            while (samplesPerChannelWritten < count)
            {
                int writeSize = Math.Min(count - samplesPerChannelWritten, _maxBlockLengthSamplesPerChannel);
                await Output.WriteAsync(buffer, offset + samplesPerChannelWritten, writeSize, cancelToken, realTime).ConfigureAwait(false);
                samplesPerChannelWritten += writeSize;
            }
        }
    }
}
