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
    /// Audio graph filter which passes data through, but also takes a running count of how much audio data has flowed across.
    /// </summary>
    public sealed class PositionMeasuringAudioPipe : AbstractAudioSampleFilter
    {
        private long _samplesPerChannelPassedThrough = 0;

        public PositionMeasuringAudioPipe(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName)
            : base(graph, nameof(PassthroughAudioPipe), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
        }

        /// <summary>
        /// Gets or sets the current position, meaning the amount of audio data that has passed through this component.
        /// Can be set to an arbitrary value if you want to change the time base or just reset it to zero.
        /// </summary>
        public TimeSpan Position
        {
            get
            {
                return AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, _samplesPerChannelPassedThrough);
            }
            set
            {
                _samplesPerChannelPassedThrough = AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.SampleRateHz, value);
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int readReturnVal = await Input.ReadAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
            if (readReturnVal > 0)
            {
                _samplesPerChannelPassedThrough += readReturnVal;
            }

            return readReturnVal;
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _samplesPerChannelPassedThrough += count;
            return Output.WriteAsync(buffer, offset, count, cancelToken, realTime);
        }
    }
}
