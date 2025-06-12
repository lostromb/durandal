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
    /// The purpose of this component is to try to ensure that any Read() will always return exactly the number
    /// of samples that were requested and no fewer. If not enough samples are available downstream, the read
    /// will retry a fixed maximum number of times until enough data is available to satisfy the request.
    /// No buffering is done internally so this component should not add algorithmic delay.
    /// </summary>
    public sealed class AudioBlockFiller : AbstractAudioSampleFilter
    {
        // Maximum number of times to spinread
        private readonly int _maxSpinCount;

        public AudioBlockFiller(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, int maxSpinCount = 10)
            : base(graph, nameof(AudioBlockFiller), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            if (maxSpinCount <= 0)
            {
                throw new ArgumentOutOfRangeException("Max spin count must be positive");
            }

            _maxSpinCount = maxSpinCount;
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelRead = 0;
            int spinCount = 0;
            while (samplesPerChannelRead < count && spinCount++ < _maxSpinCount)
            {
                int thisReadSize = await Input.ReadAsync(
                    buffer,
                    offset + (samplesPerChannelRead * OutputFormat.NumChannels),
                    count - samplesPerChannelRead,
                    cancelToken,
                    realTime).ConfigureAwait(false);
                
                // Has input stream ended?
                if (thisReadSize < 0)
                {
                    if (samplesPerChannelRead > 0)
                    {
                        // Ended during a partial read. Return what we have
                        return samplesPerChannelRead;
                    }
                    else
                    {
                        // Stream is done and no more data to return.
                        return thisReadSize;
                    }
                }
                else
                {
                    samplesPerChannelRead += thisReadSize;
                }
            }

            return samplesPerChannelRead;
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Output.WriteAsync(buffer, offset, count, cancelToken, realTime);
        }
    }
}
