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
    /// Audio graph filter which can be used to prevent mixer stutters from unreliable inputs.
    /// Whenever the filter input reads less than the requested samples available, the filter instead pads silence and returns that to the caller.
    /// </summary>
    public sealed class SilencePaddingFilter : AbstractAudioSampleFilter
    {
        public SilencePaddingFilter(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName)
            : base(graph, nameof(SilencePaddingFilter), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
        }

        public override bool PlaybackFinished => Input == null ? false : Input.PlaybackFinished;

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int returnVal = await Input.ReadAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
            int samplesPerChannelMissing = count - returnVal;
            if (samplesPerChannelMissing > 0)
            {
                ArrayExtensions.WriteZeroes(
                    buffer,
                    offset + (returnVal * InputFormat.NumChannels),
                    samplesPerChannelMissing * InputFormat.NumChannels);
            }

            return count;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await Output.WriteAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
        }
    }
}
