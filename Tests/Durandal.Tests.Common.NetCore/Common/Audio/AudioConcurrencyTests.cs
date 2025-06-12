using Durandal.Common.Audio;
using Durandal.Common.Audio.Components;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
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

namespace Durandal.Tests.Common.Audio
{
    [TestClass]
    [DoNotParallelize]
    public class AudioConcurrencyTests
    {
        [TestMethod]
        public void TestAudioConcurrencyTwoSourcesOneTarget()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testFinishedCancelizer = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            CancellationToken testFinishedCancelToken = testFinishedCancelizer.Token;
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source1 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (SineWaveSampleSource source2 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (Barrier startingPistol = new Barrier(8))
            {
                List<Task> threads = new List<Task>();
                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            source1.ConnectOutput(target);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            source2.ConnectOutput(target);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            target.ConnectInput(source1);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            target.ConnectInput(source2);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await target.ReadSamplesFromInput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await source1.WriteSamplesToOutput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await source2.WriteSamplesToOutput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                startingPistol.SignalAndWait();

                foreach (Task thread in threads)
                {
                    Assert.IsTrue(thread.AwaitWithTimeout(10000), "Deadlocked");
                }

                // Assert that the graph is consistent
                Assert.IsNotNull(target.Input);
                IAudioSampleSource expectedSource = target.Input;
                IAudioSampleSource nonConnectedSource = expectedSource == source1 ? source2 : source1;
                Assert.AreEqual(target, expectedSource.Output);
                Assert.IsNull(nonConnectedSource.Output);
                source1.DisconnectOutput();
                source2.DisconnectOutput();
                target.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioConcurrencyTwoFiltersOneTarget()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testFinishedCancelizer = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            CancellationToken testFinishedCancelToken = testFinishedCancelizer.Token;
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source1 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (SineWaveSampleSource source2 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (PassthroughAudioPipe filter1 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (PassthroughAudioPipe filter2 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (Barrier startingPistol = new Barrier(8))
            {
                source1.ConnectOutput(filter1);
                source2.ConnectOutput(filter2);

                List<Task> threads = new List<Task>();
                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            filter1.ConnectOutput(target);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            filter2.ConnectOutput(target);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            target.ConnectInput(filter1);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            target.ConnectInput(filter2);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await target.ReadSamplesFromInput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await source1.WriteSamplesToOutput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await source2.WriteSamplesToOutput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                startingPistol.SignalAndWait();

                foreach (Task thread in threads)
                {
                    Assert.IsTrue(thread.AwaitWithTimeout(10000), "Deadlocked");
                }

                // Assert that the graph is consistent
                Assert.IsNotNull(target.Input);
                IAudioSampleSource expectedSource = target.Input;
                IAudioSampleSource nonConnectedSource = expectedSource == filter1 ? filter2 : filter1;
                Assert.AreEqual(target, expectedSource.Output);
                Assert.IsNull(nonConnectedSource.Output);
                source1.DisconnectOutput();
                source2.DisconnectOutput();
                filter1.DisconnectInput();
                filter2.DisconnectInput();
                filter1.DisconnectOutput();
                filter2.DisconnectOutput();
                target.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioConcurrencyTwoSourcesOneFilter()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testFinishedCancelizer = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            CancellationToken testFinishedCancelToken = testFinishedCancelizer.Token;
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source1 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (SineWaveSampleSource source2 = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (PassthroughAudioPipe filter = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (NullAudioSampleTarget target = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (Barrier startingPistol = new Barrier(8))
            {
                filter.ConnectOutput(target);

                List<Task> threads = new List<Task>();
                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            source1.ConnectOutput(filter);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            source2.ConnectOutput(filter);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            filter.ConnectInput(source1);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            filter.ConnectInput(source2);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await target.ReadSamplesFromInput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await source1.WriteSamplesToOutput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await source2.WriteSamplesToOutput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                startingPistol.SignalAndWait();

                foreach (Task thread in threads)
                {
                    Assert.IsTrue(thread.AwaitWithTimeout(10000), "Deadlocked");
                }

                // Assert that the graph is consistent
                Assert.IsNotNull(filter.Input);
                IAudioSampleSource expectedSource = filter.Input;
                IAudioSampleSource nonConnectedSource = expectedSource == source1 ? source2 : source1;
                Assert.AreEqual(filter, expectedSource.Output);
                Assert.IsNull(nonConnectedSource.Output);
                source1.DisconnectOutput();
                source2.DisconnectOutput();
                filter.DisconnectInput();
                filter.DisconnectOutput();
                target.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioConcurrencyOneSourceTwoTargets()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testFinishedCancelizer = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            CancellationToken testFinishedCancelToken = testFinishedCancelizer.Token;
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (NullAudioSampleTarget target1 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (NullAudioSampleTarget target2 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (Barrier startingPistol = new Barrier(8))
            {
                List<Task> threads = new List<Task>();
                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            source.ConnectOutput(target1);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            source.ConnectOutput(target2);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            target1.ConnectInput(source);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            target2.ConnectInput(source);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await source.WriteSamplesToOutput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await target1.ReadSamplesFromInput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await target2.ReadSamplesFromInput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                startingPistol.SignalAndWait();

                foreach (Task thread in threads)
                {
                    Assert.IsTrue(thread.AwaitWithTimeout(10000), "Deadlocked");
                }

                // Assert that the graph is consistent
                Assert.IsNotNull(source.Output);
                IAudioSampleTarget expectedTarget = source.Output;
                IAudioSampleTarget nonConnectedTarget = expectedTarget == target1 ? target2 : target1;
                Assert.AreEqual(source, expectedTarget.Input);
                Assert.IsNull(nonConnectedTarget.Input);
                source.DisconnectOutput();
                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioConcurrencyOneFilterTwoTargets()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testFinishedCancelizer = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            CancellationToken testFinishedCancelToken = testFinishedCancelizer.Token;
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (PassthroughAudioPipe filter = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (NullAudioSampleTarget target1 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (NullAudioSampleTarget target2 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (Barrier startingPistol = new Barrier(8))
            {
                source.ConnectOutput(filter);
                List<Task> threads = new List<Task>();
                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            filter.ConnectOutput(target1);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            filter.ConnectOutput(target2);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            target1.ConnectInput(filter);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            target2.ConnectInput(filter);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await source.WriteSamplesToOutput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await target1.ReadSamplesFromInput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await target2.ReadSamplesFromInput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                startingPistol.SignalAndWait();

                foreach (Task thread in threads)
                {
                    Assert.IsTrue(thread.AwaitWithTimeout(10000), "Deadlocked");
                }

                // Assert that the graph is consistent
                Assert.IsNotNull(filter.Output);
                IAudioSampleTarget expectedTarget = filter.Output;
                IAudioSampleTarget nonConnectedTarget = expectedTarget == target1 ? target2 : target1;
                Assert.AreEqual(filter, expectedTarget.Input);
                Assert.IsNull(nonConnectedTarget.Input);
                source.DisconnectOutput();
                filter.DisconnectInput();
                filter.DisconnectOutput();
                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }

        [TestMethod]
        public void TestAudioConcurrencyOneSourceTwoFilters()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testFinishedCancelizer = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            CancellationToken testFinishedCancelToken = testFinishedCancelizer.Token;
            AudioSampleFormat format = AudioSampleFormat.Mono(48000);

            using (IAudioGraph graph = new AudioGraph(AudioGraphCapabilities.Concurrent))
            using (SineWaveSampleSource source = new SineWaveSampleSource(new WeakPointer<IAudioGraph>(graph), format, null, 440, 0.2f))
            using (PassthroughAudioPipe filter1 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (PassthroughAudioPipe filter2 = new PassthroughAudioPipe(new WeakPointer<IAudioGraph>(graph), format, null))
            using (NullAudioSampleTarget target1 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (NullAudioSampleTarget target2 = new NullAudioSampleTarget(new WeakPointer<IAudioGraph>(graph), format, null))
            using (Barrier startingPistol = new Barrier(8))
            {
                filter1.ConnectOutput(target1);
                filter2.ConnectOutput(target2);

                List<Task> threads = new List<Task>();
                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            source.ConnectOutput(filter1);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            source.ConnectOutput(filter2);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            filter1.ConnectInput(source);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(() =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            filter2.ConnectInput(source);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await source.WriteSamplesToOutput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await target1.ReadSamplesFromInput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                threads.Add(Task.Run(async () =>
                {
                    startingPistol.SignalAndWait();
                    try
                    {
                        while (!testFinishedCancelToken.IsCancellationRequested)
                        {
                            await target2.ReadSamplesFromInput(10, testFinishedCancelToken, DefaultRealTimeProvider.Singleton);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e);
                    }
                }));

                startingPistol.SignalAndWait();

                foreach (Task thread in threads)
                {
                    Assert.IsTrue(thread.AwaitWithTimeout(10000), "Deadlocked");
                }

                // Assert that the graph is consistent
                Assert.IsNotNull(source.Output);
                IAudioSampleTarget expectedTarget = source.Output;
                IAudioSampleTarget nonConnectedTarget = expectedTarget == filter1 ? filter2 : filter1;
                Assert.AreEqual(source, expectedTarget.Input);
                Assert.IsNull(nonConnectedTarget.Input);
                source.DisconnectOutput();
                filter1.DisconnectInput();
                filter2.DisconnectInput();
                filter1.DisconnectOutput();
                filter2.DisconnectOutput();
                target1.DisconnectInput();
                target2.DisconnectInput();
            }
        }
    }
}
