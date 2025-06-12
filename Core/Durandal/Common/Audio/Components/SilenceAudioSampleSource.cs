using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio sample source which generates silence indefinitely.
    /// </summary>
    public sealed class SilenceAudioSampleSource : AbstractAudioSampleSource
    {
        public SilenceAudioSampleSource(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName)
            : base(graph, nameof(SilenceAudioSampleSource), nodeCustomName)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            OutputFormat = format;
        }

        public override bool PlaybackFinished => false;

        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ArrayExtensions.WriteZeroes(buffer, bufferOffset, numSamplesPerChannel * OutputFormat.NumChannels);
            return new ValueTask<int>(numSamplesPerChannel);
        }
    }
}
