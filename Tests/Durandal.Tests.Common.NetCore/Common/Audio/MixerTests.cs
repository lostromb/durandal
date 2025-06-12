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
    public class MixerTests
    {
        [TestMethod]
        public async Task TestAudioMixerPassthrough()
        {
            for (int c = 0; c < 100; c++)
            {
                AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
                float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
                AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
                AudioSample inputSample = new AudioSample(sampleData, format);

                using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                using (LinearMixer filter = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
                using (PassthroughAudioPipe pipe = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    filter.AddInput(pipe);
                    AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), pipe, filter, inputSample, c);
                    AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                }
            }
        }

        [TestMethod]
        public async Task TestAudioLinearMixerPushOnly()
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
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(shortSampleSource);
                mixer.AddInput(longSampleSource);
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
        public async Task TestAudioLinearMixerPushWithDisconnectedOutput()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource sampleSource = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 1.0f))
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            {
                mixer.AddInput(sampleSource);
                await sampleSource.WriteSamplesToOutput(1000, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        [TestMethod]
        public async Task TestAudioLinearMixerBasic()
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
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(shortSampleSource);
                mixer.AddInput(longSampleSource);
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
        public async Task TestAudioLinearMixerFiniteStream()
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
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.ConnectOutput(target);

                // Reads from an initially disconnected mixer should return zero
                Assert.AreEqual(0, await target.ReadSamplesFromInput(48000 * 4, CancellationToken.None, DefaultRealTimeProvider.Singleton));
                Assert.AreEqual(0, await target.ReadSamplesFromInput(48000 * 4, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                mixer.AddInput(shortSampleSource);
                mixer.AddInput(longSampleSource);

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
        public async Task TestAudioLinearMixerInfiniteStream()
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
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, true))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(shortSampleSource);
                mixer.AddInput(longSampleSource);
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
        public async Task TestAudioLinearMixerChannelFinishedEventsFireOnPullGraph()
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
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(shortSampleSource,"channel_one");
                mixer.AddInput(longSampleSource, "channel_two");
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

        [Ignore] // Channel finished events on async thread are still unreliable
        [TestMethod]
        public async Task TestAudioLinearMixerChannelFinishedEventsFireOnPushGraph()
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
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(shortSampleSource, "channel_one");
                mixer.AddInput(longSampleSource, "channel_two");
                mixer.ConnectOutput(target);

                EventRecorder<PlaybackFinishedEventArgs> recorder = new EventRecorder<PlaybackFinishedEventArgs>();
                mixer.ChannelFinishedEvent.Subscribe(recorder.HandleEventAsync);

                LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                longSampleSource.BeginActivelyWriting(logger.Clone("AudioRead"), realTime, true);
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
                await target.ReadSamplesFromInput(1, CancellationToken.None, realTime);
                rr = await recorder.WaitForEvent(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsTrue(rr.Success);
                Assert.AreEqual("channel_two", rr.Result.Args.ChannelToken);
            }
        }

        [TestMethod]
        public async Task TestAudioLinearMixerChannelFinishedEventsDontFireIfNoChannelToken()
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
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(shortSampleSource);
                mixer.AddInput(longSampleSource);
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
        public async Task TestAudioLinearMixerCannotAdvanceIfSourceReturnsZero()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source1 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.3f))
            using (SineWaveSampleSource source2 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 700, 0.3f))
            using (SimulatedUnreliableAudioSource filter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, new FastRandom(), 1.0f, 0.0f))
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, true))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                mixer.AddInput(source1);
                mixer.AddInput(filter);
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
        public async Task TestAudioLinearMixerPushWithUnreliableInputs()
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
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source2.ConnectOutput(filter2);
                source3.ConnectOutput(filter3);
                source4.ConnectOutput(filter4);

                mixer.AddInput(source1);
                mixer.AddInput(filter2);
                mixer.AddInput(filter3);
                mixer.AddInput(filter4);
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
        public async Task TestAudioLinearMixerPullWithUnreliableInputs()
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
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source1.ConnectOutput(filter1);
                source2.ConnectOutput(filter2);
                source3.ConnectOutput(filter3);
                source4.ConnectOutput(filter4);

                mixer.AddInput(filter1);
                mixer.AddInput(filter2);
                mixer.AddInput(filter3);
                mixer.AddInput(filter4);
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
        public async Task TestAudioLinearMixerFlushesPushBuffersProperlyWithUnreliableInputs()
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
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source2.ConnectOutput(filter);
                mixer.AddInput(source1);
                mixer.AddInput(filter);
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
        public async Task TestAudioLinearMixerZeroesOutTargetBufferProperlyOnPull()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource audioSource = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 1.0f))
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, true))
            {
                mixer.AddInput(audioSource);

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
        public async Task TestAudioLinearMixerZeroesOutTargetBufferProperlyOnPush()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            float[] inputSampleData = new float[960];
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 960, 0, 0, format.NumChannels, 1.0f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource audioSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (PollutedBufferPipe pipe = new PollutedBufferPipe(new WeakPointer<IAudioGraph>(graph), format))
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, true))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                pipe.ConnectInput(audioSource);
                mixer.AddInput(pipe);
                mixer.ConnectOutput(bucket);

                await audioSource.WriteSamplesToOutput(960, CancellationToken.None, DefaultRealTimeProvider.Singleton);

                float[] expectedOutputBuf = new float[960];
                AudioTestHelpers.GenerateSineWave(expectedOutputBuf, format.SampleRateHz, 440, 960, 0, 0, format.NumChannels, 1.0f, 0.0f);

                AudioSample expectedOutput = new AudioSample(expectedOutputBuf, format);
                AudioTestHelpers.AssertSamplesAreSimilar(bucket.GetAllAudio(), expectedOutput, 0.999f);
            }
        }

        [TestMethod]
        public async Task TestAudioLinearMixerWorksOnPushAfterInputDisconnected()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            float[] longSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioSample sample = new AudioSample(longSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SilenceAudioSampleSource source1 = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (FixedAudioSampleSource source2 = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), sample, null))
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, true))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                mixer.ConnectOutput(target);
                mixer.AddInput(source1);
                mixer.AddInput(source2);
                await source2.WriteSamplesToOutput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                source1.DisconnectOutput();
                await source2.WriteSamplesToOutput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        [TestMethod]
        public async Task TestAudioLinearMixerWorksOnPullAfterInputDisconnected()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SilenceAudioSampleSource source1 = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (SilenceAudioSampleSource source2 = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, true))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                mixer.ConnectOutput(target);
                mixer.AddInput(source1);
                mixer.AddInput(source2);
                await target.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                source1.DisconnectOutput();
                await target.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        [TestMethod]
        public async Task TestAudioLinearMixerPullFromEmptyMixerReadForever()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, true))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                mixer.ConnectOutput(target);
                int samplesRead = await target.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);

                // Should just pull silence
                Assert.AreEqual(100, samplesRead);
            }
        }

        [TestMethod]
        public async Task TestAudioLinearMixerPullFromEmptyMixerNoReadForever()
        {
            AudioSample emptySample = new AudioSample(BinaryHelpers.EMPTY_FLOAT_ARRAY, AudioSampleFormat.Mono(48000));
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), emptySample, null))
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, false))
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
                mixer.AddInput(source);
                samplesRead = await target.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(-1, samplesRead);
            }
        }

        [TestMethod]
        public async Task TestAudioMixerDisposeOfOwnedInputOnPlaybackFinish()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null);
                mixer.ConnectOutput(bucket);
                mixer.AddInput(sampleSource, null, true);
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
        public async Task TestAudioMixerDisposeOfOwnedInputOnMixerDisposal()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null);
                using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
                using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    mixer.ConnectOutput(bucket);
                    mixer.AddInput(sampleSource, null, true);
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
        public void TestAudioLinearMixerThreadSafety()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testFinishedSource = new CancellationTokenSource(1000);
            CancellationToken testFinishedToken = testFinishedSource.Token;

            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            AudioSample sample = new AudioSample(BinaryHelpers.EMPTY_FLOAT_ARRAY, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (LinearMixer mixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
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
                        mixer.AddInput(previousSource, null, false);
                    }
                }
                finally
                {
                    source.StopActivelyWriting();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioLinearMixerDoesntOverflowWhenAnInputPrunesDuringPush()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);

            float[] inputSampleData = new float[100 * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(inputSampleData, 48000, 500, 100, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (PassthroughAudioPipe fixedSourcePassthrough = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (SineWaveSampleSource infiniteSource = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 2351, 0.3f))
            using (LinearMixer roomMixer = new LinearMixer(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (PassthroughAudioPipe output = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                roomMixer.AddInput(infiniteSource);
                roomMixer.AddInput(fixedSourcePassthrough);
                roomMixer.ConnectOutput(output);
                AudioSample outputSample = await AudioTestHelpers.PushAudioThroughGraph(new WeakPointer<IAudioGraph>(graph), fixedSourcePassthrough, output, inputSample, 7763);
            }
        }
        
        // TODO: 
        // test cases that might cause mixer to return 0 - read with no inputs, read from an input that returns zero, with + without the silence flag
        // two input to a mixer - one is disconnected. push audio through the other - to check if null input is allowed on DriveInput
        // Splitter basics (push/pull of different outputs, assert alignment)
        // Splitter + mixer combo ends up doubling volume

        //using (Stream outputStream = new FileStream(@"C:\code\Durandal\Data\Test.wav", FileMode.Create, FileAccess.Write))
        //using (WaveStreamSampleTarget fileOut = WaveStreamSampleTarget.Create(new WeakPointer<IAudioGraph>(graph), outputStream, format).Await())
        //{
        //    FixedAudioSampleSource outSampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), target.GetAllAudio());
        //    outSampleSource.ConnectOutput(fileOut);
        //    while (!outSampleSource.PlaybackFinished)
        //    {
        //        outSampleSource.WriteSamplesToOutput(1000, CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
        //    }
        //}

        [TestMethod]
        public async Task TestAudioConcatenatorBasic()
        {
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
            using (AudioConcatenator concatenator = new AudioConcatenator(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                concatenator.EnqueueInput(shortSampleSource);
                concatenator.EnqueueInput(longSampleSource);
                concatenator.ConnectOutput(target);

                IRandom rand = new FastRandom(9704103);
                int totalSamplesRead = 0;
                while (totalSamplesRead < 48000 * 6)
                {
                    int firstRead = await target.ReadSamplesFromInput(rand.NextInt(1, 999), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreNotEqual(0, firstRead); // prevent infinite loops
                    if (firstRead > 0)
                    {
                        totalSamplesRead += firstRead;
                    }
                }

                float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 6];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 2000, 48000 * 2, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 48000 * 2, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 48000 * 2, 1, format.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioSample actualOutput = target.GetAllAudio();
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, actualOutput);
            }
        }

        [TestMethod]
        public async Task TestAudioConcatenatorReadInitialSilenceIfReadFully()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioConcatenator concatenator = new AudioConcatenator(new WeakPointer<IAudioGraph>(graph), format, null, true))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                concatenator.ConnectOutput(target);
                float[] buf = new float[10];
                int samplesPerChannelRead = await concatenator.ReadAsync(buf, 0, 5, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(5, samplesPerChannelRead);
                for (int c = 0; c < buf.Length; c++)
                {
                    Assert.AreEqual(0, buf[c]);
                }
            }
        }

        [TestMethod]
        public async Task TestAudioConcatenatorImmediateEndOfStream()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioConcatenator concatenator = new AudioConcatenator(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                concatenator.ConnectOutput(target);
                float[] buf = new float[10];
                int samplesPerChannelRead = await concatenator.ReadAsync(buf, 0, 5, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                Assert.AreEqual(-1, samplesPerChannelRead);
            }
        }

        [TestMethod]
        public async Task TestAudioConcatenatorCantPush()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SilenceAudioSampleSource source = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), format, null))
            using (AudioConcatenator concatenator = new AudioConcatenator(new WeakPointer<IAudioGraph>(graph), format, null, false))
            {
                concatenator.EnqueueInput(source);
                try
                {
                    await source.Output.WriteAsync(new float[10], 0, 5, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Expected an InvalidOperationException");
                }
                catch (InvalidOperationException) { }
            }
        }
        
        [TestMethod]
        public async Task TestAudioConcatenatorDisposeOfOwnedInputOnPlaybackFinish()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioConcatenator concatenator = new AudioConcatenator(new WeakPointer<IAudioGraph>(graph), format, null, false))
            using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null);
                concatenator.ConnectOutput(bucket);
                concatenator.EnqueueInput(sampleSource, null, true);
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
        public async Task TestAudioConcatenatorDisposeOfOwnedInputOnParentDisposal()
        {
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            {
                FixedAudioSampleSource sampleSource = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null);

                using (AudioConcatenator concatenator = new AudioConcatenator(new WeakPointer<IAudioGraph>(graph), format, null, true))
                using (BucketAudioSampleTarget bucket = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
                {
                    concatenator.ConnectOutput(bucket);
                    concatenator.EnqueueInput(sampleSource, null, true);
                }

                // The sample source should have been disposed of by now.
                try
                {
                    await sampleSource.ReadAsync(new float[10], 0, 1, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown an ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }
            }
        }

        
    }
}
