using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Audio.Components;
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
using Durandal.Common.Audio.Test;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    public class ChannelFanoutSplitterTests
    {
        [TestMethod]
        public void TestChannelFanoutSplitterInvalidArgs()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Stereo(48000), null))
            using (NullAudioSampleTarget unknownTarget = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), new AudioSampleFormat(48000, 2, MultiChannelMapping.Unknown), null))
            using (NullAudioSampleTarget monoTarget = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                try
                {
                    splitter.AddOutput(unknownTarget, 0);
                    Assert.Fail("Should have thrown an ArgumentException");
                }
                catch (ArgumentException) { }

                try
                {
                    splitter.AddOutput(monoTarget, 1, 0);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }

                try
                {
                    splitter.AddOutput(monoTarget, 2);
                    Assert.Fail("Should have thrown an ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException) { }
            }
        }

        [TestMethod]
        public void TestChannelFanoutSplitterThreadSafety()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testFinishedSource = new CancellationTokenSource(1000);
            CancellationToken testFinishedToken = testFinishedSource.Token;

            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 1.0f))
            {
                try
                {
                    source.BeginActivelyWriting(logger.Clone("WriteThread"), DefaultRealTimeProvider.Singleton);
                    IAudioSampleTarget previousTarget = null;
                    while (!testFinishedToken.IsCancellationRequested)
                    {
                        if (previousTarget != null)
                        {
                            previousTarget.DisconnectInput();
                            previousTarget.Dispose();
                        }

                        previousTarget = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null);
                        splitter.AddOutput(previousTarget, new sbyte[] { 0 });
                    }
                }
                finally
                {
                    source.StopActivelyWriting();
                }
            }
        }

        [TestMethod]
        public async Task TestChannelFanoutSplitterBasicPush()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(splitter);
                splitter.AddOutput(target1, new sbyte[] { 0, 1 });
                splitter.AddOutput(target2, new sbyte[] { 0, 1 });

                IRandom rand = new FastRandom(23662);
                int totalSamplesRead = 0;
                while (totalSamplesRead < (48000 * 4))
                {
                    totalSamplesRead += await source.WriteSamplesToOutput(rand.NextInt(1, 4800), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await splitter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, target1.GetAllAudio());
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, target2.GetAllAudio());

                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public async Task TestChannelFanoutSplitterBasicPull()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                splitter.AddOutput(target1, new sbyte[] { 0, 1 });
                splitter.AddOutput(target2, new sbyte[] { 0, 1 });
                Assert.AreEqual(0, await target1.ReadSamplesFromInput(1, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                source.ConnectOutput(splitter);

                IRandom rand = new FastRandom(988553);
                int totalSamplesRead = 0;
                while (totalSamplesRead < (48000 * 4))
                {
                    totalSamplesRead += await target1.ReadSamplesFromInput(rand.NextInt(1, 4800), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                Assert.AreEqual(-1, await target1.ReadSamplesFromInput(rand.NextInt(1, 4800), CancellationToken.None, DefaultRealTimeProvider.Singleton));

                float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, target1.GetAllAudio());
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, target2.GetAllAudio());

                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public async Task TestChannelFanoutSplitterBasicPullUnreliableInput()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);
            IRandom rand = new FastRandom(49233);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.5f))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                splitter.AddOutput(target1, new sbyte[] { 0, 1 });
                splitter.AddOutput(target2, new sbyte[] { 0, 1 });
                Assert.AreEqual(0, await target1.ReadSamplesFromInput(1, CancellationToken.None, DefaultRealTimeProvider.Singleton));

                source.ConnectOutput(unreliableFilter);
                unreliableFilter.ConnectOutput(splitter);

                int totalSamplesRead = 0;
                while (totalSamplesRead < (48000 * 4))
                {
                    totalSamplesRead += await target1.ReadSamplesFromInput(rand.NextInt(1, 4800), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                unreliableFilter.ChanceOfFailure = 0;
                Assert.AreEqual(-1, await target1.ReadSamplesFromInput(rand.NextInt(1, 4800), CancellationToken.None, DefaultRealTimeProvider.Singleton));

                float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, target1.GetAllAudio());
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, target2.GetAllAudio());

                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestChannelFanoutSplitterIsDisposedAfterDisconnection()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                splitter.AddOutput(target, new sbyte[] { 0 });
                IAudioSampleSource splitterEndpoint = target.Input;
                target.DisconnectInput();
                try
                {
                    splitterEndpoint.ConnectOutput(target);
                    Assert.Fail("Should have thrown an ObjectDisposedException");
                }
                catch (ObjectDisposedException) { }
            }
        }

        [TestMethod]
        public void TestChannelFanoutSplitterOutputCanTransferConnection()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (NullAudioSampleTarget target1 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (NullAudioSampleTarget target2 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                splitter.AddOutput(target1, new sbyte[] { 0 });
                IAudioSampleSource splitterOutput = target1.Input;
                splitterOutput.ConnectOutput(target2);
                Assert.IsNull(target1.Input);
                Assert.AreEqual(target2, splitterOutput.Output);
                Assert.AreEqual(splitterOutput, target2.Input);
            }
        }

        [TestMethod]
        public async Task TestChannelFanoutSplitterWorksOnPullAfterOutputDisconnected()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SilenceAudioSampleSource source = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (NullAudioSampleTarget target2 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                source.ConnectOutput(splitter);
                splitter.AddOutput(target1, new sbyte[] { 0 });
                splitter.AddOutput(target2, new sbyte[] { 0 });
                await target1.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                target2.DisconnectInput();
                await target1.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        [TestMethod]
        public async Task TestChannelFanoutSplitterWorksOnPushAfterOutputDisconnected()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            float[] longSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioSample sample = new AudioSample(longSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), sample, null))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (NullAudioSampleTarget target2 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                source.ConnectOutput(splitter);
                splitter.AddOutput(target1, new sbyte[] { 0 });
                splitter.AddOutput(target2, new sbyte[] { 0 });
                await source.WriteSamplesToOutput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                target2.DisconnectInput();
                await source.WriteSamplesToOutput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        [TestMethod]
        public async Task TestChannelFanoutSplitterDecomposeStereoInput()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(format.SampleRateHz), null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(format.SampleRateHz), null))
            {
                source.ConnectOutput(splitter);
                splitter.AddOutput(target1, 0);
                splitter.AddOutput(target2, 1);

                IRandom rand = new FastRandom(23662);
                int totalSamplesRead = 0;
                while (totalSamplesRead < (48000 * 4))
                {
                    totalSamplesRead += await source.WriteSamplesToOutput(rand.NextInt(1, 4800), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await splitter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                float[] expectedSampleData1 = new float[format.SampleRateHz * 4];
                AudioTestHelpers.GenerateSineWave(expectedSampleData1, format.SampleRateHz, 440, 48000 * 4, 0, 0, 1, 0.4f, 0.0f);
                AudioSample expectedOutput1 = new AudioSample(expectedSampleData1, AudioSampleFormat.Mono(format.SampleRateHz));
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput1, target1.GetAllAudio());

                float[] expectedSampleData2 = new float[format.SampleRateHz * 4];
                AudioTestHelpers.GenerateSineWave(expectedSampleData2, format.SampleRateHz, 440, 48000 * 4, 0, 0, 1, 0.4f, 0.5f);
                AudioSample expectedOutput2 = new AudioSample(expectedSampleData2, AudioSampleFormat.Mono(format.SampleRateHz));
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput2, target2.GetAllAudio());

                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public async Task TestChannelFanoutSplitterSwapStereoChannels()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(splitter);
                splitter.AddOutput(target, new sbyte[] { 1, 0 });

                IRandom rand = new FastRandom(23662);
                int totalSamplesRead = 0;
                while (totalSamplesRead < (48000 * 4))
                {
                    totalSamplesRead += await source.WriteSamplesToOutput(rand.NextInt(1, 4800), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await splitter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.0f);
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, target.GetAllAudio());

                target.DisconnectInput();
            }
        }

        [TestMethod]
        public async Task TestChannelFanoutSplitterSkipChannels()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (ChannelFanoutSplitter splitter = new ChannelFanoutSplitter(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(splitter);
                splitter.AddOutput(target, new sbyte[] { 1, -1 });

                IRandom rand = new FastRandom(23662);
                int totalSamplesRead = 0;
                while (totalSamplesRead < (48000 * 4))
                {
                    totalSamplesRead += await source.WriteSamplesToOutput(rand.NextInt(1, 4800), CancellationToken.None, DefaultRealTimeProvider.Singleton);
                }

                await splitter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                float[] expectedSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
                AudioTestHelpers.GenerateSineWave(expectedSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.5f);
                AudioSample expectedOutput = new AudioSample(expectedSampleData, format);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput, target.GetAllAudio());

                target.DisconnectInput();
            }
        }
    }
}
