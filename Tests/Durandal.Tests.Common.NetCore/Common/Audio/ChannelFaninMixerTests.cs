using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
using Durandal.Common.Events;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Test;
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
using Durandal.Common.Audio.Test;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    public class ChannelFaninMixerTests
    {
        [TestMethod]
        public async Task TestChannelFaninMixerPassthrough()
        {
            for (int c = 0; c < 100; c++)
            {
                AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
                float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
                AudioSample inputSample = new AudioSample(sampleData, format);

                using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (ChannelFaninMixer filter = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
                using (PassthroughAudioPipe pipe = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    filter.AddInput(pipe, null, false, 0, 1);
                    AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(graph, pipe, filter, inputSample, c);
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                }
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerPushOnly()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] shortSampleData = new float[format.SampleRateHz * format.NumChannels * 2];
            float[] longSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);

            AudioSample shortSample = new AudioSample(shortSampleData, format);
            AudioSample longSample = new AudioSample(longSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource shortSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), shortSample, null))
            using (FixedAudioSampleSource longSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), longSample, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(shortSampleSource, null, false, 0, 1);
                mixer.AddInput(longSampleSource, null, false, 0, 1);
                mixer.ConnectOutput(target);

                IRandom rand = new FastRandom(9704103);
                int totalSamplesRead = 0;
                while (totalSamplesRead < 48000 * 4)
                {
                    totalSamplesRead += await longSampleSource.WriteSamplesToOutput(rand.NextInt(1, 999), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioSample actualOutput = target.GetAllAudio();
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, actualOutput);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerPushWithDisconnectedOutput()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource sampleSource = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 1.0f))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            {
                mixer.AddInput(sampleSource, null, false, 0, 1);
                await sampleSource.WriteSamplesToOutput(1000, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerBasic()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] shortSampleData = new float[format.SampleRateHz * format.NumChannels * 2];
            float[] longSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);

            AudioSample shortSample = new AudioSample(shortSampleData, format);
            AudioSample longSample = new AudioSample(longSampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource shortSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), shortSample, null))
            using (FixedAudioSampleSource longSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), longSample, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(shortSampleSource, null, false, 0, 1);
                mixer.AddInput(longSampleSource, null, false, 0, 1);
                mixer.ConnectOutput(target);

                IRandom rand = new FastRandom(9704103);
                int totalSamplesRead = 0;
                while (totalSamplesRead < 48000 * 4)
                {
                    int firstRead = await target.ReadSamplesFromInput(rand.NextInt(1, 999), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreNotEqual(0, firstRead); // prevent infinite loops
                    totalSamplesRead += firstRead;
                    totalSamplesRead += await shortSampleSource.WriteSamplesToOutput(rand.NextInt(1, 999), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    totalSamplesRead += await longSampleSource.WriteSamplesToOutput(rand.NextInt(1, 999), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioSample actualOutput = target.GetAllAudio();
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, actualOutput);
            }
        }

        [TestMethod]
        public void TestChannelFaninMixerInvalidInputs()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SilenceAudioSampleSource unknownMappingSource = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), new AudioSampleFormat(48000, 2, MultiChannelMapping.Unknown), null))
            using (SilenceAudioSampleSource wrongSampleRateSource = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Stereo(96000), null))
            using (SilenceAudioSampleSource correctSource = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), format, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            {
                try
                {
                    mixer.AddInput(unknownMappingSource, null, false, 0, 1);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }

                try
                {
                    mixer.AddInput(wrongSampleRateSource, null, false, 0, 1);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }

                try
                {
                    mixer.AddInput(correctSource, null, false, 0);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }

                try
                {
                    mixer.AddInput(correctSource, null, false, 2, 0);
                    Assert.Fail("Expected an ArgumentException");
                }
                catch (ArgumentException) { }
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerFiniteStream()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] shortSampleData = new float[format.SampleRateHz * format.NumChannels * 2];
            float[] longSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);

            AudioSample shortSample = new AudioSample(shortSampleData, format);
            AudioSample longSample = new AudioSample(longSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource shortSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), shortSample, null))
            using (FixedAudioSampleSource longSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), longSample, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.ConnectOutput(target);

                // Reads from an initially disconnected mixer should return zero
                Assert.AreEqual(0, await target.ReadSamplesFromInput(48000 * 4, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                Assert.AreEqual(0, await target.ReadSamplesFromInput(48000 * 4, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                mixer.AddInput(shortSampleSource, null, false, 0, 1);
                mixer.AddInput(longSampleSource, null, false, 0, 1);

                int samplesRead = await target.ReadSamplesFromInput(48000 * 4, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(48000 * 4, samplesRead);
                samplesRead = await target.ReadSamplesFromInput(48000, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(-1, samplesRead);
                Assert.IsTrue(mixer.PlaybackFinished);

                float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioSample actualOutput = target.GetAllAudio();
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, actualOutput);

                Assert.AreEqual(-1, await target.ReadSamplesFromInput(48000 * 4, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                shortSampleSource.DisconnectOutput();
                longSampleSource.DisconnectOutput();
                Assert.AreEqual(-1, await target.ReadSamplesFromInput(48000 * 4, CancellationToken.None, DefaultRealTimeProvider.Singleton));
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerInfiniteStream()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] shortSampleData = new float[format.SampleRateHz * format.NumChannels * 2];
            float[] longSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);

            AudioSample shortSample = new AudioSample(shortSampleData, format);
            AudioSample longSample = new AudioSample(longSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource shortSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), shortSample, null))
            using (FixedAudioSampleSource longSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), longSample, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, true))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(shortSampleSource, null, false, 0, 1);
                mixer.AddInput(longSampleSource, null, false, 0, 1);
                mixer.ConnectOutput(target);

                int samplesRead = await target.ReadSamplesFromInput(48000 * 4, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(48000 * 4, samplesRead);
                samplesRead = await target.ReadSamplesFromInput(48000, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(48000, samplesRead);
                Assert.IsFalse(mixer.PlaybackFinished);

                float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 5];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioSample actualOutput = target.GetAllAudio();
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, actualOutput);

                // Successive reads should return silence
                Assert.AreEqual(20, await target.ReadSamplesFromInput(20, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                shortSampleSource.DisconnectOutput();
                longSampleSource.DisconnectOutput();
                Assert.AreEqual(20, await target.ReadSamplesFromInput(20, CancellationToken.None, DefaultRealTimeProvider.Singleton));
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerChannelFinishedEventsFireOnPullGraph()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] shortSampleData = new float[format.SampleRateHz * format.NumChannels * 2];
            float[] longSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);

            AudioSample shortSample = new AudioSample(shortSampleData, format);
            AudioSample longSample = new AudioSample(longSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource shortSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), shortSample, null))
            using (FixedAudioSampleSource longSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), longSample, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(shortSampleSource, "channel_one", false, 0, 1);
                mixer.AddInput(longSampleSource, "channel_two", false, 0, 1);
                mixer.ConnectOutput(target);

                EventRecorder<PlaybackFinishedEventArgs> recorder = new EventRecorder<PlaybackFinishedEventArgs>();
                mixer.ChannelFinishedEvent.Subscribe(recorder.HandleEventAsync);

                LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                target.BeginActivelyReading(logger.Clone("AudioRead"), realTime, true);
                realTime.Step(TimeSpan.FromMilliseconds(1900));

                RetrieveResult<CapturedEvent<PlaybackFinishedEventArgs>> rr = await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsFalse(rr.Success);
                realTime.Step(TimeSpan.FromMilliseconds(200), 50);

                rr = await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(rr.Success);
                Assert.AreEqual("channel_one", rr.Result.Args.ChannelToken);
                rr = await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsFalse(rr.Success);
                realTime.Step(TimeSpan.FromMilliseconds(2000));
                rr = await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(rr.Success);
                Assert.AreEqual("channel_two", rr.Result.Args.ChannelToken);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerChannelFinishedEventsDontFireIfNoChannelToken()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] shortSampleData = new float[format.SampleRateHz * format.NumChannels * 2];
            float[] longSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(shortSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);

            AudioSample shortSample = new AudioSample(shortSampleData, format);
            AudioSample longSample = new AudioSample(longSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource shortSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), shortSample, null))
            using (FixedAudioSampleSource longSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), longSample, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(shortSampleSource, null, false, 0, 1);
                mixer.AddInput(longSampleSource, null, false, 0, 1);
                mixer.ConnectOutput(target);

                EventRecorder<PlaybackFinishedEventArgs> recorder = new EventRecorder<PlaybackFinishedEventArgs>();
                mixer.ChannelFinishedEvent.Subscribe(recorder.HandleEventAsync);

                LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                target.BeginActivelyReading(logger.Clone("AudioRead"), realTime, true);
                realTime.Step(TimeSpan.FromMilliseconds(4000), 1000);

                RetrieveResult<CapturedEvent<PlaybackFinishedEventArgs>> rr = await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsFalse(rr.Success);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerCannotAdvanceIfSourceReturnsZero()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source1 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.3f))
            using (SineWaveSampleSource source2 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 700, 0.3f))
            using (SimulatedUnreliableAudioSource filter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(), 1.0f, 0.0f))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, true))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(source1, null, false, 0, 1);
                mixer.AddInput(filter, null, false, 0, 1);
                filter.ConnectInput(source2);
                mixer.ConnectOutput(target);

                int samplesRead;
                for (int loop = 0; loop < 10; loop++)
                {
                    samplesRead = await target.ReadSamplesFromInput(1000, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual(0, samplesRead);
                    Assert.IsFalse(mixer.PlaybackFinished);
                }

                filter.DisconnectInput();
                source2.ConnectOutput(filter.Output);

                samplesRead = await target.ReadSamplesFromInput(1000, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreNotEqual(0, samplesRead);
                Assert.IsFalse(mixer.PlaybackFinished);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerPushWithUnreliableInputs()
        {
            ILogger logger = new ConsoleLogger();
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            IRandom rand = new FastRandom(564564);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source1 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (SineWaveSampleSource source2 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 550, 0.2f))
            using (SineWaveSampleSource source3 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 660, 0.2f))
            using (SineWaveSampleSource source4 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 770, 0.2f))
            using (SimulatedUnreliableAudioSource filter2 = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.2f)) // Each input has a 20% chance of returning empty on read
            using (SimulatedUnreliableAudioSource filter3 = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.2f))
            using (SimulatedUnreliableAudioSource filter4 = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.2f))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source2.ConnectOutput(filter2);
                source3.ConnectOutput(filter3);
                source4.ConnectOutput(filter4);

                mixer.AddInput(source1, null, false, 0, 1);
                mixer.AddInput(filter2, null, false, 0, 1);
                mixer.AddInput(filter3, null, false, 0, 1);
                mixer.AddInput(filter4, null, false, 0, 1);
                mixer.ConnectOutput(target);

                int totalSamplesRead = 0;
                for (int c = 0; c < 1000; c++)
                {
                    int toRead = rand.NextInt(1, 480);
                    await source1.WriteSamplesToOutput(toRead, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    totalSamplesRead += toRead;
                }

                // Because there might be samples still stuck in various mixer buffers, we turn reliability back to 100% to flush it out
                filter2.ChanceOfFailure = 0;
                filter3.ChanceOfFailure = 0;
                filter4.ChanceOfFailure = 0;

                await source1.WriteSamplesToOutput(24000, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                totalSamplesRead += 24000;

                float[] expectedSampleData = new float[totalSamplesRead * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, totalSamplesRead, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, totalSamplesRead, 0, 1, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 550, totalSamplesRead, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 550, totalSamplesRead, 0, 1, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 660, totalSamplesRead, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 660, totalSamplesRead, 0, 1, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 770, totalSamplesRead, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 770, totalSamplesRead, 0, 1, format.NumChannels, 0.2f, 0.0f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioSample actualOutput = target.GetAllAudio();
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, actualOutput, 0.998f); // The output is not quite exact. I assume this is a numerical stability issue and not a glitch
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerPullWithUnreliableInputs()
        {
            ILogger logger = new ConsoleLogger();
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            IRandom rand = new FastRandom(64288);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source1 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (SineWaveSampleSource source2 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 550, 0.2f))
            using (SineWaveSampleSource source3 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 660, 0.2f))
            using (SineWaveSampleSource source4 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 770, 0.2f))
            using (SimulatedUnreliableAudioSource filter1 = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.2f))
            using (SimulatedUnreliableAudioSource filter2 = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.2f)) // Each input has a 20% chance of returning empty on read
            using (SimulatedUnreliableAudioSource filter3 = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.2f))
            using (SimulatedUnreliableAudioSource filter4 = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.2f))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source1.ConnectOutput(filter1);
                source2.ConnectOutput(filter2);
                source3.ConnectOutput(filter3);
                source4.ConnectOutput(filter4);

                mixer.AddInput(filter1, null, false, 0, 1);
                mixer.AddInput(filter2, null, false, 0, 1);
                mixer.AddInput(filter3, null, false, 0, 1);
                mixer.AddInput(filter4, null, false, 0, 1);
                mixer.ConnectOutput(target);

                int totalSamplesRead = 0;
                for (int c = 0; c < 1000; c++)
                {
                    totalSamplesRead += await target.ReadSamplesFromInput(rand.NextInt(1, 480), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                float[] expectedSampleData = new float[totalSamplesRead * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, totalSamplesRead, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, totalSamplesRead, 0, 1, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 550, totalSamplesRead, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 550, totalSamplesRead, 0, 1, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 660, totalSamplesRead, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 660, totalSamplesRead, 0, 1, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 770, totalSamplesRead, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 770, totalSamplesRead, 0, 1, format.NumChannels, 0.2f, 0.0f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioSample actualOutput = target.GetAllAudio();
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, actualOutput, 0.998f); // The output is not quite exact. I assume this is a numerical stability issue and not a glitch
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerFlushesPushBuffersProperlyWithUnreliableInputs()
        {
            ILogger logger = new ConsoleLogger();
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            IRandom rand = new FastRandom(5287);

            // Source 1 is reliable, source 2 is not
            float[] source1Sample = new float[4800 * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(source1Sample, format.SampleRateHz, 600, 4800, 0, 0, format.NumChannels, 0.5f, 0.0f);
            AudioTestHelpers.GenerateSineWave(source1Sample, format.SampleRateHz, 600, 4800, 0, 1, format.NumChannels, 0.5f, 0.5f);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source1 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), new AudioSample(source1Sample, format), null))
            using (SineWaveSampleSource source2 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 550, 0.2f))
            using (SimulatedUnreliableAudioSource filter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 1.0f, 0.0f))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source2.ConnectOutput(filter);
                mixer.AddInput(source1, null, false, 0, 1);
                mixer.AddInput(filter, null, false, 0, 1);
                mixer.ConnectOutput(target);

                // Push 100ms of audio (the full sample 1) to the mixer
                await source1.WriteSamplesToOutput(4800, CancellationToken.None, DefaultRealTimeProvider.Singleton);

                // Assert that the mixer didn't output anything (since source 2 is unreliable)
                // This means that all of source 1's output should be in the mixer buffer
                Assert.AreEqual(0, target.SamplesPerChannelInBucket);

                // Now make source 2 reliable, and flush.
                // This should cause the mixer to output 4800 mixed samples to output
                filter.ChanceOfFailure = 0;
                await source1.Output.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                Assert.AreEqual(4800, target.SamplesPerChannelInBucket);
                float[] expectedOutputData = new float[4800 * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 600, 4800, 0, 0, format.NumChannels, 0.5f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 600, 4800, 0, 1, format.NumChannels, 0.5f, 0.5f);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 550, 4800, 0, 0, format.NumChannels, 0.2f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedOutputData, format.SampleRateHz, 550, 4800, 0, 1, format.NumChannels, 0.2f, 0.0f);
                AudioSample expectedOutput = new AudioSample(expectedOutputData, format);
                AudioSample actualOutput = target.GetAllAudio();
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, actualOutput, 0.999f);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerZeroesOutTargetBufferProperlyOnPull()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource audioSource = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 1.0f))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, true))
            {
                mixer.AddInput(audioSource, null, false, 0);

                float[] pollutedTargetBuf = new float[960];
                AudioTestHelpers.GenerateSineWave(pollutedTargetBuf, format.SampleRateHz, 1200, 960, 0, 0, format.NumChannels, 0.5f, 0.0f);
                await mixer.ReadAsync(pollutedTargetBuf, 0, 960, CancellationToken.None, DefaultRealTimeProvider.Singleton);

                float[] expectedOutputBuf = new float[960];
                AudioTestHelpers.GenerateSineWave(expectedOutputBuf, format.SampleRateHz, 440, 960, 0, 0, format.NumChannels, 1.0f, 0.0f);

                AudioSample output = new AudioSample(pollutedTargetBuf, format);
                AudioSample expectedOutput = new AudioSample(expectedOutputBuf, format);
                AudioTestHelpers.AssertSamplesAreSimilar(output, expectedOutput, 0.999f);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerZeroesOutTargetBufferProperlyOnPush()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            float[] inputSampleData = new float[960];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 960, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource audioSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (PollutedBufferPipe pipe = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, true))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                pipe.ConnectInput(audioSource);
                mixer.AddInput(pipe, null, false, 0);
                mixer.ConnectOutput(bucket);

                await audioSource.WriteSamplesToOutput(960, CancellationToken.None, DefaultRealTimeProvider.Singleton);

                float[] expectedOutputBuf = new float[960];
                AudioTestHelpers.GenerateSineWave(expectedOutputBuf, format.SampleRateHz, 440, 960, 0, 0, format.NumChannels, 1.0f, 0.0f);

                AudioSample expectedOutput = new AudioSample(expectedOutputBuf, format);
                AudioTestHelpers.AssertSamplesAreSimilar(bucket.GetAllAudio(), expectedOutput, 0.999f);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerWorksOnPushAfterInputDisconnected()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            float[] longSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioSample sample = new AudioSample(longSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SilenceAudioSampleSource source1 = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (FixedAudioSampleSource source2 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), sample, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, true))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                mixer.ConnectOutput(target);
                mixer.AddInput(source1, null, false, 0);
                mixer.AddInput(source2, null, false, 0);
                await source2.WriteSamplesToOutput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                source1.DisconnectOutput();
                await source2.WriteSamplesToOutput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerWorksOnPullAfterInputDisconnected()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SilenceAudioSampleSource source1 = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (SilenceAudioSampleSource source2 = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, true))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                mixer.ConnectOutput(target);
                mixer.AddInput(source1, null, false, 0);
                mixer.AddInput(source2, null, false, 0);
                await target.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                source1.DisconnectOutput();
                await target.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerPullFromEmptyMixerReadForever()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, true))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                mixer.ConnectOutput(target);
                int samplesRead = await target.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);

                // Should just pull silence
                Assert.AreEqual(100, samplesRead);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerPullFromEmptyMixerNoReadForever()
        {
            AudioSample emptySample = new AudioSample(BinaryHelpers.EMPTY_FLOAT_ARRAY, AudioSampleFormat.Mono(48000));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), emptySample, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                mixer.ConnectOutput(target);
                int samplesRead;
                for (int c = 0; c < 10; c++)
                {
                    samplesRead = await target.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual(0, samplesRead);
                }

                // Add an input which finishes immediately. This should trigger the mixer's end of stream
                mixer.AddInput(source, null, false, 0);
                samplesRead = await target.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(-1, samplesRead);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerDisposeOfOwnedInputOnPlaybackFinish()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null);
                mixer.ConnectOutput(bucket);
                mixer.AddInput(sampleSource, null, true, 0, 1);
                for (int c = 0; c < 100; c++)
                {
                    await bucket.ReadSamplesFromInput(10000, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                // The sample source should have been read fully and disposed of by now.
                try
                {
                    Assert.IsNull(sampleSource.Output);
                    await sampleSource.ReadAsync(new float[10], 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown an ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }

                AudioSample outputSample = bucket.GetAllAudio();
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerDisposeOfOwnedInputOnMixerDisposal()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null);
                using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
                using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    mixer.ConnectOutput(bucket);
                    mixer.AddInput(sampleSource, null, true, 0, 1);
                }

                // The sample source should have been disposed when the mixer got disposed
                try
                {
                    await sampleSource.ReadAsync(new float[10], 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown an ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }
            }
        }

        [TestMethod]
        public void TestChannelFaninMixerThreadSafety()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testFinishedSource = new CancellationTokenSource(1000);
            CancellationToken testFinishedToken = testFinishedSource.Token;

            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            AudioSample sample = new AudioSample(BinaryHelpers.EMPTY_FLOAT_ARRAY, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 1.0f))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                try
                {
                    source.BeginActivelyWriting(logger.Clone("WriteThread"), DefaultRealTimeProvider.Singleton);
                    IAudioSampleSource previousSource = null;
                    while (!testFinishedToken.IsCancellationRequested)
                    {
                        if (previousSource != null)
                        {
                            previousSource.DisconnectOutput();
                            previousSource.Dispose();
                        }

                        previousSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), sample, null);
                        mixer.AddInput(previousSource, null, false, 0);
                    }
                }
                finally
                {
                    source.StopActivelyWriting();
                }
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerDoesntOverflowWhenAnInputPrunesDuringPush()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);

            float[] inputSampleData = new float[100 * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 48000, 500, 100, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe fixedSourcePassthrough = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (SineWaveSampleSource infiniteSource = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 2351, 0.3f))
            using (ChannelFaninMixer roomMixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (PassthroughAudioPipe output = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                roomMixer.AddInput(infiniteSource, null, false, 0);
                roomMixer.AddInput(fixedSourcePassthrough, null, false, 0);
                roomMixer.ConnectOutput(output);
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(graph, fixedSourcePassthrough, output, inputSample, 7763);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerComposeStereoPull()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat mixerOutputFormat = AudioSampleFormat.Stereo(48000);
            AudioSampleFormat mixerInputFormat = AudioSampleFormat.Mono(48000);
            float[] leftChannelData = new float[mixerInputFormat.SampleRateHz * mixerInputFormat.NumChannels * 2];
            float[] rightChannelData = new float[mixerInputFormat.SampleRateHz * mixerInputFormat.NumChannels * 2];

            AudioTestHelpers.GenerateSineWave(leftChannelData, mixerInputFormat.SampleRateHz, 2000, 48000 * 2, 0, 0, mixerInputFormat.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(rightChannelData, mixerInputFormat.SampleRateHz, 440, 48000 * 2, 0, 0, mixerInputFormat.NumChannels, 0.4f, 0.0f);

            AudioSample leftChannelSample = new AudioSample(leftChannelData, mixerInputFormat);
            AudioSample rightChannelSample = new AudioSample(rightChannelData, mixerInputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource leftSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), leftChannelSample, null))
            using (FixedAudioSampleSource rightSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), rightChannelSample, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), mixerOutputFormat, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), mixerOutputFormat, null))
            {
                mixer.AddInput(leftSampleSource, null, false, 0, -1);
                mixer.AddInput(rightSampleSource, null, false, -1, 0);
                mixer.ConnectOutput(target);

                IRandom rand = new FastRandom(9704103);
                int totalSamplesRead = 0;
                while (totalSamplesRead < 48000 * 2)
                {
                    totalSamplesRead += await target.ReadSamplesFromInput(rand.NextInt(1, 999), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                float[] expectedSampleData = new float[mixerOutputFormat.SampleRateHz * mixerOutputFormat.NumChannels * 2];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, mixerOutputFormat.SampleRateHz, 2000, 48000 * 2, 0, 0, mixerOutputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, mixerOutputFormat.SampleRateHz, 440, 48000 * 2, 0, 1, mixerOutputFormat.NumChannels, 0.4f, 0.0f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, mixerOutputFormat);
                AudioSample actualOutput = target.GetAllAudio();

                using (FileStream dumpStream = new FileStream(@"C:\Code\Durandal\Data\test.wav", FileMode.Create, FileAccess.Write))
                {
                    await AudioHelpers.WriteWaveToStream(actualOutput, dumpStream);
                }

                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, actualOutput);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerComposeStereoPush()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat mixerOutputFormat = AudioSampleFormat.Stereo(48000);
            AudioSampleFormat mixerInputFormat = AudioSampleFormat.Mono(48000);
            float[] leftChannelData = new float[mixerInputFormat.SampleRateHz * mixerInputFormat.NumChannels * 2];
            float[] rightChannelData = new float[mixerInputFormat.SampleRateHz * mixerInputFormat.NumChannels * 2];

            AudioTestHelpers.GenerateSineWave(leftChannelData, mixerInputFormat.SampleRateHz, 2000, 48000 * 2, 0, 0, mixerInputFormat.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(rightChannelData, mixerInputFormat.SampleRateHz, 440, 48000 * 2, 0, 0, mixerInputFormat.NumChannels, 0.4f, 0.0f);

            AudioSample leftChannelSample = new AudioSample(leftChannelData, mixerInputFormat);
            AudioSample rightChannelSample = new AudioSample(rightChannelData, mixerInputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource leftSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), leftChannelSample, null))
            using (FixedAudioSampleSource rightSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), rightChannelSample, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), mixerOutputFormat, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), mixerOutputFormat, null))
            {
                mixer.AddInput(leftSampleSource, null, false, 0, -1);
                mixer.AddInput(rightSampleSource, null, false, -1, 0);
                mixer.ConnectOutput(target);

                IRandom rand = new FastRandom(9704103);
                int totalSamplesRead = 0;
                while (totalSamplesRead < 48000 * 2)
                {
                    totalSamplesRead += await leftSampleSource.WriteSamplesToOutput(rand.NextInt(1, 999), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                float[] expectedSampleData = new float[mixerOutputFormat.SampleRateHz * mixerOutputFormat.NumChannels * 2];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, mixerOutputFormat.SampleRateHz, 2000, 48000 * 2, 0, 0, mixerOutputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, mixerOutputFormat.SampleRateHz, 440, 48000 * 2, 0, 1, mixerOutputFormat.NumChannels, 0.4f, 0.0f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, mixerOutputFormat);
                AudioSample actualOutput = target.GetAllAudio();

                using (FileStream dumpStream = new FileStream(@"C:\Code\Durandal\Data\test.wav", FileMode.Create, FileAccess.Write))
                {
                    await AudioHelpers.WriteWaveToStream(actualOutput, dumpStream);
                }

                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, actualOutput);
            }
        }

        [TestMethod]
        public async Task TestChannelFaninMixerMonoToStereo()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat mixerOutputFormat = AudioSampleFormat.Stereo(48000);
            AudioSampleFormat mixerInputFormat = AudioSampleFormat.Mono(48000);
            float[] inputData = new float[mixerInputFormat.SampleRateHz * mixerInputFormat.NumChannels * 2];

            AudioTestHelpers.GenerateSineWave(inputData, mixerInputFormat.SampleRateHz, 2000, 48000 * 2, 0, 0, mixerInputFormat.NumChannels, 0.4f, 0.0f);

            AudioSample inputSample = new AudioSample(inputData, mixerInputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource inputSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (ChannelFaninMixer mixer = new ChannelFaninMixer(new WeakPointer<IAudioGraph>(graph), mixerOutputFormat, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), mixerOutputFormat, null))
            {
                mixer.AddInput(inputSampleSource, null, false, 0, 0);
                mixer.ConnectOutput(target);

                IRandom rand = new FastRandom(9704103);
                int totalSamplesRead = 0;
                while (totalSamplesRead < 48000 * 2)
                {
                    totalSamplesRead += await target.ReadSamplesFromInput(rand.NextInt(1, 999), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                float[] expectedSampleData = new float[mixerOutputFormat.SampleRateHz * mixerOutputFormat.NumChannels * 2];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, mixerOutputFormat.SampleRateHz, 2000, 48000 * 2, 0, 0, mixerOutputFormat.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, mixerOutputFormat.SampleRateHz, 2000, 48000 * 2, 0, 1, mixerOutputFormat.NumChannels, 0.4f, 0.0f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, mixerOutputFormat);
                AudioSample actualOutput = target.GetAllAudio();

                using (FileStream dumpStream = new FileStream(@"C:\Code\Durandal\Data\test.wav", FileMode.Create, FileAccess.Write))
                {
                    await AudioHelpers.WriteWaveToStream(actualOutput, dumpStream);
                }

                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, actualOutput);
            }
        }
    }
}
