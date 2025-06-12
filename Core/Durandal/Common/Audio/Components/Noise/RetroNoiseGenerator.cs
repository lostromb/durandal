using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Components.Noise
{
    /// <summary>
    /// "Retro" noise generation based on 16 and 32-bit fast shifts and adds.
    /// Based on implementation in pcsxr emulator, credit to Dr. Hell for research.
    /// </summary>
    public class RetroNoiseGenerator : INoiseGenerator
    {
        private static readonly byte[] NOISE_ADD/*[64]*/ = {
                1, 0, 0, 1, 0, 1, 1, 0,
                1, 0, 0, 1, 0, 1, 1, 0,
                1, 0, 0, 1, 0, 1, 1, 0,
                1, 0, 0, 1, 0, 1, 1, 0,
                0, 1, 1, 0, 1, 0, 0, 1,
                0, 1, 1, 0, 1, 0, 0, 1,
                0, 1, 1, 0, 1, 0, 0, 1,
                0, 1, 1, 0, 1, 0, 0, 1};

        private static readonly ushort[] NOISE_FREQ_ADD = {
                0, 84, 140, 180, 210};

        // Noise Clock - range 0 to 63
        // Usually should be >32 to sound good
        private readonly byte _noiseClock;
        private uint[] _noiseCounterPerChannel; // Noise Counter
        private uint[] _noiseOutputPerChannel; // Noise Output
        private readonly uint _level;
        private readonly float _amplitudeNormalizer;
        private int _channelCount;

        /// <summary>
        /// Constructs a <see cref="RetroNoiseGenerator"/>.
        /// </summary>
        /// <param name="outputFormat">The format to output</param>
        /// <param name="maxAmplitude">The maximum amplitude of the generated noise between 0 and 1</param>
        /// <param name="frequency">A frequency value, between 0 and 63. Values above 32 are generally used.
        /// 38 is "SNES Volcano" sound. 45 is "Waterfall". Above that is near white noise.</param>
        /// <param name="srand">A seed for the random initialization, for deterministic testing</param>
        public RetroNoiseGenerator(AudioSampleFormat outputFormat, float maxAmplitude = 1.0f, byte frequency = 48, ulong? srand = null)
        {
            if (maxAmplitude <= 0)
            {
                throw new ArgumentOutOfRangeException("maxAmplitude param must be a positive number");
            }

            if (frequency < 0 || frequency > 63)
            {
                throw new ArgumentOutOfRangeException("frequency param must be between 0 and 63");
            }

            _channelCount = outputFormat.AssertNonNull(nameof(outputFormat)).NumChannels;
            _noiseClock = frequency;
            _noiseCounterPerChannel = new uint[_channelCount];
            _noiseOutputPerChannel = new uint[_channelCount];
            _amplitudeNormalizer = maxAmplitude / 32768.0f;

            // we can precalculate this since noiseclock is constant
            _level = 0x8000U >> (_noiseClock >> 2);
            _level <<= 16;
            // this normalizes the noise frequency based on the configured output sample rate
            _level = (uint)((((ulong)_level) * (uint)outputFormat.SampleRateHz) / 48000UL);

            // And randomize the initial noise counter and output so each channel is different
            IRandom rand = new FastRandom(srand.GetValueOrDefault((ulong)System.Diagnostics.Stopwatch.GetTimestamp()));
            for (int c = 0; c < _channelCount; c++)
            {
                _noiseCounterPerChannel[c] = (uint)rand.NextInt() % _level;
                _noiseOutputPerChannel[c] = (uint)(32768 * rand.NextFloat() * maxAmplitude);
            }
        }

        /// <inheritdoc/>
        public void GenerateNoise(float[] target, int offset, int samplesPerChannelToGenerate)
        {
            for (int chan = 0; chan < _channelCount; chan++)
            {
                int targetOffset = offset + chan;
                for (int sample = 0; sample < samplesPerChannelToGenerate; sample++)
                {
                    target[targetOffset] = UpdateNoise(ref _noiseCounterPerChannel[chan], ref _noiseOutputPerChannel[chan]) * _amplitudeNormalizer;
                    targetOffset += _channelCount;
                }
            }
        }

        private short UpdateNoise(ref uint noiseCounter, ref uint noiseOutput)
        {
            noiseCounter += 0x10000;
            noiseCounter += NOISE_FREQ_ADD[_noiseClock & 3];
            if ((noiseCounter & 0xffff) >= NOISE_FREQ_ADD[4])
            {
                noiseCounter += 0x10000;
                noiseCounter -= NOISE_FREQ_ADD[_noiseClock & 3];
            }

            if (noiseCounter >= _level)
            {
                noiseCounter = noiseCounter % _level;
                noiseOutput = (noiseOutput << 1) | NOISE_ADD[(noiseOutput >> 10) & 63];
            }

            return (short)noiseOutput;
        }
    }
}
