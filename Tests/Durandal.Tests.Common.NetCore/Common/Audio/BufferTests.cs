using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Tests.Common.Audio;
using Durandal.Common.Test;
using Durandal.Common.Audio.Test;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    public class BufferTests
    {
        [TestMethod]
        public async Task TestAudioBufferReadsZeroIfEmpty()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.7f))
            using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(3122), 1.0f, 0.0f))
            using (AudioSampleBuffer buffer = new AudioSampleBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(500)))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(unreliableFilter);
                unreliableFilter.ConnectOutput(buffer);
                buffer.ConnectOutput(bucket);

                for (int c = 0; c < 10; c++)
                {
                    int samplesRead = await bucket.ReadSamplesFromInput(4800, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual(0, samplesRead);
                }

                source.DisconnectOutput();
                unreliableFilter.DisconnectOutput();
                buffer.DisconnectOutput();
            }
        }

        [TestMethod]
        public async Task TestAudioBufferUnreliableRead()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.7f))
            using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(5522), 0.5f))
            using (AudioSampleBuffer buffer = new AudioSampleBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(500)))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(unreliableFilter);
                unreliableFilter.ConnectOutput(buffer);
                buffer.ConnectOutput(bucket);

                // Now do random reads
                IRandom random = new FastRandom(77543);
                int samplesRead = 0;
                int samplesToRead = 48000 * 10;
                while (samplesRead < samplesToRead)
                {
                    int toRead = Math.Min(samplesToRead - samplesRead, random.NextInt(1, 40000));
                    samplesRead += await bucket.ReadSamplesFromInput(toRead, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                AudioSample outputSample = bucket.GetAllAudio();
                float[] expectedAudioData = new float[samplesToRead];
                AudioTestHelpers.GenerateSineWave(expectedAudioData, format.SampleRateHz, 440, samplesToRead, 0, 0, 1, 0.7f, 0.0f);
                AudioSample expectedAudio = new AudioSample(expectedAudioData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedAudio, outputSample, 0.99f);

                source.DisconnectOutput();
                unreliableFilter.DisconnectOutput();
                buffer.DisconnectOutput();
            }
        }

        [TestMethod]
        public async Task TestAudioBufferWriteOnly()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.7f))
            using (AudioSampleBuffer buffer = new AudioSampleBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(500)))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(buffer);
                buffer.ConnectOutput(bucket);

                IRandom random = new FastRandom(4345);
                int samplesRead = 0;
                int samplesToRead = 48000 * 10;
                while (samplesRead < samplesToRead)
                {
                    int toWrite = Math.Min(samplesToRead - samplesRead, random.NextInt(1, 40000));
                    await source.WriteSamplesToOutput(toWrite, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    samplesRead += toWrite;
                }

                await buffer.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                AudioSample outputSample = bucket.GetAllAudio();
                float[] expectedAudioData = new float[samplesToRead];
                AudioTestHelpers.GenerateSineWave(expectedAudioData, format.SampleRateHz, 440, samplesToRead, 0, 0, 1, 0.7f, 0.0f);
                AudioSample expectedAudio = new AudioSample(expectedAudioData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedAudio, outputSample, 0.99f);

                source.DisconnectOutput();
                buffer.DisconnectOutput();
            }
        }

        [TestMethod]
        public async Task TestAudioBufferReadWrite()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] expectedAudioData = new float[format.SampleRateHz * format.NumChannels * 10];
            AudioTestHelpers.GenerateSineWave(expectedAudioData, format.SampleRateHz, 440, format.SampleRateHz * format.NumChannels, 0, 0, format.NumChannels, 0.7f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedAudioData, format.SampleRateHz, 440, format.SampleRateHz * format.NumChannels, 0, 1, format.NumChannels, 0.7f, 0.5f);
            AudioSample inputSample = new AudioSample(expectedAudioData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (AudioSampleBuffer buffer = new AudioSampleBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(100)))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(buffer);
                buffer.ConnectOutput(bucket);

                // Now do random reads and writes
                IRandom random = new FastRandom(6345);
                while (!source.PlaybackFinished)
                {
                    await bucket.ReadSamplesFromInput(random.NextInt(1, 4000), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await source.WriteSamplesToOutput(random.NextInt(1, 4000), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await buffer.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                AudioSample expectedAudio = new AudioSample(expectedAudioData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedAudio, bucket.GetAllAudio());

                source.DisconnectOutput();
                buffer.DisconnectOutput();
            }
        }

        [TestMethod]
        public async Task TestAudioBufferUnreliableReadWrite()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] expectedAudioData = new float[format.SampleRateHz * format.NumChannels * 10];
            AudioTestHelpers.GenerateSineWave(expectedAudioData, format.SampleRateHz, 440, format.SampleRateHz * format.NumChannels, 0, 0, format.NumChannels, 0.7f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedAudioData, format.SampleRateHz, 440, format.SampleRateHz * format.NumChannels, 0, 1, format.NumChannels, 0.7f, 0.5f);
            AudioSample inputSample = new AudioSample(expectedAudioData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(4323), 0.5f))
            using (AudioSampleBuffer buffer = new AudioSampleBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(100)))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(unreliableFilter);
                unreliableFilter.ConnectOutput(buffer);
                buffer.ConnectOutput(bucket);

                // Now do random reads and writes
                IRandom random = new FastRandom(995333);
                while (!source.PlaybackFinished)
                {
                    await bucket.ReadSamplesFromInput(random.NextInt(1, 4000), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await source.WriteSamplesToOutput(random.NextInt(1, 4000), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await buffer.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                AudioSample expectedAudio = new AudioSample(expectedAudioData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedAudio, bucket.GetAllAudio());

                source.DisconnectOutput();
                unreliableFilter.DisconnectOutput();
                buffer.DisconnectOutput();
            }
        }

        [TestMethod]
        public async Task TestAudioBufferPrebufferShortSample()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[960]; // 10ms input sample
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 480, 0, 0, format.NumChannels, 0.7f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 480, 0, 1, format.NumChannels, 0.7f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (AudioSampleBuffer buffer = new AudioSampleBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(500), true))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(buffer);
                buffer.ConnectOutput(bucket);

                int attempts = 0;
                int samplesRead = 0;
                while (samplesRead >= 0)
                {
                    samplesRead = await bucket.ReadSamplesFromInput(48, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreNotEqual(0, samplesRead);
                    Assert.IsTrue(attempts++ < 1000);
                }

                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, bucket.GetAllAudio());

                source.DisconnectOutput();
                buffer.DisconnectOutput();
            }
        }

        [TestMethod]
        public async Task TestAudioBufferPrebufferLongSample()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[96000]; // 1000ms input sample
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48000, 0, 0, format.NumChannels, 0.7f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48000, 0, 1, format.NumChannels, 0.7f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (AudioThrottlingFilter throttle = new AudioThrottlingFilter(new WeakPointer<IAudioGraph>(graph), format))
            using (AudioSampleBuffer buffer = new AudioSampleBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(500), true))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(throttle);
                throttle.ConnectOutput(buffer);
                buffer.ConnectOutput(bucket);

                for (int c = 0; c < 4; c++)
                {
                    throttle.AllowMoreAudio(TimeSpan.FromMilliseconds(100));
                    Assert.AreEqual(0, await bucket.ReadSamplesFromInput(48000, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                }

                throttle.AllowMoreAudio(TimeSpan.FromMilliseconds(100));
                Assert.AreEqual(24000, await bucket.ReadSamplesFromInput(48000, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                throttle.LengthOfAudioToAllow = TimeSpan.FromSeconds(10);
                Assert.AreEqual(24000, await bucket.ReadSamplesFromInput(48000, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, bucket.GetAllAudio());

                source.DisconnectOutput();
                buffer.DisconnectOutput();
            }
        }

        [TestMethod]
        public async Task TestAudioBufferPrebufferLongSampleSmallReads()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] sampleData = new float[96000]; // 1000ms input sample
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48000, 0, 0, format.NumChannels, 0.7f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, format.SampleRateHz, 440, 48000, 0, 1, format.NumChannels, 0.7f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            IRandom rand = new FastRandom(5428832);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (AudioThrottlingFilter throttle = new AudioThrottlingFilter(new WeakPointer<IAudioGraph>(graph), format))
            using (AudioSampleBuffer buffer = new AudioSampleBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(500), true))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(throttle);
                throttle.ConnectOutput(buffer);
                buffer.ConnectOutput(bucket);

                while (!buffer.PlaybackFinished)
                {
                    throttle.AllowMoreAudio(TimeSpan.FromMilliseconds(1));
                    await bucket.ReadSamplesFromInput(rand.NextInt(40, 60), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, bucket.GetAllAudio());

                source.DisconnectOutput();
                buffer.DisconnectOutput();
            }
        }

        [TestMethod]
        public async Task TestAudioDelayBufferBasic()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 500, 48000 * 2, 48000, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 500, 48000 * 2, 48000, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 500, 48000 * 2, 52800, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 500, 48000 * 2, 52800, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample expectedSample = new AudioSample(expectedSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioDelayBuffer filter = new AudioDelayBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(100)))
            {
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), filter, filter, inputSample, 4311178);
                AudioTestHelpers.AssertSamplesAreSimilar(outputSample, expectedSample, 0.99f);
            }
        }

        [TestMethod]
        public async Task TestAudioDelayBufferReadFromEmptyBuffer()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioDelayBuffer filter = new AudioDelayBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(100)))
            {
                float[] scratch = new float[100];
                int samplesRead = await filter.ReadAsync(scratch, 0, 100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(0, samplesRead);
            }
        }

        [TestMethod]
        public async Task TestAudioFloatingDelayBufferConstantDelay()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 500, 48000 * 3, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 500, 48000 * 3, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 500, 48000 * 3, 4800, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 500, 48000 * 3, 4800, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample expectedSample = new AudioSample(expectedSampleData, format);

            IRandom rand = new FastRandom(23452665);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioFloatingDelayBuffer filter = new AudioFloatingDelayBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100)))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (SimulatedUnreliableAudioSource unreliability = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.3f))
            using (PollutedBufferPipe bufferPolluter1 = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (PollutedBufferPipe bufferPolluter2 = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(unreliability);
                unreliability.ConnectOutput(bufferPolluter1);
                bufferPolluter1.ConnectOutput(filter);
                filter.ConnectOutput(bufferPolluter2);
                bufferPolluter2.ConnectOutput(target);

                while (!source.PlaybackFinished)
                {
                    await target.ReadSamplesFromInput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await source.WriteSamplesToOutput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await filter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                source.DisconnectOutput();
                target.DisconnectInput();
                AudioSample outputSample = target.GetAllAudio();

                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(outputSample, expectedSample, 0.99f);
            }
        }

        [TestMethod]
        public async Task TestAudioFloatingDelayBufferConstantDelayLargeReadWrites()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 12];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 500, 48000 * 11, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 500, 48000 * 11, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 12];
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 500, 48000 * 11, 4800, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 500, 48000 * 11, 4800, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample expectedSample = new AudioSample(expectedSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioFloatingDelayBuffer filter = new AudioFloatingDelayBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100)))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(filter);
                filter.ConnectOutput(target);

                while (!source.PlaybackFinished)
                {
                    await target.ReadSamplesFromInput(3 * format.SampleRateHz, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await source.WriteSamplesToOutput(3 * format.SampleRateHz, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await filter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                source.DisconnectOutput();
                target.DisconnectInput();
                AudioSample outputSample = target.GetAllAudio();

                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(outputSample, expectedSample, 0.99f);
            }
        }

        [TestMethod]
        public async Task TestAudioFloatingDelayBufferIncreasingDelay()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 480, 48000 * 3, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 480, 48000 * 3, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, 48200, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, 48200, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 472.3503f, 97387 - 48200, 48200, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 472.3503f, 97387 - 48200, 48200, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, 144826 - 97387, 97387, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, 144826 - 97387, 97387, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample expectedSample = new AudioSample(expectedSampleData, format);

            IRandom rand = new FastRandom(1611275);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioFloatingDelayBuffer filter = new AudioFloatingDelayBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(100)))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (SimulatedUnreliableAudioSource unreliability = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.3f))
            using (PollutedBufferPipe bufferPolluter1 = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (PollutedBufferPipe bufferPolluter2 = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(unreliability);
                unreliability.ConnectOutput(bufferPolluter1);
                bufferPolluter1.ConnectOutput(filter);
                filter.ConnectOutput(bufferPolluter2);
                bufferPolluter2.ConnectOutput(target);

                // 1 second of zero delay
                int samplesRead = 0;
                while (samplesRead < 48000)
                {
                    int amountToAdvance = Math.Min(48000 - samplesRead, rand.NextInt(1, 50) * rand.NextInt(1, 50));
                    if (rand.NextBool())
                    {
                        samplesRead += await target.ReadSamplesFromInput(amountToAdvance, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }
                    else
                    {
                        samplesRead += await source.WriteSamplesToOutput(amountToAdvance, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }
                }

                // And then augment the rest
                filter.AlgorithmicDelay = TimeSpan.FromMilliseconds(10);
                while (!source.PlaybackFinished)
                {
                    await target.ReadSamplesFromInput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await source.WriteSamplesToOutput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await filter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                source.DisconnectOutput();
                target.DisconnectInput();
                AudioSample outputSample = target.GetAllAudio();
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(outputSample, expectedSample, 0.975f, 400);
            }
        }

        [TestMethod]
        public async Task TestAudioFloatingDelayBufferIncreasingDelayLargeReadWrites()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 480, 48000 * 3, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 480, 48000 * 3, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            int point1 = 48000;
            int point2 = 95980;
            int point3 = 144480;
            float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, point1, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, point1, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 475.2021f, point2 - point1, point1, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 475.2021f, point2 - point1, point1, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, point3 - point2, point2, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, point3 - point2, point2, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample expectedSample = new AudioSample(expectedSampleData, format);

            IRandom rand = new FastRandom(752465434);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioFloatingDelayBuffer filter = new AudioFloatingDelayBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(100)))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(filter);
                filter.ConnectOutput(target);

                // 1 second of zero delay
                int samplesRead = 0;
                while (samplesRead < 48000)
                {
                    int amountToAdvance = Math.Min(48000 - samplesRead, rand.NextInt(1, 50) * rand.NextInt(1, 50));
                    if (rand.NextBool())
                    {
                        samplesRead += await target.ReadSamplesFromInput(amountToAdvance, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }
                    else
                    {
                        samplesRead += await source.WriteSamplesToOutput(amountToAdvance, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }
                }

                // And then augment the rest
                filter.AlgorithmicDelay = TimeSpan.FromMilliseconds(10);
                while (!source.PlaybackFinished)
                {
                    await target.ReadSamplesFromInput(37100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await source.WriteSamplesToOutput(37100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await filter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                source.DisconnectOutput();
                target.DisconnectInput();
                AudioSample outputSample = target.GetAllAudio();
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(outputSample, expectedSample, 0.99f, 400);
            }
        }

        [TestMethod]
        public async Task TestAudioFloatingDelayBufferIncreasingDelayMono()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 480, 48000 * 3, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, 48200, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 472.3503f, 98708 - 48200, 48200, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, 144826 - 98708, 98708, 0, format.NumChannels, 0.4f, 0.0f);
            AudioSample expectedSample = new AudioSample(expectedSampleData, format);

            IRandom rand = new FastRandom(684542182);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioFloatingDelayBuffer filter = new AudioFloatingDelayBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(100)))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (SimulatedUnreliableAudioSource unreliability = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.3f, 0.0f))
            using (PollutedBufferPipe bufferPolluter1 = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (PollutedBufferPipe bufferPolluter2 = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(unreliability);
                unreliability.ConnectOutput(bufferPolluter1);
                bufferPolluter1.ConnectOutput(filter);
                filter.ConnectOutput(bufferPolluter2);
                bufferPolluter2.ConnectOutput(target);

                // 1 second of zero delay
                int samplesRead = 0;
                while (samplesRead < 48000)
                {
                    int amountToAdvance = Math.Min(48000 - samplesRead, rand.NextInt(1, 50) * rand.NextInt(1, 50));
                    if (rand.NextBool())
                    {
                        samplesRead += await target.ReadSamplesFromInput(amountToAdvance, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }
                    else
                    {
                        samplesRead += await source.WriteSamplesToOutput(amountToAdvance, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }
                }

                // And then augment the rest
                filter.AlgorithmicDelay = TimeSpan.FromMilliseconds(10);
                while (!source.PlaybackFinished)
                {
                    await target.ReadSamplesFromInput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await source.WriteSamplesToOutput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await filter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                source.DisconnectOutput();
                target.DisconnectInput();
                AudioSample outputSample = target.GetAllAudio();
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(outputSample, expectedSample, 0.99f, 400);
            }
        }

        [TestMethod]
        public async Task TestAudioFloatingDelayBufferDecreasingDelay()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 480, 48000 * 3, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 480, 48000 * 3, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, 48476 - 480, 480, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, 48476 - 480, 480, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 483.5771f, 113000 - 48476, 48080, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 483.5771f, 113000 - 48476, 48080, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, 144000 - 113000, 109800, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 480, 144000 - 113000, 109800, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample expectedSample = new AudioSample(expectedSampleData, format);

            IRandom rand = new FastRandom(947527173);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioFloatingDelayBuffer filter = new AudioFloatingDelayBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(100)))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (SimulatedUnreliableAudioSource unreliability = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.3f))
            using (PollutedBufferPipe bufferPolluter1 = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (PollutedBufferPipe bufferPolluter2 = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(unreliability);
                unreliability.ConnectOutput(bufferPolluter1);
                bufferPolluter1.ConnectOutput(filter);
                filter.ConnectOutput(bufferPolluter2);
                bufferPolluter2.ConnectOutput(target);

                // 1 second of high delay
                int samplesRead = 0;
                while (samplesRead < 48000)
                {
                    int amountToRead = Math.Min(48000 - samplesRead, rand.NextInt(1, 50));
                    samplesRead += await target.ReadSamplesFromInput(amountToRead, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                // And then augment the rest
                filter.AlgorithmicDelay = TimeSpan.FromMilliseconds(0);
                while (!source.PlaybackFinished)
                {
                    await target.ReadSamplesFromInput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await source.WriteSamplesToOutput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await filter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                source.DisconnectOutput();
                target.DisconnectInput();
                AudioSample outputSample = target.GetAllAudio();
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
                AudioTestHelpers.AssertSamplesAreSimilar(outputSample, expectedSample, 0.97f);
            }
        }

        [TestMethod]
        public async Task TestAudioFloatingDelayBufferRandomDelay()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            int numSecondsOfAudio = 10;
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * numSecondsOfAudio];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 1000, 48000 * numSecondsOfAudio, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 1000, 48000 * numSecondsOfAudio, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            IRandom rand = new FastRandom(654478);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioFloatingDelayBuffer filter = new AudioFloatingDelayBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(100)))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (SimulatedUnreliableAudioSource unreliability = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.3f))
            using (PollutedBufferPipe bufferPolluter1 = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (PollutedBufferPipe bufferPolluter2 = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(unreliability);
                unreliability.ConnectOutput(bufferPolluter1);
                bufferPolluter1.ConnectOutput(filter);
                filter.ConnectOutput(bufferPolluter2);
                bufferPolluter2.ConnectOutput(target);

                while (!source.PlaybackFinished)
                {
                    if (rand.NextInt(0, 10) == 0)
                    {
                        filter.AlgorithmicDelay = TimeSpan.FromMilliseconds(rand.NextFloat() * 20);
                    }

                    await target.ReadSamplesFromInput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    await source.WriteSamplesToOutput(rand.NextInt(1, 50) * rand.NextInt(1, 50), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await filter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                source.DisconnectOutput();
                target.DisconnectInput();
                AudioSample outputSample = target.GetAllAudio();
                AudioTestHelpers.AssertAudioSignalIsContinuous(outputSample);
            }
        }

        [TestMethod]
        public async Task TestAudioPeekBuffer()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            FastRandom rand = new FastRandom(6462222);

            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 10];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 20, 48000 * 10, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 20, 48000 * 10, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            float[] peekBufferData = new float[480 * format.NumChannels];

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (NullAudioSampleTarget sink = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (AudioPeekBuffer peekBuffer = new AudioPeekBuffer(new WeakPointer<IAudioGraph>(graph), format, null, TimeSpan.FromMilliseconds(10), rand))
            {
                source.ConnectOutput(peekBuffer);
                peekBuffer.ConnectOutput(sink);

                while (!source.PlaybackFinished)
                {
                    if (rand.NextBool())
                    {
                        await source.WriteSamplesToOutput(rand.NextInt(1, 4000), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }
                    else
                    {
                        await sink.ReadSamplesFromInput(rand.NextInt(1, 4000), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    }

                    int actualOutputLength;
                    long peekTimestamp;
                    peekBuffer.PeekAtBuffer(peekBufferData, 0, 480, out actualOutputLength, out peekTimestamp);

                    int numberOfValidSamples = peekTimestamp < 0 ? (int)(peekTimestamp + actualOutputLength) : actualOutputLength;
                    if (numberOfValidSamples > 0)
                    {
                        float[] fromPeek = new float[numberOfValidSamples * format.NumChannels];
                        Array.Copy(peekBufferData, Math.Max(0, 0 - peekTimestamp) * format.NumChannels, fromPeek, 0, numberOfValidSamples * format.NumChannels);
                        float[] expected = new float[numberOfValidSamples * format.NumChannels];
                        Array.Copy(inputSampleData, Math.Max(0, peekTimestamp) * format.NumChannels, expected, 0, numberOfValidSamples * format.NumChannels);
                        Assert.IsTrue(ArrayExtensions.ArrayEquals(fromPeek, expected));
                    }
                }
            }
        }
    }
}
