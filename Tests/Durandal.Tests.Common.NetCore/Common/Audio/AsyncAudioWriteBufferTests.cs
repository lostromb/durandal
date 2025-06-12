using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    public class AsyncAudioWriteBufferTests
    {
        [TestMethod]
        public async Task TestAudioAsyncWriteBufferBasic()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph sourceGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioGraph targetGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AsyncAudioWriteBuffer filter = new AsyncAudioWriteBuffer(new WeakPointer<IAudioGraph>(sourceGraph), new WeakPointer<IAudioGraph>(targetGraph), format, null, TimeSpan.FromMilliseconds(200), logger.Clone("Buffer"), NullMetricCollector.WeakSingleton, DimensionSet.Empty))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(sourceGraph), inputSample, null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(targetGraph), format, null))
            {
                source.ConnectOutput(filter);
                target.ConnectInput(filter);

                IRandom random = new FastRandom(553899);
                while (!filter.PlaybackFinished)
                {
                    await source.WriteSamplesToOutput(random.NextInt(1, 400), CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                    // need to flush intermittently otherwise this loop will just overwhelm the buffer instantly
                    await filter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                }

                AudioSample outputSample = target.GetAllAudio();
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                source.DisconnectOutput();
                target.DisconnectInput();
            }
        }

        // Flaky test
        [TestMethod]
        public async Task TestAudioAsyncWriteBufferHidesUpstreamDelay()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (CancellationTokenSource testCancel = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                CancellationToken testCancelToken = testCancel.Token;

                LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                IRealTimeProvider threadLocalTime = lockStepTime.Fork("TestThread");
                Task lockStepThread = Task.Run(async () =>
                {
                    try
                    {
                        IRandom random = new FastRandom(5519);
                        using (IAudioGraph sourceGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                        using (IAudioGraph targetGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                        using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(sourceGraph), inputSample, null))
                        using (AsyncAudioWriteBuffer filter = new AsyncAudioWriteBuffer(new WeakPointer<IAudioGraph>(sourceGraph), new WeakPointer<IAudioGraph>(targetGraph), format, null, TimeSpan.FromMilliseconds(1000), logger.Clone("Buffer"), NullMetricCollector.WeakSingleton, DimensionSet.Empty))
                        using (AudioRandomDelayFilter delay = new AudioRandomDelayFilter(new WeakPointer<IAudioGraph>(targetGraph), format, random, logger.Clone("Delay")))
                        using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(targetGraph), format, null))
                        {
                            source.ConnectOutput(filter);
                            filter.ConnectOutput(delay);
                            delay.ConnectOutput(target);

                            // Enable delays
                            // Note that because of the way the write buffer works, delay applies to each 10ms segment, not some larger imagined batch write.
                            // So we carefully select these values to avoid overflowing the test buffer
                            delay.MinDelay = TimeSpan.FromMilliseconds(1);
                            delay.MaxDelay = TimeSpan.FromMilliseconds(10);

                            TimeSpan writeIncrement = TimeSpan.FromMilliseconds(10);
                            int samplesPerChannelToWrite = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, writeIncrement);

                            while (!filter.PlaybackFinished)
                            {
                                await threadLocalTime.WaitAsync(writeIncrement, testCancelToken).ConfigureAwait(false);
                                DateTimeOffset writeStartTime = threadLocalTime.Time;
                                await source.WriteSamplesToOutput(samplesPerChannelToWrite, testCancelToken, threadLocalTime).ConfigureAwait(false);
                                TimeSpan timeTaken = threadLocalTime.Time - writeStartTime;
                                Assert.IsTrue(timeTaken < TimeSpan.FromMilliseconds(2)); // Writes should always complete within our real time budget
                            }

                            await filter.FlushAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);

                            AudioSample outputSample = target.GetAllAudio();
                            AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                            source.DisconnectOutput();
                            target.DisconnectInput();
                        }
                    }
                    finally
                    {
                        threadLocalTime.Merge();
                    }
                });

                // synchronous write time
                lockStepTime.Step(TimeSpan.FromMilliseconds(1100), 10);

                // then background buffer drain
                lockStepTime.Step(TimeSpan.FromMilliseconds(10000), 1000);
                await lockStepThread.ConfigureAwait(false);
            }
        }
    }
}
