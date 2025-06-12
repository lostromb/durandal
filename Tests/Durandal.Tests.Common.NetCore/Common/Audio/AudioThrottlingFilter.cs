using Durandal.Common.Audio;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Tests.Common.Audio;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Audio
{
    public class AudioThrottlingFilter : AbstractAudioSampleFilter
    {
        private long _samplesPerChannelToAllow = 0;
        private long _samplesPerChannelPassedSoFar = 0;
        private bool _streamFinished = false;

        /// <summary>
        /// Creates an audio filter which will pass through from the input only up to a specified amount of cumulative data.
        /// The length of this data is controlled by <see cref="LengthOfAudioToAllow"/>.
        /// </summary>
        /// <param name="graph">The graph that this will be a part of</param>
        /// <param name="format">The audio format</param>
        public AudioThrottlingFilter(WeakPointer<IAudioGraph> graph, AudioSampleFormat format)
            : base(graph, nameof(AudioThrottlingFilter), nodeCustomName: null)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
        }

        public TimeSpan LengthOfAudioToAllow
        {
            get
            {
                return AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, _samplesPerChannelToAllow);
            }
            set
            {
                _samplesPerChannelToAllow = AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.SampleRateHz, value);
            }
        }

        public TimeSpan LengthOfAudioPassedSoFar
        {
            get
            {
                return AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, _samplesPerChannelPassedSoFar);
            }
        }

        public void AllowMoreAudio(TimeSpan amount)
        {
            _samplesPerChannelToAllow += AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.SampleRateHz, amount);
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_streamFinished)
            {
                return -1;
            }

            int samplesPerChannelAllowed = (int)Math.Min(_samplesPerChannelToAllow - _samplesPerChannelPassedSoFar, count);
            if (samplesPerChannelAllowed <= 0)
            {
                return 0;
            }

            int returnVal = await Input.ReadAsync(buffer, offset, samplesPerChannelAllowed, cancelToken, realTime).ConfigureAwait(false);
            if (returnVal < 0)
            {
                _streamFinished = true;
            }
            else
            {
                _samplesPerChannelPassedSoFar += returnVal;
            }

            return returnVal;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await Output.WriteAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
        }
    }
}
