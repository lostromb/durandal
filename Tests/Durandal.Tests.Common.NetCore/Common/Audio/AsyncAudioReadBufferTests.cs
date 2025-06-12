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
    public class AsyncAudioReadBufferTests
    {
        [TestMethod]
        public async Task TestAudioAsyncReadBufferBasic()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (IAudioGraph sourceGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (IAudioGraph targetGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (AsyncAudioReadBuffer filter = new AsyncAudioReadBuffer(new WeakPointer<IAudioGraph>(sourceGraph), new WeakPointer<IAudioGraph>(targetGraph), format, null, TimeSpan.FromMilliseconds(200), logger.Clone("Buffer"), NullMetricCollector.WeakSingleton, DimensionSet.Empty))
            using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(sourceGraph), inputSample, null))
            using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(targetGraph), format, null))
            {
                source.ConnectOutput(filter);
                target.ConnectInput(filter);

                // Prime the buffer
                filter.FillBufferInBackground(CancellationToken.None, DefaultRealTimeProvider.Singleton);

                IRandom random = new FastRandom(553899);
                while (!filter.PlaybackFinished)
                {
                    await target.ReadSamplesFromInput(random.NextInt(1, 400), CancellationToken.None, DefaultRealTimeProvider.Singleton).ConfigureAwait(false);
                }

                AudioSample outputSample = target.GetAllAudio();
                AudioTestHelpers.AssertSamplesAreSimilar(inputSample, outputSample);
                source.DisconnectOutput();
                target.DisconnectInput();
            }
        }

        [TestMethod]
        public async Task TestAudioAsyncReadBufferHidesUpstreamDelay()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            AudioSampleFormat format = AudioSampleFormat.Stereo(44100);
            float[] sampleData = new float[format.SampleRateHz * format.NumChannels];
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 0, format.NumChannels, 0.1f, 0.0f);
            AudioTestHelpers.GenerateSineWave(sampleData, 44100, 500, 44100, 0, 1, format.NumChannels, 0.1f, 0.5f);
            AudioSample inputSample = new AudioSample(sampleData, format);

            using (CancellationTokenSource testCancel = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken testCancelToken = testCancel.Token;

                LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
                IRealTimeProvider threadLocalTime = lockStepTime.Fork("TestThread");
                Task lockStepThread = Task.Run(async () =>
                {
                    try
                    {
                        IRandom random = new FastRandom(651);
                        using (IAudioGraph sourceGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                        using (IAudioGraph targetGraph = new AudioGraph(AudioGraphCapabilities.Concurrent))
                        using (FixedAudioSampleSource source = new FixedAudioSampleSource(new WeakPointer<IAudioGraph>(sourceGraph), inputSample, null))
                        using (AudioRandomDelayFilter delay = new AudioRandomDelayFilter(new WeakPointer<IAudioGraph>(sourceGraph), format, random, logger.Clone("Delay")))
                        using (AsyncAudioReadBuffer filter = new AsyncAudioReadBuffer(new WeakPointer<IAudioGraph>(sourceGraph), new WeakPointer<IAudioGraph>(targetGraph), format, null, TimeSpan.FromMilliseconds(100), logger.Clone("Buffer"), NullMetricCollector.WeakSingleton, DimensionSet.Empty))
                        using (BucketAudioSampleTarget target = new BucketAudioSampleTarget(new WeakPointer<IAudioGraph>(targetGraph), format, null))
                        {
                            source.ConnectOutput(delay);
                            delay.ConnectOutput(filter);
                            target.ConnectInput(filter);

                            // Prime the buffer
                            filter.FillBufferInBackground(testCancelToken, threadLocalTime);
                            await filter.WaitForCurrentReadToFinish(testCancelToken);

                            // Enable delays
                            delay.MinDelay = TimeSpan.FromMilliseconds(1);
                            delay.MaxDelay = TimeSpan.FromMilliseconds(50);

                            TimeSpan readIncrement = TimeSpan.FromMilliseconds(10);
                            int samplesPerChannelToRead = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, readIncrement);

                            while (!filter.PlaybackFinished)
                            {
                                await threadLocalTime.WaitAsync(readIncrement, testCancelToken).ConfigureAwait(false);
                                DateTimeOffset readStartTime = threadLocalTime.Time;
                                int samplesRead = await target.ReadSamplesFromInput(samplesPerChannelToRead, testCancelToken, threadLocalTime).ConfigureAwait(false);
                                if (samplesRead < 0)
                                {
                                    Assert.IsTrue(filter.PlaybackFinished);
                                }
                                else
                                {
                                    Assert.IsTrue(samplesRead > 0); // Reads should always return data
                                    Assert.IsTrue(threadLocalTime.Time - readStartTime < readIncrement); // Reads should always complete within our real time budget
                                }
                            }

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

                lockStepTime.Step(TimeSpan.FromMilliseconds(1100), 10);
                await lockStepThread.ConfigureAwait(false);
            }
        }
    }
}
