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
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Audio
{
    public class AudioRandomDelayFilter : AbstractAudioSampleFilter
    {
        private readonly IRandom _rand;
        private readonly ILogger _logger;
        private bool _streamFinished = false;

        /// <summary>
        /// Creates an audio filter which will pass through from the input only up to a specified amount of cumulative data.
        /// The length of this data is controlled by <see cref="LengthOfAudioToAllow"/>.
        /// </summary>
        /// <param name="graph">The graph that this will be a part of</param>
        /// <param name="format">The audio format</param>
        public AudioRandomDelayFilter(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, IRandom rand, ILogger logger)
            : base(graph, nameof(AudioThrottlingFilter), nodeCustomName: null)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            MinDelay = TimeSpan.Zero;
            MaxDelay = TimeSpan.Zero;
            _rand = rand.AssertNonNull(nameof(rand));
            _logger = logger.AssertNonNull(nameof(logger));
        }

        /// <summary>
        /// The minimum delay to include on each read/write.
        /// </summary>
        public TimeSpan MinDelay { get; set; }

        /// <summary>
        /// The maximum delay to include on each read/write.
        /// </summary>
        public TimeSpan MaxDelay { get; set; }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_streamFinished)
            {
                return -1;
            }

            TimeSpan delay = GenerateDelay();
            if (delay > TimeSpan.Zero)
            {
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Delaying audio read for {0} ms", delay.TotalMilliseconds);
                await realTime.WaitAsync(delay, cancelToken).ConfigureAwait(false);
            }

            int returnVal = await Input.ReadAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
            if (returnVal < 0)
            {
                _streamFinished = true;
            }

            return returnVal;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            TimeSpan delay = GenerateDelay();
            if (delay > TimeSpan.Zero)
            {
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "Delaying audio write for {0} ms", delay.TotalMilliseconds);
                await realTime.WaitAsync(delay, cancelToken).ConfigureAwait(false);
            }

            await Output.WriteAsync(buffer, offset, count, cancelToken, realTime);
        }

        private TimeSpan GenerateDelay()
        {
            return TimeSpan.FromMilliseconds((_rand.NextDouble() * (MaxDelay.TotalMilliseconds - MinDelay.TotalMilliseconds)) + MinDelay.TotalMilliseconds);
        }
    }
}
