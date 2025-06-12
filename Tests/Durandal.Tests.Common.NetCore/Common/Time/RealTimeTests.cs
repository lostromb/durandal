using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Time.Scheduling;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Tests.Common.Time;
using System.Runtime.InteropServices;

namespace Durandal.Tests.Common.Time
{
    [TestClass]
    public class RealTimeTests
    {
        [TestMethod]
        public void TestLockstepTime()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider time = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            using (CustomThreadPool threadPool = new CustomThreadPool(logger.Clone("ThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ThreadPool", 10))
            {
                LockStepWorker[] workers = new LockStepWorker[10];

                for (int c = 0; c < threadPool.ThreadCount; c++)
                {
                    LockStepWorker p = new LockStepWorker(c, time.Fork("Worker"));
                    threadPool.EnqueueUserWorkItem(p.Run);
                    workers[c] = p;
                }

                bool allFinished = false;
                long globalTime = 0;
                TimeSpan stepTime = TimeSpan.FromMilliseconds(10);
                while (!allFinished && globalTime < TimeSpan.FromMilliseconds(1000).Ticks)
                {
                    globalTime += stepTime.Ticks;
                    Console.WriteLine("Step! The global time is advancing to " + (globalTime / 10000));
                    time.Step(stepTime);
                    Console.WriteLine("Lock! The global time is " + (globalTime / 10000));
                    allFinished = true;
                    for (int c = 0; c < workers.Length; c++)
                    {
                        allFinished = !workers[c].IsAlive && allFinished;
                    }
                }

                Assert.IsTrue(allFinished);
            }
        }

        [TestMethod]
        public void TestLockstepTimeAsync()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider time = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            using (CustomThreadPool threadPool = new CustomThreadPool(logger.Clone("ThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ThreadPool", 10))
            {
                LockStepWorker[] workers = new LockStepWorker[10];

                for (int c = 0; c < threadPool.ThreadCount; c++)
                {
                    LockStepWorker p = new LockStepWorker(c, time.Fork("Worker"));
                    threadPool.EnqueueUserAsyncWorkItem(p.RunAsync);
                    workers[c] = p;
                }

                bool allFinished = false;
                long globalTime = 0;
                TimeSpan stepTime = TimeSpan.FromMilliseconds(10);
                while (!allFinished && globalTime < TimeSpan.FromMilliseconds(1000).Ticks)
                {
                    globalTime += stepTime.Ticks;
                    Console.WriteLine("Step! The global time is advancing to " + (globalTime / 10000));
                    time.Step(stepTime);
                    Console.WriteLine("Lock! The global time is " + (globalTime / 10000));
                    allFinished = true;
                    for (int c = 0; c < workers.Length; c++)
                    {
                        allFinished = !workers[c].IsAlive && allFinished;
                    }
                }

                Assert.IsTrue(allFinished);
            }
        }

        [TestMethod]
        public async Task TestLockstepTimeCanWaitOnFork0()
        {
            CancellationTokenSource testKiller = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            ILogger logger = new ConsoleLogger("Test", LogLevel.All);
            LockStepRealTimeProvider time = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            int threadHasWaitedFor = 0;
            IRealTimeProvider forkedTime = time.Fork("ForkedTime");
            Task backgroundThread = Task.Run(() =>
            {
                try
                {
                    while (threadHasWaitedFor < 1000)
                    {
                        forkedTime.Wait(TimeSpan.FromMilliseconds(10), testKiller.Token);
                        threadHasWaitedFor += 10;
                    }
                }
                finally
                {
                    forkedTime.Merge();
                }
            });

            logger.Log("Step 200");
            time.Step(TimeSpan.FromMilliseconds(200));
            DurandalTaskExtensions.Block(100, testKiller.Token);
            logger.Log("Thread has now waited for " + threadHasWaitedFor);
            Assert.AreEqual(200, threadHasWaitedFor, 20);

            logger.Log("Wait 200");
            time.Wait(TimeSpan.FromMilliseconds(200), testKiller.Token);
            DurandalTaskExtensions.Block(100, testKiller.Token);
            logger.Log("Thread has now waited for " + threadHasWaitedFor);
            Assert.AreEqual(400, threadHasWaitedFor, 20);

            logger.Log("Step 200");
            time.Step(TimeSpan.FromMilliseconds(200));
            DurandalTaskExtensions.Block(100, testKiller.Token);
            logger.Log("Thread has now waited for " + threadHasWaitedFor);
            Assert.AreEqual(600, threadHasWaitedFor, 20);

            logger.Log("WaitAsync 200");
            await time.WaitAsync(TimeSpan.FromMilliseconds(200), testKiller.Token);
            DurandalTaskExtensions.Block(100, testKiller.Token);
            logger.Log("Thread has now waited for " + threadHasWaitedFor);
            Assert.AreEqual(800, threadHasWaitedFor, 20);

            logger.Log("Step 1000");
            time.Step(TimeSpan.FromMilliseconds(1000));
            DurandalTaskExtensions.Block(100, testKiller.Token);
            logger.Log("Thread has now waited for " + threadHasWaitedFor);
            Assert.AreEqual(1000, threadHasWaitedFor, 20);

            await backgroundThread;
            Assert.IsFalse(testKiller.Token.IsCancellationRequested);
        }

        public class LockStepWorker
        {
            public int local_threadId;
            public IRealTimeProvider local_time;
            public volatile bool IsAlive;

            public LockStepWorker(int threadId, IRealTimeProvider time)
            {
                local_threadId = threadId;
                local_time = time;
                IsAlive = true;
            }

            public void Run()
            {
                Console.WriteLine("[" + local_threadId + "] starting");

                IRandom rand = new FastRandom(local_threadId);
                for (int t = 0; t < 5; t++)
                {
                    Console.WriteLine("[" + local_threadId + "] timestamp is " + (local_time.TimestampMilliseconds));
                    TimeSpan waitTime = TimeSpan.FromMilliseconds(rand.NextInt(0, 50));
                    // Also simulate some real-time processing
                    Thread.Sleep(rand.NextInt(0, 1000));
                    //Console.WriteLine("[" + threadParams.threadId + "] sleeping for " + waitTime.Ticks);
                    local_time.Wait(waitTime, CancellationToken.None);
                }

                Console.WriteLine("[" + local_threadId + "] finished");
                local_time.Merge();
                IsAlive = false;
            }

            public async Task RunAsync()
            {
                Console.WriteLine("[" + local_threadId + "] starting");

                IRandom rand = new FastRandom(local_threadId);
                for (int t = 0; t < 5; t++)
                {
                    Console.WriteLine("[" + local_threadId + "] timestamp is " + (local_time.TimestampMilliseconds));
                    TimeSpan waitTime = TimeSpan.FromMilliseconds(rand.NextInt(0, 50));
                    // Also simulate some real-time processing
                    Thread.Sleep(rand.NextInt(0, 1000));
                    //Console.WriteLine("[" + threadParams.threadId + "] sleeping for " + waitTime.Ticks);
                    await local_time.WaitAsync(waitTime, CancellationToken.None);
                }

                Console.WriteLine("[" + local_threadId + "] finished");
                local_time.Merge();
                IsAlive = false;
            }
        }

        [TestMethod]
        public async Task TestNonRealTimeCancellationTokenSourceDebugMode()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            LockStepRealTimeProvider fakeTime = new LockStepRealTimeProvider(logger);
            using (CancellationTokenSource cancelToken = new NonRealTimeCancellationTokenSource(fakeTime, TimeSpan.FromMilliseconds(10)))
            {

                // Wait for a second to ensure that the task isn't working in real time
                logger.Log("Waiting a bit...");
                await Task.Delay(500);
                Assert.IsFalse(cancelToken.IsCancellationRequested);

                logger.Log("Advancing virtual time...");
                fakeTime.Step(TimeSpan.FromSeconds(1));

                Assert.IsTrue(cancelToken.IsCancellationRequested);
            }
        }

        [TestMethod]
        public async Task TestNonRealTimeCancellationTokenSourceNonDebug()
        {
            ILogger logger = new ConsoleLogger("Main", LogLevel.All);
            DefaultRealTimeProvider fakeTime = DefaultRealTimeProvider.Singleton;
            using (CancellationTokenSource cancelToken = new NonRealTimeCancellationTokenSource(fakeTime, TimeSpan.FromMilliseconds(10)))
            {
                await Task.Delay(500);
                Assert.IsTrue(cancelToken.IsCancellationRequested);
            }
        }

        /// <summary>
        /// Tests that we can cancel + dispose of a nonrealtime cancellation token source, and it won't throw object disposed exceptions for threads still trying to check it.
        /// </summary>
        [TestMethod]
        public void TestNonRealTimeCancellationTokenSourceDisposal()
        {
            ILogger logger = new DetailedConsoleLogger();

            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            IRealTimeProvider forkedTime = lockStepTime.Fork("ForkedTime");
            NonRealTimeCancellationTokenSource nrtSource = new NonRealTimeCancellationTokenSource(DefaultRealTimeProvider.Singleton, TimeSpan.FromMilliseconds(100));
            Task.Run(() =>
            {
                try
                {
                    logger.Log("Thread started");
                    forkedTime.Wait(TimeSpan.FromMilliseconds(10), CancellationToken.None);

                    CancellationToken token = nrtSource.Token;
                    while (!token.IsCancellationRequested)
                    {
                        forkedTime.Wait(TimeSpan.FromMilliseconds(100), CancellationToken.None);
                    }

                    token.IsCancellationRequested.GetHashCode();
                }
                finally
                {
                    forkedTime.Merge();
                    logger.Log("Thread finished");
                }
            });

            lockStepTime.Step(TimeSpan.FromMilliseconds(450), 50);

            logger.Log("Cancelling");
            nrtSource.Cancel();
            logger.Log("Disposing");
            nrtSource.Dispose();
            logger.Log("Disposed");

            lockStepTime.Step(TimeSpan.FromMilliseconds(1000), 100);
        }

        /// <summary>
        /// Tests that if we do a bunch of using(NonRealTimeCancellationTokenSource) calls in a row, they don't overload the lockstep time fork count.
        /// </summary>
        [TestMethod]
        public void TestNonRealTimeCancellationTokenSourceDoesntOverloadLockstep()
        {
            ILogger logger = new DetailedConsoleLogger();

            CancellationTokenSource testWideCancelizer = new CancellationTokenSource();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            for (int c = 0; c < 200; c++)
            {
                NonRealTimeCancellationTokenSource nrtSource = new NonRealTimeCancellationTokenSource(lockStepTime, TimeSpan.FromMilliseconds(3000));
                try
                {
                    using (CancellationTokenSource combinedCancelizer = CancellationTokenSource.CreateLinkedTokenSource(nrtSource.Token, testWideCancelizer.Token))
                    {
                    }
                }
                finally
                {
                    nrtSource?.Dispose();
                }
            }
        }

        [TestMethod]
        public void TestDeltaClock()
        {
            ILogger logger = new ConsoleLogger();
            using (CancellationTokenSource testAbortSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken testAbort = testAbortSource.Token;

                // Run the test many times in a row to try and catch race conditions
                for (int c = 0; c < 50; c++)
                {
                    LockStepRealTimeProvider lockstepTime = new LockStepRealTimeProvider(logger);
                    long baseTime = lockstepTime.TimestampMilliseconds;
                    DeltaClock<int> clock = new DeltaClock<int>(lockstepTime);

                    // Empty clock returns the default value immediately
                    Assert.AreEqual(0, clock.WaitForNextEvent(testAbort));

                    // Schedule some events in order
                    clock.ScheduleEvent(1, 500);
                    clock.ScheduleEvent(2, 750);
                    clock.ScheduleEvent(3, 1500);
                    lockstepTime.Step(TimeSpan.FromMilliseconds(1000));
                    int value = clock.WaitForNextEvent(testAbort);
                    Assert.AreEqual(1, value);
                    value = clock.WaitForNextEvent(testAbort);
                    Assert.AreEqual(2, value);
                    lockstepTime.Step(TimeSpan.FromMilliseconds(1000));
                    value = clock.WaitForNextEvent(testAbort);
                    Assert.AreEqual(3, value);
                    clock.ScheduleEventAbsolute(4, baseTime + 3000);
                    lockstepTime.Step(TimeSpan.FromMilliseconds(2000));
                    value = clock.WaitForNextEvent(testAbort);
                    Assert.AreEqual(4, value);

                    // Schedule some events out-of-order
                    clock.ScheduleEventAbsolute(7, baseTime + 5500);
                    clock.ScheduleEvent(6, 1000);
                    clock.ScheduleEventAbsolute(5, baseTime + 4500);
                    lockstepTime.Step(TimeSpan.FromMilliseconds(2000));
                    value = clock.WaitForNextEvent(testAbort);
                    Assert.AreEqual(5, value);
                    value = clock.WaitForNextEvent(testAbort);
                    Assert.AreEqual(6, value);
                    value = clock.WaitForNextEvent(testAbort);
                    Assert.AreEqual(7, value);

                    // Cancel the clock in the middle of a wait
                    clock.ScheduleEvent(99, TimeSpan.FromSeconds(10000));
                    CancellationTokenSource cancelToken = new NonRealTimeCancellationTokenSource(lockstepTime, TimeSpan.FromMilliseconds(500));
                    value = clock.WaitForNextEvent(cancelToken.Token);
                    Assert.AreEqual(0, value);

                    clock.Stop();
                    // And assert that we can't run a stopped clock
                    try
                    {
                        clock.ScheduleEvent(100, 10);
                        Assert.Fail("Should have thrown an exception");
                    }
                    catch (ObjectDisposedException) { }
                }
            }
        }

        [TestMethod]
        public async Task TestDeltaClockAsync()
        {
            ILogger logger = new ConsoleLogger();
            using (CancellationTokenSource testAbortSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken testAbort = testAbortSource.Token;

                // Run the test many times in a row to try and catch race conditions
                for (int c = 0; c < 50; c++)
                {
                    LockStepRealTimeProvider lockstepTime = new LockStepRealTimeProvider(logger);
                    long baseTime = lockstepTime.TimestampMilliseconds;
                    DeltaClock<int> clock = new DeltaClock<int>(lockstepTime);

                    // Empty clock returns the default value immediately
                    Assert.AreEqual(0, await clock.WaitForNextEventAsync(testAbort).ConfigureAwait(false));

                    // Schedule some events in order
                    clock.ScheduleEvent(1, 500);
                    clock.ScheduleEvent(2, 750);
                    clock.ScheduleEvent(3, 1500);
                    lockstepTime.Step(TimeSpan.FromMilliseconds(1000));
                    int value = await clock.WaitForNextEventAsync(testAbort).ConfigureAwait(false);
                    Assert.AreEqual(1, value);
                    value = await clock.WaitForNextEventAsync(testAbort).ConfigureAwait(false);
                    Assert.AreEqual(2, value);
                    lockstepTime.Step(TimeSpan.FromMilliseconds(1000));
                    value = await clock.WaitForNextEventAsync(testAbort).ConfigureAwait(false);
                    Assert.AreEqual(3, value);
                    clock.ScheduleEventAbsolute(4, baseTime + 3000);
                    lockstepTime.Step(TimeSpan.FromMilliseconds(2000));
                    value = await clock.WaitForNextEventAsync(testAbort).ConfigureAwait(false);
                    Assert.AreEqual(4, value);

                    // Schedule some events out-of-order
                    clock.ScheduleEventAbsolute(7, baseTime + 5500);
                    clock.ScheduleEvent(6, 1000);
                    clock.ScheduleEventAbsolute(5, baseTime + 4500);
                    lockstepTime.Step(TimeSpan.FromMilliseconds(2000));
                    value = await clock.WaitForNextEventAsync(testAbort).ConfigureAwait(false);
                    Assert.AreEqual(5, value);
                    value = await clock.WaitForNextEventAsync(testAbort).ConfigureAwait(false);
                    Assert.AreEqual(6, value);
                    value = await clock.WaitForNextEventAsync(testAbort).ConfigureAwait(false);
                    Assert.AreEqual(7, value);

                    // Cancel the clock in the middle of a wait
                    clock.ScheduleEvent(99, TimeSpan.FromSeconds(10000));
                    CancellationTokenSource cancelToken = new NonRealTimeCancellationTokenSource(lockstepTime, TimeSpan.FromMilliseconds(500));
                    value = await clock.WaitForNextEventAsync(cancelToken.Token).ConfigureAwait(false);
                    Assert.AreEqual(0, value);

                    clock.Stop();
                    // And assert that we can't run a stopped clock
                    try
                    {
                        clock.ScheduleEvent(100, 10);
                        Assert.Fail("Should have thrown an exception");
                    }
                    catch (ObjectDisposedException) { }
                }
            }
        }

        [TestMethod]
        [DoNotParallelize]
        public async Task TestSpinwaitHighPrecisionWaitProvider()
        {
            const int NUM_THREADS = 4;
            MovingPercentile actualWaitTimes = new MovingPercentile(1000, 0.25, 0.5, 0.75);
            ILogger logger = new ConsoleLogger();
            using (IHighPrecisionWaitProvider waitProvider = new SpinwaitHighPrecisionWaitProvider(true))
            using (CancellationTokenSource cancelTokenSource = new CancellationTokenSource())
            using (IThreadPool threadPool = new CustomThreadPool(logger.Clone("ThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.AboveNormal, "ThreadPool", threadCount: NUM_THREADS))
            {
                for (int thread = 0; thread < NUM_THREADS; thread++)
                {
                    CancellationToken cancelToken = cancelTokenSource.Token;
                    threadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        Stopwatch watch = new Stopwatch();
                        while (!cancelToken.IsCancellationRequested)
                        {
                            watch.Restart();
                            await waitProvider.WaitAsync(1, CancellationToken.None); // WAIT EXACTLY ONE MILLISECOND
                            watch.Stop();
                            lock (actualWaitTimes)
                            {
                                actualWaitTimes.Add(watch.ElapsedMillisecondsPrecise());
                            }
                        }
                    });
                }

                await Task.Delay(1000);
                cancelTokenSource.Cancel();
                await threadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }

            logger.Log("Actual wait times: " + actualWaitTimes.ToString());
            Assert.AreEqual(1, actualWaitTimes.GetPercentile(0.5), 0.2);
        }

        [TestMethod]
        [DoNotParallelize]
        public async Task TestWin32HighPrecisionWaitProvider()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("Test must run on Windows platform");
                return;
            }

            const int NUM_THREADS = 16;
            MovingPercentile actualWaitTimes = new MovingPercentile(1000, 0.25, 0.5, 0.75);
            ILogger logger = new ConsoleLogger();
            using (IHighPrecisionWaitProvider waitProvider = new Win32HighPrecisionWaitProvider())
            using (CancellationTokenSource cancelTokenSource = new CancellationTokenSource())
            using (IThreadPool threadPool = new CustomThreadPool(logger.Clone("ThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, threadCount: NUM_THREADS))
            {
                CancellationToken cancelToken = cancelTokenSource.Token;

                // This thread just spams cancelled events to try and throw off the waiting thread count
                threadPool.EnqueueUserAsyncWorkItem(async () =>
                {
                    using (CancellationTokenSource cancelledSource = new CancellationTokenSource())
                    {
                        cancelledSource.Cancel();
                        while (!cancelToken.IsCancellationRequested)
                        {
                            await waitProvider.WaitAsync(5, cancelledSource.Token);
                        }
                    }
                });

                for (int thread = 0; thread < NUM_THREADS; thread++)
                {
                    threadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        Stopwatch watch = new Stopwatch();
                        while (!cancelToken.IsCancellationRequested)
                        {
                            watch.Restart();
                            await waitProvider.WaitAsync(5, CancellationToken.None); // WAIT EXACTLY FIVE MILLISECONDS
                            watch.Stop();
                            lock (actualWaitTimes)
                            {
                                actualWaitTimes.Add(watch.ElapsedMillisecondsPrecise());
                            }
                        }
                    });
                }

                // This is the total test runtime
                await Task.Delay(1000);
                cancelTokenSource.Cancel();
                await threadPool.WaitForCurrentTasksToFinish(CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }

            Console.WriteLine("Actual wait times: " + actualWaitTimes.ToString());
            Assert.AreEqual(5, actualWaitTimes.GetPercentile(0.5), 0.5);
        }
    }
}
