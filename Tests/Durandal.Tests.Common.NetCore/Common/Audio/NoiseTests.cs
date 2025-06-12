using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Components.Noise;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    public class NoiseTests
    {
        [TestMethod]
        public async Task TestAudioWhiteNoiseMono48Khz()
        {
            IRandom rand = new FastRandom();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (NoiseSampleSource noise = new NoiseSampleSource(new WeakPointer<IAudioGraph>(graph), format, new WhiteNoiseGenerator(format, maxAmplitude: 0.75f)))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, "Test bucket"))
            {
                noise.ConnectOutput(bucket);
                int totalRead = 0;
                int desiredLength = format.SampleRateHz * 5;
                while (totalRead < desiredLength)
                {
                    int nextReadSize = Math.Min(desiredLength - totalRead, rand.NextInt(1, 512) * rand.NextInt(1, 512));
                    int actualReadSize = await bucket.ReadSamplesFromInput(nextReadSize, cancelToken, realTime).ConfigureAwait(false);
                    Assert.AreNotEqual(0, actualReadSize);
                    totalRead += actualReadSize;
                }

                AudioSample allAudio = bucket.GetAllAudio();
                Assert.IsNotNull(allAudio);

                // Assert on deviation of the output samples
                double[] stdDev = CalculateAudioSamplePerChannelDeviation(allAudio);
                Assert.AreEqual(0.433, stdDev[0], 0.03);

                // Assert on max peak of all samples
                double maxSample = 0;
                int iter = allAudio.Data.Offset;
                for (int c = 0; c < allAudio.LengthSamplesPerChannel; c++)
                {
                    maxSample = Math.Max(maxSample, allAudio.Data.Array[iter++]);
                }

                Assert.AreEqual(0.75, maxSample, 0.01);
            }
        }

        [TestMethod]
        public async Task TestAudioWhiteNoiseStereo48Khz()
        {
            IRandom rand = new FastRandom();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (NoiseSampleSource noise = new NoiseSampleSource(new WeakPointer<IAudioGraph>(graph), format, new WhiteNoiseGenerator(format, maxAmplitude: 0.75f)))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, "Test bucket"))
            {
                noise.ConnectOutput(bucket);
                int totalRead = 0;
                int desiredLength = format.SampleRateHz * 5;
                while (totalRead < desiredLength)
                {
                    int nextReadSize = Math.Min(desiredLength - totalRead, rand.NextInt(1, 512) * rand.NextInt(1, 512));
                    int actualReadSize = await bucket.ReadSamplesFromInput(nextReadSize, cancelToken, realTime).ConfigureAwait(false);
                    Assert.AreNotEqual(0, actualReadSize);
                    totalRead += actualReadSize;
                }

                AudioSample allAudio = bucket.GetAllAudio();
                Assert.IsNotNull(allAudio);
                double[] stdDev = CalculateAudioSamplePerChannelDeviation(allAudio);
                Assert.AreEqual(stdDev[0], stdDev[1], 0.03);
                Assert.AreEqual(0.433, stdDev[0], 0.03);
                Assert.AreEqual(0.433, stdDev[1], 0.03);

                // Assert on max peak of all samples on all channels
                for (int chan = 0; chan < format.NumChannels; chan++)
                {
                    double maxSample = 0;
                    int iter = chan + allAudio.Data.Offset;
                    for (int c = 0; c < allAudio.LengthSamplesPerChannel; c++)
                    {
                        maxSample = Math.Max(maxSample, allAudio.Data.Array[iter]);
                        iter += format.NumChannels;
                    }

                    Assert.AreEqual(0.75, maxSample, 0.01);
                }
            }
        }

        [TestMethod]
        public async Task TestAudioWhiteNoiseMono16Khz()
        {
            IRandom rand = new FastRandom();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (NoiseSampleSource noise = new NoiseSampleSource(new WeakPointer<IAudioGraph>(graph), format, new WhiteNoiseGenerator(format, maxAmplitude: 0.75f)))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, "Test bucket"))
            {
                noise.ConnectOutput(bucket);
                int totalRead = 0;
                int desiredLength = format.SampleRateHz * 5;
                while (totalRead < desiredLength)
                {
                    int nextReadSize = Math.Min(desiredLength - totalRead, rand.NextInt(1, 512) * rand.NextInt(1, 512));
                    int actualReadSize = await bucket.ReadSamplesFromInput(nextReadSize, cancelToken, realTime).ConfigureAwait(false);
                    Assert.AreNotEqual(0, actualReadSize);
                    totalRead += actualReadSize;
                }

                AudioSample allAudio = bucket.GetAllAudio();
                Assert.IsNotNull(allAudio);
                double[] stdDev = CalculateAudioSamplePerChannelDeviation(allAudio);
                Assert.AreEqual(0.433, stdDev[0], 0.03);

                // Assert on max peak of all samples on all channels
                for (int chan = 0; chan < format.NumChannels; chan++)
                {
                    double maxSample = 0;
                    int iter = chan + allAudio.Data.Offset;
                    for (int c = 0; c < allAudio.LengthSamplesPerChannel; c++)
                    {
                        maxSample = Math.Max(maxSample, allAudio.Data.Array[iter]);
                        iter += format.NumChannels;
                    }

                    Assert.AreEqual(0.75, maxSample, 0.01);
                }
            }
        }

        [TestMethod]
        public async Task TestAudioRetroNoiseMono48Khz()
        {
            IRandom rand = new FastRandom();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (NoiseSampleSource noise = new NoiseSampleSource(new WeakPointer<IAudioGraph>(graph), format, new RetroNoiseGenerator(format, maxAmplitude: 0.75f, frequency: 48)))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, "Test bucket"))
            {
                noise.ConnectOutput(bucket);
                int totalRead = 0;
                int desiredLength = format.SampleRateHz * 5;
                while (totalRead < desiredLength)
                {
                    int nextReadSize = Math.Min(desiredLength - totalRead, rand.NextInt(1, 512) * rand.NextInt(1, 512));
                    int actualReadSize = await bucket.ReadSamplesFromInput(nextReadSize, cancelToken, realTime).ConfigureAwait(false);
                    Assert.AreNotEqual(0, actualReadSize);
                    totalRead += actualReadSize;
                }

                AudioSample allAudio = bucket.GetAllAudio();
                Assert.IsNotNull(allAudio);
                double[] stdDev = CalculateAudioSamplePerChannelDeviation(allAudio);
                Assert.AreEqual(0.433, stdDev[0], 0.03);

                // Assert on max peak of all samples on all channels
                for (int chan = 0; chan < format.NumChannels; chan++)
                {
                    double maxSample = 0;
                    int iter = chan + allAudio.Data.Offset;
                    for (int c = 0; c < allAudio.LengthSamplesPerChannel; c++)
                    {
                        maxSample = Math.Max(maxSample, allAudio.Data.Array[iter]);
                        iter += format.NumChannels;
                    }

                    Assert.AreEqual(0.75, maxSample, 0.01);
                }
            }
        }

        [TestMethod]
        public async Task TestAudioRetroNoiseStereo48Khz()
        {
            IRandom rand = new FastRandom();
            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            CancellationToken cancelToken = CancellationToken.None;
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.None))
            using (NoiseSampleSource noise = new NoiseSampleSource(new WeakPointer<IAudioGraph>(graph), format, new RetroNoiseGenerator(format, maxAmplitude: 0.75f, frequency: 48)))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, "Test bucket"))
            {
                noise.ConnectOutput(bucket);
                int totalRead = 0;
                int desiredLength = format.SampleRateHz * 5;
                while (totalRead < desiredLength)
                {
                    int nextReadSize = Math.Min(desiredLength - totalRead, rand.NextInt(1, 512) * rand.NextInt(1, 512));
                    int actualReadSize = await bucket.ReadSamplesFromInput(nextReadSize, cancelToken, realTime).ConfigureAwait(false);
                    Assert.AreNotEqual(0, actualReadSize);
                    totalRead += actualReadSize;
                }

                AudioSample allAudio = bucket.GetAllAudio();
                Assert.IsNotNull(allAudio);
                double[] stdDev = CalculateAudioSamplePerChannelDeviation(allAudio);
                Assert.AreEqual(stdDev[0], stdDev[1], 0.03);
                Assert.AreEqual(0.433, stdDev[0], 0.03);
                Assert.AreEqual(0.433, stdDev[1], 0.03);

                // Assert on max peak of all samples on all channels
                for (int chan = 0; chan < format.NumChannels; chan++)
                {
                    double maxSample = 0;
                    int iter = chan + allAudio.Data.Offset;
                    for (int c = 0; c < allAudio.LengthSamplesPerChannel; c++)
                    {
                        maxSample = Math.Max(maxSample, allAudio.Data.Array[iter]);
                        iter += format.NumChannels;
                    }

                    Assert.AreEqual(0.75, maxSample, 0.01);
                }
            }
        }

        private static double[] CalculateAudioSamplePerChannelDeviation(AudioSample sample)
        {
            double[] average = new double[sample.Format.NumChannels];
            double[] stdDev = new double[sample.Format.NumChannels];
            for (int chan = 0; chan < sample.Format.NumChannels; chan++)
            {
                int iter = sample.Data.Offset;
                for (int c = 0; c < sample.LengthSamplesPerChannel; c++)
                {
                    average[chan] += sample.Data.Array[iter];
                    iter += sample.Format.NumChannels;
                }

                average[chan] = average[chan] / (double)sample.LengthSamplesPerChannel;
                iter = sample.Data.Offset;
                for (int c = 0; c < sample.LengthSamplesPerChannel; c++)
                {
                    double deviation = average[chan] - (double)sample.Data.Array[iter];
                    stdDev[chan] += (deviation * deviation);
                    iter += sample.Format.NumChannels;
                }

                stdDev[chan] = Math.Sqrt(stdDev[chan] / (double)sample.LengthSamplesPerChannel);
            }

            return stdDev;
        }
    }
}
