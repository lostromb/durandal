namespace Durandal.Common.Audio.Components
{
    using Durandal.Common.MathExt;
    using Durandal.Common.ServiceMgmt;
    using global::Durandal.Common.IO;
    using global::Durandal.Common.Time;
    using global::Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Pipe which measures the per-channel volume of audio which passes through it.
    /// </summary>
    public sealed class PassiveVolumeMeter : AbstractAudioSampleFilter
    {
        private static readonly TimeSpan DEFAULT_WINDOW = TimeSpan.FromMilliseconds(50);
        private readonly MovingAverageRmsVolume[] _perChannelVolume;

        /// <summary>
        /// Constructs a new <see cref="PassiveVolumeMeter"/>.
        /// </summary>
        /// <param name="graph">The audio graph this will be a part of.</param>
        /// <param name="format">The format of the audio signal.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="window">The size of the time window used for averaging (default 50ms).</param>
        public PassiveVolumeMeter(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, TimeSpan? window = null) : base(graph, nameof(PassiveVolumeMeter), nodeCustomName)
        {
            InputFormat = format.AssertNonNull(nameof(format));
            if (window.HasValue && window.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(window));
            }

            OutputFormat = format;
            _perChannelVolume = new MovingAverageRmsVolume[format.NumChannels];
            int volumeLengthSamples = Math.Max(1, (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, window.GetValueOrDefault(DEFAULT_WINDOW)));
            for (int c = 0; c < format.NumChannels; c++)
            {
                _perChannelVolume[c] = new MovingAverageRmsVolume(volumeLengthSamples, 0.0f);
            }
        }

        /// <summary>
        /// Gets the RMS volume of all channels averaged.
        /// </summary>
        /// <returns></returns>
        public float GetAverageRmsVolume()
        {
            float sum = 0;
            foreach (MovingAverageRmsVolume vol in _perChannelVolume)
            {
                sum += vol.RmsVolume;
            }

            return sum / (float)_perChannelVolume.Length;
        }

        /// <summary>
        /// Gets the RMS volume of the single loudest channel
        /// </summary>
        /// <returns></returns>
        public float GetLoudestRmsVolume()
        {
            float max = 0;
            foreach (MovingAverageRmsVolume vol in _perChannelVolume)
            {
                max = Math.Max(max, vol.RmsVolume);
            }

            return max;
        }

        /// <summary>
        /// Gets the volume of the single highest peak sample
        /// </summary>
        /// <returns></returns>
        public float GetPeakVolume()
        {
            float max = 0;
            foreach (MovingAverageRmsVolume vol in _perChannelVolume)
            {
                max = Math.Max(max, vol.PeakVolume);
            }

            return max;
        }

        /// <summary>
        /// Gets the RMS volume of a specific channel.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public float GetChannelRmsVolume(int channel)
        {
            if (channel < 0 || channel >= _perChannelVolume.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            return _perChannelVolume[channel].RmsVolume;
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelActuallyRead = await Input.ReadAsync(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);

            if (samplesPerChannelActuallyRead > 0)
            {
                // Process each sample across each interleaved channel after we have copied it to the destination
                for (int channel = 0; channel < InputFormat.NumChannels; channel++)
                {
                    MovingAverageRmsVolume thisChannelVolume = _perChannelVolume[channel];
                    for (int sample = 0; sample < samplesPerChannelActuallyRead * InputFormat.NumChannels; sample += InputFormat.NumChannels)
                    {
                        thisChannelVolume.Add(buffer[offset + channel + sample]);
                    }
                }
            }

            return samplesPerChannelActuallyRead;
        }

        protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Process each sample across each interleaved channel before we copy it to the destination
            for (int channel = 0; channel < InputFormat.NumChannels; channel++)
            {
                MovingAverageRmsVolume thisChannelVolume = _perChannelVolume[channel];
                for (int sample = 0; sample < count * InputFormat.NumChannels; sample += InputFormat.NumChannels)
                {
                    thisChannelVolume.Add(buffer[offset + channel + sample]);
                }
            }

            return Output.WriteAsync(buffer, offset, count, cancelToken, realTime);
        }
    }
}