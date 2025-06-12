using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Components.Noise;
using Durandal.Common.Audio.Dsp;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Audio.Dsp
{
    [TestClass]
    public class GoertzelArrayTests
    {
        [TestMethod]
        public void TestGoertzelArrayInvalidInputs()
        {
            TestAssert.ExceptionThrown<ArgumentOutOfRangeException>(() => new GoertzelArray(0, 44100));
            TestAssert.ExceptionThrown<ArgumentOutOfRangeException>(() => new GoertzelArray(-10, 44100));
            TestAssert.ExceptionThrown<ArgumentOutOfRangeException>(() => new GoertzelArray(100, 0));
            TestAssert.ExceptionThrown<ArgumentOutOfRangeException>(() => new GoertzelArray(100, -100));

            // Extreme frequency should still result in a valid internal buffer
            GoertzelArray res = new GoertzelArray(200, 9600000);
            res.AddFilter(441);
            res.AddSamples(new float[100].AsSpan());
        }

        [TestMethod]
        [DataRow(440, 1.0f, 48000)]
        [DataRow(440, 0.5f, 48000)]
        [DataRow(440, 0.1f, 48000)]
        [DataRow(440, 1.0f, 24000)]
        [DataRow(440, 0.5f, 24000)]
        [DataRow(440, 0.1f, 24000)]
        [DataRow(440, 1.0f, 16000)]
        [DataRow(440, 0.5f, 16000)]
        [DataRow(440, 0.1f, 16000)]
        [DataRow(1600, 0.1f, 48000)]
        [DataRow(1600, 0.5f, 48000)]
        [DataRow(1600, 1.0f, 48000)]
        [DataRow(2750, 0.1f, 48000)]
        [DataRow(2750, 0.5f, 48000)]
        [DataRow(2750, 1.0f, 48000)]
        public async Task TestGoertzelArraySingleFilterExactMatch(int frequency, float amplitude, int sampleRate)
        {
            int samplesToAccumulate = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(sampleRate, TimeSpan.FromMilliseconds(25));
            AudioSampleFormat format = AudioSampleFormat.Mono(sampleRate);
            float[] scratch = new float[100];
            int totalSamplesRead = 0;
            GoertzelArray filter = new GoertzelArray(samplesToAccumulate, sampleRate);
            filter.AddFilter(frequency);
            Assert.AreEqual(1, filter.NumFilters);
            Assert.AreEqual(samplesToAccumulate, filter.AccumulatorSizeSamples);
            using (IAudioGraph fakeGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (SineWaveSampleSource sineWaveSource = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(fakeGraph), format, null, frequency, amplitude))
            {
                while (totalSamplesRead < samplesToAccumulate)
                {
                    int toRead = Math.Min(scratch.Length, samplesToAccumulate - totalSamplesRead);
                    int samplesRead = await sineWaveSource.ReadAsync(scratch, 0, toRead, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(samplesRead > 0);
                    filter.AddSamples(scratch.AsSpan(0, samplesRead));
                    totalSamplesRead += samplesRead;
                }

                // Assert we have the correct response
                float E = Math.Max(1, filter.SignalEnergy);
                float normalizedR = (filter.Response[0] / E) * 10.0f / (float)samplesToAccumulate;
                Console.WriteLine(normalizedR);
                Assert.AreEqual(5.0f, normalizedR, 0.01f);

                // Test reset as well
                filter.Reset();

                // On this loop, add samples one at a time
                totalSamplesRead = 0;
                while (totalSamplesRead < samplesToAccumulate)
                {
                    int samplesRead = await sineWaveSource.ReadAsync(scratch, 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(samplesRead > 0);
                    filter.AddSample(scratch[0]);
                    totalSamplesRead++;
                }

                E = Math.Max(1, filter.SignalEnergy);
                normalizedR = (filter.Response[0] / E) * 10.0f / (float)samplesToAccumulate;
                Console.WriteLine(normalizedR);
                Assert.AreEqual(5.0f, normalizedR, 0.01f);
            }
        }

        [TestMethod]
        [DataRow(440, 1.0f, 48000)]
        [DataRow(440, 0.5f, 48000)]
        [DataRow(440, 0.1f, 48000)]
        [DataRow(440, 1.0f, 24000)]
        [DataRow(440, 0.5f, 24000)]
        [DataRow(440, 0.1f, 24000)]
        [DataRow(440, 1.0f, 16000)]
        [DataRow(440, 0.5f, 16000)]
        [DataRow(440, 0.1f, 16000)]
        [DataRow(1600, 0.1f, 48000)]
        [DataRow(1600, 0.5f, 48000)]
        [DataRow(1600, 1.0f, 48000)]
        [DataRow(2750, 0.1f, 48000)]
        [DataRow(2750, 0.5f, 48000)]
        [DataRow(2750, 1.0f, 48000)]
        public async Task TestGoertzelArraySingleFilterNoisy(int frequency, float amplitude, int sampleRate)
        {
            int samplesToAccumulate = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(sampleRate, TimeSpan.FromMilliseconds(25));
            AudioSampleFormat format = AudioSampleFormat.Mono(sampleRate);
            float[] scratch = new float[100];
            int totalSamplesRead = 0;
            GoertzelArray filter = new GoertzelArray(samplesToAccumulate, sampleRate);
            filter.AddFilter(frequency);
            using (IAudioGraph fakeGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (NoiseSampleSource noise = new NoiseSampleSource(new WeakPointer<IAudioGraph>(fakeGraph), format, new WhiteNoiseGenerator(format, amplitude * 0.2f, 6322243UL), null))
            using (SineWaveSampleSource sineWaveSource = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(fakeGraph), format, null, frequency, amplitude))
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(fakeGraph), format, null))
            {
                mixer.AddInput(sineWaveSource);
                mixer.AddInput(noise);
                while (totalSamplesRead < sampleRate * 2)
                {
                    int samplesRead = await mixer.ReadAsync(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(samplesRead > 0);
                    filter.AddSamples(scratch.AsSpan(0, samplesRead));
                    totalSamplesRead += samplesRead;
                }

                float E = Math.Max(1, filter.SignalEnergy);
                float normalizedR = (filter.Response[0] / E) * 10.0f / (float)samplesToAccumulate;
                Console.WriteLine(normalizedR);
                Assert.AreEqual(400.0f, normalizedR, 15.0f);
            }
        }

        [TestMethod]
        [DataRow(440, 1.0f, 48000)]
        [DataRow(440, 0.5f, 48000)]
        [DataRow(440, 0.1f, 48000)]
        [DataRow(440, 1.0f, 24000)]
        [DataRow(440, 0.5f, 24000)]
        [DataRow(440, 0.1f, 24000)]
        [DataRow(440, 1.0f, 16000)]
        [DataRow(440, 0.5f, 16000)]
        [DataRow(440, 0.1f, 16000)]
        [DataRow(1600, 0.1f, 48000)]
        [DataRow(1600, 0.5f, 48000)]
        [DataRow(1600, 1.0f, 48000)]
        [DataRow(2750, 0.1f, 48000)]
        [DataRow(2750, 0.5f, 48000)]
        [DataRow(2750, 1.0f, 48000)]
        public async Task TestGoertzelArraySingleFilterNoiseOnly(int frequency, float amplitude, int sampleRate)
        {
            int samplesToAccumulate = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(sampleRate, TimeSpan.FromMilliseconds(25));
            AudioSampleFormat format = AudioSampleFormat.Mono(sampleRate);
            float[] scratch = new float[100];
            int totalSamplesRead = 0;
            GoertzelArray filter = new GoertzelArray(samplesToAccumulate, sampleRate);
            filter.AddFilter(frequency);
            using (IAudioGraph fakeGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (NoiseSampleSource noise = new NoiseSampleSource(new WeakPointer<IAudioGraph>(fakeGraph), format, new WhiteNoiseGenerator(format, amplitude, 6322243UL), null))
            {
                while (totalSamplesRead < sampleRate * 2)
                {
                    int samplesRead = await noise.ReadAsync(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(samplesRead > 0);
                    filter.AddSamples(scratch.AsSpan(0, samplesRead));
                    totalSamplesRead += samplesRead;
                }

                float E = Math.Max(1, filter.SignalEnergy);
                float normalizedR = (filter.Response[0] / E) * 10.0f / (float)samplesToAccumulate;
                Console.WriteLine(normalizedR);
                Assert.AreEqual(0.0f, normalizedR, 0.1f);
            }
        }

        [TestMethod]
        [DataRow(440, 1.0f, 48000)]
        [DataRow(440, 0.5f, 48000)]
        [DataRow(440, 0.1f, 48000)]
        [DataRow(440, 1.0f, 24000)]
        [DataRow(440, 0.5f, 24000)]
        [DataRow(440, 0.1f, 24000)]
        [DataRow(440, 1.0f, 16000)]
        [DataRow(440, 0.5f, 16000)]
        [DataRow(440, 0.1f, 16000)]
        [DataRow(1600, 0.1f, 48000)]
        [DataRow(1600, 0.5f, 48000)]
        [DataRow(1600, 1.0f, 48000)]
        [DataRow(2750, 0.1f, 48000)]
        [DataRow(2750, 0.5f, 48000)]
        [DataRow(2750, 1.0f, 48000)]
        public async Task TestGoertzelArrayMultiFilterExactMatch(int frequency, float amplitude, int sampleRate)
        {
            const int numFilters = 15;
            int samplesToAccumulate = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(sampleRate, TimeSpan.FromMilliseconds(25));
            AudioSampleFormat format = AudioSampleFormat.Mono(sampleRate);
            float[] scratch = new float[100];
            int totalSamplesRead = 0;
            GoertzelArray filter = new GoertzelArray(samplesToAccumulate, sampleRate);
            for (int f = 0; f < numFilters; f++)
            {
                filter.AddFilter(frequency);
            }

            Assert.AreEqual(numFilters, filter.NumFilters);
            using (IAudioGraph fakeGraph = new AudioGraph(AudioGraphCapabilities.None))
            using (SineWaveSampleSource sineWaveSource = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(fakeGraph), format, null, frequency, amplitude))
            {
                while (totalSamplesRead < sampleRate * 2)
                {
                    int samplesRead = await sineWaveSource.ReadAsync(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(samplesRead > 0);
                    filter.AddSamples(scratch.AsSpan(0, samplesRead));
                    totalSamplesRead += samplesRead;
                }

                float E = Math.Max(1, filter.SignalEnergy);
                for (int f = 0; f < numFilters; f++)
                {
                    float normalizedR = (filter.Response[f] / E) * 10.0f / (float)samplesToAccumulate;
                    Assert.AreEqual(400.0f, normalizedR, 1.0f);
                }

                filter.Reset();

                // Loop 2 - add one sample at a time
                totalSamplesRead = 0;
                while (totalSamplesRead < sampleRate * 2)
                {
                    int samplesRead = await sineWaveSource.ReadAsync(scratch, 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                    Assert.IsTrue(samplesRead > 0);
                    filter.AddSample(scratch[0]);
                    totalSamplesRead++;
                }

                E = Math.Max(1, filter.SignalEnergy);
                for (int f = 0; f < numFilters; f++)
                {
                    float normalizedR = (filter.Response[f] / E) * 10.0f / (float)samplesToAccumulate;
                    Assert.AreEqual(400.0f, normalizedR, 1.0f);
                }
            }
        }
    }
}
