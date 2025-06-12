using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.MathExt;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Tests.Common.Audio;
using Durandal.Common.Audio.Test;

namespace Durandal.Tests.Common.Audio
{
    public static class AudioTestHelpers
    {
        public static void AssertAudioSignalIsContinuous(
            AudioSample sample,
            float maxDelta = 0.10f)
        {
            AssertAudioSignalIsContinuous(sample, TimeSpan.Zero, sample.Duration, maxDelta);
        }

        public static void AssertAudioSignalIsContinuous(
            AudioSample sample,
            TimeSpan start,
            TimeSpan end,
            float maxDelta = 0.10f)
        {
            if (start < TimeSpan.Zero)
            {
                start = TimeSpan.Zero;
            }
            if (end > sample.Duration)
            {
                end = sample.Duration;
            }

            int startIdx = (int)((long)start.TotalMilliseconds * (long)sample.Format.SampleRateHz / 1000L);
            int count = (int)((long)(end - start).TotalMilliseconds * (long)sample.Format.SampleRateHz / 1000L);
            
            // Iterate through each channel
            for (int channel = 0; channel < sample.Format.NumChannels; channel++)
            {
                int idx = (startIdx * sample.Format.NumChannels) + channel;
                float prev = sample.Data.Array[sample.Data.Offset + idx];
                for (int c = 0; c < count; c++)
                {
                    float next = sample.Data.Array[sample.Data.Offset + idx];
                    float delta = next - prev;
                    if (delta < 0)
                    {
                        delta = 0 - delta;
                    }

                    TimeSpan currentTime = AudioMath.ConvertSamplesPerChannelToTimeSpan(sample.Format.SampleRateHz, idx / sample.Format.NumChannels);
                    Assert.IsTrue(delta <= maxDelta, "Audio signal contained a discontinuity " + delta + " at time " + currentTime.PrintTimeSpan() + " outside of the accepted range +-" + maxDelta);

                    idx += sample.Format.NumChannels;
                    prev = next;
                }
            }
        }

        public static float CompareSimilarity(AudioSample a, AudioSample b)
        {
            int limit = Math.Min(a.LengthSamplesPerChannel, b.LengthSamplesPerChannel) * a.Format.NumChannels;
            float sumDifference = 0;

            // Calculate the mean difference
            for (int c = 0; c < limit; c++)
            {
                float thisDifference = Math.Abs(a.Data.Array[c + a.Data.Offset] - b.Data.Array[c + b.Data.Offset]);
                sumDifference += thisDifference;
            }

            float similarity = 1.0f - (sumDifference / (float)limit);
            return similarity;
        }

        public static void AssertSamplesAreSimilar(AudioSample a, AudioSample b, float requiredSimilarity = 1.0f, int maxLengthDeviationPerChannel = 0)
        {
            // Assert format is identical
            Assert.AreEqual(a.Format, b.Format, "Audio samples have incompatible formats. " + a.Format.ToString() + " != " + b.Format.ToString());

            // Assert sample length
            Assert.AreEqual((double)a.LengthSamplesPerChannel, (double)b.LengthSamplesPerChannel, maxLengthDeviationPerChannel, "Audio samples are too different in length. " + a.ToString() + " != " + b.ToString());

            float similarity = CompareSimilarity(a, b);
            Assert.IsTrue(similarity >= requiredSimilarity, "Audio samples were not similar enough: expected at least " + requiredSimilarity + ", got " + similarity);
        }

        /// <summary>
        /// Generates a sine wave, adding it to the data current in the input buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to</param>
        /// <param name="sampleRate">The sample rate of the sample</param>
        /// <param name="frequencyHz">The frequency of the wave to generate in hz</param>
        /// <param name="numSamples">The number of samples to generate (on one channel)</param>
        /// <param name="channel">The channel to generate the tone on</param>
        /// <param name="numChannels">The total number of channels in the signal (the interleave)</param>
        /// <param name="amplitude">Amplitude of the wave to generate</param>
        /// <param name="phase">The initial phase of the wave, from 0 to 1</param>
        public static void GenerateSineWave(
            float[] buffer,
            int sampleRate,
            float frequencyHz,
            int numSamplesPerChannelToWrite,
            int startOffsetSamplesPerChannel = 0,
            int channel = 0,
            int numChannels = 1,
            float amplitude = 1,
            double phase = 0)
        {
            const double TWOPI = Math.PI * 2;
            phase = phase * TWOPI;
            double phaseIncrement = (double)frequencyHz * TWOPI / (double)sampleRate;
            int writeIdx = (startOffsetSamplesPerChannel * numChannels) + channel;
            for (int c = 0; c < numSamplesPerChannelToWrite; c++)
            {
                buffer[writeIdx] += ((float)Math.Sin(phase) * amplitude);
                phase += phaseIncrement;
                if (phase > TWOPI)
                {
                    phase -= TWOPI;
                }

                writeIdx += numChannels;
            }
        }
    }
}
