using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;

namespace Durandal.Common.Audio.Components.Noise
{
    /// <summary>
    /// Basic random white noise generator. All samples are random uniform.
    /// Output will have equal power on all frequency bands.
    /// </summary>
    public  class WhiteNoiseGenerator : INoiseGenerator
    {
        private readonly float _maxAmplitude;
        private readonly int _channelCount;
        private readonly FastRandom _rand;

        /// <summary>
        /// Constructs a new <see cref="WhiteNoiseGenerator"/>.
        /// </summary>
        /// <param name="outputFormat">The format of output audio</param>
        /// <param name="maxAmplitude">The maximum amplitude to output, between 0 and 1</param>
        /// <param name="srand">A seed for the random function, for deterministic testing.</param>
        public WhiteNoiseGenerator(AudioSampleFormat outputFormat, float maxAmplitude = 1.0f, ulong? srand = null)
        {
            if (maxAmplitude <= 0)
            {
                throw new ArgumentOutOfRangeException("maxAmplitude param must be a positive number");
            }

            _rand = new FastRandom(srand.GetValueOrDefault((ulong)System.Diagnostics.Stopwatch.GetTimestamp()));
            _maxAmplitude = maxAmplitude;
            _channelCount = outputFormat.AssertNonNull(nameof(outputFormat)).NumChannels;
        }

        /// <inheritdoc/>
        public void GenerateNoise(float[] target, int offset, int samplesPerChannelToGenerate)
        {
            for (int c = 0; c < samplesPerChannelToGenerate * _channelCount; c++)
            {
                target[offset++] = ((_rand.NextFloat() * 2) - 1) * _maxAmplitude;
            }
        }
    }
}
