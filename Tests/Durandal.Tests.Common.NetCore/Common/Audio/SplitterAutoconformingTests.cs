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
    public class SplitterAutoconformingTests
    {
        [TestMethod]
        public void TestAudioSplitterAutoconformingThreadSafety()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testFinishedSource = new CancellationTokenSource(1000);
            CancellationToken testFinishedToken = testFinishedSource.Token;

            AudioSampleFormat format = AudioSampleFormat.Mono(16000);
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioSplitterAutoConforming splitter = new AudioSplitterAutoConforming(new WeakPointer<IAudioGraph>(graph), format, null, logger.Clone("Resampler")))
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
                        splitter.AddOutput(previousTarget);
                    }
                }
                finally
                {
                    source.StopActivelyWriting();
                }
            }
        }

        [TestMethod]
        public async Task TestAudioSplitterAutoConformingBasicPassthroughPush()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (AudioSplitterAutoConforming splitter = new AudioSplitterAutoConforming(new WeakPointer<IAudioGraph>(graph), format, null, logger.Clone("Resampler")))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                source.ConnectOutput(splitter);
                splitter.AddOutput(target1);
                splitter.AddOutput(target2);

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
        public async Task TestAudioSplitterAutoConformingBasicPassthroughPull()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (AudioSplitterAutoConforming splitter = new AudioSplitterAutoConforming(new WeakPointer<IAudioGraph>(graph), format, null, logger.Clone("Resampler")))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                splitter.AddOutput(target1);
                splitter.AddOutput(target2);
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

        [Ignore]
        [TestMethod]
        public async Task TestAudioSplitterAutoConformingBasicPush()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat inputFormat = AudioSampleFormat.Stereo(48000);
            AudioSampleFormat output1Format = AudioSampleFormat.Mono(16000);
            AudioSampleFormat output2Format = AudioSampleFormat.Stereo(24000);
            float[] inputSampleData = new float[inputFormat.SampleRateHz * inputFormat.NumChannels * 2];
            AudioTestHelpers.GenerateSineWave(inputSampleData, inputFormat.SampleRateHz, 440, inputFormat.SampleRateHz * 2, 0, 0, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, inputFormat.SampleRateHz, 440, inputFormat.SampleRateHz * 2, 0, 1, inputFormat.NumChannels, 0.4f, 0.0f);
            AudioSample inputSample = new AudioSample(inputSampleData, inputFormat);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (AudioSplitterAutoConforming splitter = new AudioSplitterAutoConforming(new WeakPointer<IAudioGraph>(graph), inputFormat, null, logger.Clone("Resampler")))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), output1Format, null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), output2Format, null))
            {
                source.ConnectOutput(splitter);
                splitter.AddOutput(target1);
                splitter.AddOutput(target2);

                await source.WriteFully(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                int sample1Delay = 16;
                int sample2Delay = 16;

                float[] expectedSampleData1 = new float[output1Format.SampleRateHz * output1Format.NumChannels * 2];
                AudioTestHelpers.GenerateSineWave(expectedSampleData1, output1Format.SampleRateHz, 440, (output1Format.SampleRateHz * 2) - sample1Delay, sample1Delay, 0, output1Format.NumChannels, 0.4f, 0.0f);
                AudioSample expectedOutput1 = new AudioSample(expectedSampleData1, output1Format);
                AudioSample actualOutput1 = target1.GetAllAudio();

                float[] expectedSampleData2 = new float[output2Format.SampleRateHz * output2Format.NumChannels * 2];
                AudioTestHelpers.GenerateSineWave(expectedSampleData2, output2Format.SampleRateHz, 440, (output2Format.SampleRateHz * 2) - sample2Delay, sample2Delay, 0, output2Format.NumChannels, 0.4f, 0.0f);
                AudioSample expectedOutput2 = new AudioSample(expectedSampleData2, output2Format);
                AudioSample actualOutput2 = target2.GetAllAudio();

                AudioSample diff = AudioTestHelpers.GenerateDiffImage(expectedOutput1, actualOutput1);

                using (FileStream stream = new FileStream(@"C:\Code\Durandal\Data\a_expect.wav", FileMode.Create, FileAccess.Write))
                {
                    await AudioHelpers.WriteWaveToStream(expectedOutput1, stream);
                }
                using (FileStream stream = new FileStream(@"C:\Code\Durandal\Data\a_actual.wav", FileMode.Create, FileAccess.Write))
                {
                    await AudioHelpers.WriteWaveToStream(actualOutput1, stream);
                }
                using (FileStream stream = new FileStream(@"C:\Code\Durandal\Data\a_diff.wav", FileMode.Create, FileAccess.Write))
                {
                    await AudioHelpers.WriteWaveToStream(diff, stream);
                }

                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput1, actualOutput1, 0.80f);
                AudioTestHelpers.AssertSamplesAreSimilar(expectedOutput2, actualOutput2, 0.80f);

                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public async Task TestAudioSplitterAutoConformingBasicPull()
        {
            ILogger logger = new ConsoleLogger();

            AudioSampleFormat format = AudioSampleFormat.Stereo(48000);
            float[] inputSampleData = new float[format.SampleRateHz * format.NumChannels * 4];

            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioTestHelpers.GenerateSineWave(inputSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 1, format.NumChannels, 0.4f, 0.5f);
            AudioSample inputSample = new AudioSample(inputSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), inputSample, null))
            using (AudioSplitterAutoConforming splitter = new AudioSplitterAutoConforming(new WeakPointer<IAudioGraph>(graph), format, null, logger.Clone("Resampler")))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                splitter.AddOutput(target1);
                splitter.AddOutput(target2);
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
        public async Task TestAudioSplitterAutoConformingBasicPullUnreliableInput()
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
            using (AudioSplitterAutoConforming splitter = new AudioSplitterAutoConforming(new WeakPointer<IAudioGraph>(graph), format, null, logger.Clone("Resampler")))
            using (SimulatedUnreliableAudioSource unreliableFilter = new SimulatedUnreliableAudioSource(new WeakPointer<IAudioGraph>(graph), format, rand, 0.5f))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (BucketAudioSampleTarget target2 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            {
                splitter.AddOutput(target1);
                splitter.AddOutput(target2);
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
        public void TestAudioSplitterAutoConformingIsDisposedAfterDisconnection()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioSplitterAutoConforming splitter = new AudioSplitterAutoConforming(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, DebugLogger.Default))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                splitter.AddOutput(target);
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
        public void TestAudioSplitterAutoConformingOutputCanTransferConnection()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AudioSplitterAutoConforming splitter = new AudioSplitterAutoConforming(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, DebugLogger.Default))
            using (NullAudioSampleTarget target1 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (NullAudioSampleTarget target2 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                splitter.AddOutput(target1);
                IAudioSampleSource splitterOutput = target1.Input;
                splitterOutput.ConnectOutput(target2);
                Assert.IsNull(target1.Input);
                Assert.AreEqual(target2, splitterOutput.Output);
                Assert.AreEqual(splitterOutput, target2.Input);
            }
        }

        [TestMethod]
        public async Task TestAudioSplitterAutoConformingWorksOnPullAfterOutputDisconnected()
        {
            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SilenceAudioSampleSource source = new SilenceAudioSampleSource(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (AudioSplitterAutoConforming splitter = new AudioSplitterAutoConforming(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, DebugLogger.Default))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (NullAudioSampleTarget target2 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                source.ConnectOutput(splitter);
                splitter.AddOutput(target1);
                splitter.AddOutput(target2);
                await target1.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                target2.DisconnectInput();
                await target1.ReadSamplesFromInput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }

        [TestMethod]
        public async Task TestAudioSplitterAutoConformingWorksOnPushAfterOutputDisconnected()
        {
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);
            float[] longSampleData = new float[format.SampleRateHz * format.NumChannels * 4];
            AudioTestHelpers.GenerateSineWave(longSampleData, format.SampleRateHz, 440, 48000 * 4, 0, 0, format.NumChannels, 0.4f, 0.0f);
            AudioSample sample = new AudioSample(longSampleData, format);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(graph), sample, null))
            using (AudioSplitterAutoConforming splitter = new AudioSplitterAutoConforming(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null, DebugLogger.Default))
            using (BucketAudioSampleTarget target1 = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            using (NullAudioSampleTarget target2 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), AudioSampleFormat.Mono(48000), null))
            {
                source.ConnectOutput(splitter);
                splitter.AddOutput(target1);
                splitter.AddOutput(target2);
                await source.WriteSamplesToOutput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                target2.DisconnectInput();
                await source.WriteSamplesToOutput(100, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }
        }
    }
}
