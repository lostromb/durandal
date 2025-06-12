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
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Audio
{
    public static class AudioTestHelpers
    {
        public static void AssertPeakVolumeAtPoint(
            AudioSample sample,
            TimeSpan pointOfInterest,
            float expectedPeak,
            int channel = 0,
            float delta = 0.01f)
        {
            AssertPeakVolumeAtRange(
                sample,
                pointOfInterest - TimeSpan.FromMilliseconds(1),
                pointOfInterest + TimeSpan.FromMilliseconds(1),
                expectedPeak,
                channel,
                delta);
        }

        public static void AssertPeakVolumeAtRange(
            AudioSample sample,
            TimeSpan rangeStart,
            TimeSpan rangeEnd,
            float expectedPeak,
            int channel = 0,
            float delta = 0.01f)
        {
            float peak = 0;
            int peakSample = 0;
            int start = Math.Max(0, (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(sample.Format.SampleRateHz, rangeStart));
            int end = Math.Min(sample.LengthSamplesPerChannel, (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(sample.Format.SampleRateHz, rangeEnd));
            Assert.IsFalse(start > end, "Specified range is invalid");
            Assert.AreNotEqual(start, end, "Specified range of interest is outside the bounds of the sample");
            for (int c = start; c < end; c++)
            {
                float t = sample.Data.Array[sample.Data.Offset + (c * sample.Format.NumChannels) + channel];
                if (Math.Abs(t) > peak)
                {
                    peak = Math.Abs(t);
                    peakSample = c;
                }
            }

            TimeSpan peakTime = AudioMath.ConvertSamplesPerChannelToTimeSpan(sample.Format.SampleRateHz, peakSample);
            Assert.AreEqual(expectedPeak, peak, delta, "Audio peak out of range at time " + peakTime.PrintTimeSpan() + "; expected " + expectedPeak + " +-" + delta + ", got " + peak);
        }

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

            if (sample.Data.Count == 0)
            {
                return; // zero audio data is technically continuous
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

        public static async Task<AudioSample> PushAudioThroughGraph(
            WeakPointer<IAudioGraph> graph,
            IAudioSampleTarget graphIn,
            IAudioSampleSource graphOut,
            AudioSample inputSample,
            int srand = -1)
        {
            IRandom rand;
            if (srand == -1)
            {
                rand = new FastRandom();
            }
            else
            {
                rand = new FastRandom(srand);
            }

            using (FixedAudioSampleSource source = new FixedAudioSampleSource(graph, inputSample, null))
            using (SimulatedUnreliableAudioSource unreliability = new SimulatedUnreliableAudioSource(graph, graphIn.InputFormat, rand, 0.3f))
            using (PollutedBufferPipe bufferPolluter1 = new PollutedBufferPipe(graph, graphIn.InputFormat))
            using (PollutedBufferPipe bufferPolluter2 = new PollutedBufferPipe(graph, graphOut.OutputFormat))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(graph, graphOut.OutputFormat, null))
            {
                source.ConnectOutput(unreliability);
                unreliability.ConnectOutput(bufferPolluter1);
                bufferPolluter1.ConnectOutput(graphIn);
                graphOut.ConnectOutput(bufferPolluter2);
                bufferPolluter2.ConnectOutput(target);

                while (!source.PlaybackFinished)
                {
                    int amountRead = await target.ReadSamplesFromInput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    if (amountRead < 0)
                    {
                        // possibly premature end of stream
                        source.DisconnectOutput();
                        target.DisconnectInput();
                        return target.GetAllAudio();
                    }

                    await source.WriteSamplesToOutput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                source.DisconnectOutput();
                target.DisconnectInput();
                return target.GetAllAudio();
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

        public static AudioSample GenerateDiffImage(AudioSample a, AudioSample b)
        {
            int limit = Math.Min(a.LengthSamplesPerChannel, b.LengthSamplesPerChannel) * a.Format.NumChannels;
            float[] newSampleData = new float[limit];

            for (int c = 0; c < limit; c++)
            {
                float thisDifference = Math.Abs(a.Data.Array[c + a.Data.Offset] - b.Data.Array[c + b.Data.Offset]);
                newSampleData[c] = thisDifference;
            }

            return new AudioSample(newSampleData, a.Format);
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
