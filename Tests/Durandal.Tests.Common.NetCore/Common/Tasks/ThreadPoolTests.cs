using Durandal.Common.Collections;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Tasks
{
    [TestClass]
    public class ThreadPoolTests
    {
        [ClassInitialize]
        public static void InitializeClass(TestContext context)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DefaultRealTimeProvider.HighPrecisionWaitProvider = new Win32HighPrecisionWaitProvider();
            }
        }

        [ClassCleanup]
        public static void CleanupClass()
        {
            DefaultRealTimeProvider.HighPrecisionWaitProvider = null;
        }

        [TestMethod]
        public void TestCustomThreadpoolResiliency()
        {
            const int numThreads = 4;
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            using (CustomThreadPool pool = new CustomThreadPool(logger.Clone("TestDriverThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestDriver", numThreads))
            {
                ThreadpoolTest(pool);
            }
        }

        [TestMethod]
        public void TestTaskThreadpoolResiliency()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            using (TaskThreadPool pool = new TaskThreadPool())
            {
                ThreadpoolTest(pool);
            }
        }

        [TestMethod]
        public void TestSystemThreadpoolResiliency()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            using (SystemThreadPool pool = new SystemThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty))
            {
                ThreadpoolTest(pool);
            }
        }

        private void ThreadpoolTest(IThreadPool pool)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                // Try and craft a set of tasks which will exercise the thread pool's backlog filling and clearing, since
                // that is where most race conditions might happen
                cts.CancelAfter(TimeSpan.FromSeconds(60));
                int threadsFinished = 0;
                int expectedCount = 0;
                int maxTasks = 100000;
                while (expectedCount < maxTasks)
                {
                    // Wait for backlog to drain completely
                    while (pool.TotalWorkItems > 0)
                    {
                        DefaultRealTimeProvider.Singleton.Wait(TimeSpan.FromMilliseconds(1), cts.Token);
                        cts.Token.ThrowIfCancellationRequested();
                    }

                    // Then dump lots of new tasks at once
                    while (pool.TotalWorkItems < (pool.ThreadCount * 2) && expectedCount < maxTasks)
                    {
                        pool.EnqueueUserWorkItem(() =>
                        {
                            if (Interlocked.Add(ref threadsFinished, 1) % 1000 == 0)
                            {
                                throw new NullReferenceException("Occasional errors happen");
                            }

                        });
                        expectedCount++;
                    }
                }

                // And ensure they finish
                while (pool.TotalWorkItems > 0)
                {
                    DefaultRealTimeProvider.Singleton.Wait(TimeSpan.FromMilliseconds(1), cts.Token);
                    cts.Token.ThrowIfCancellationRequested();
                }

                Assert.AreEqual(expectedCount, threadsFinished);
                Assert.AreEqual(0, pool.TotalWorkItems);
                Assert.AreEqual(0, pool.RunningWorkItems);
            }
        }

        [TestMethod]
        public async Task TestCustomThreadpoolThrowsExceptions()
        {
            try
            {
                const int numThreads = 4;
                ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
                using (IThreadPool pool = new CustomThreadPool(logger.Clone("TestDriverThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestDriver", numThreads, hideExceptions: false))
                {
                    for (int c = 0; c < 10; c++)
                    {
                        pool.EnqueueUserWorkItem(() =>
                        {
                            throw new ArithmeticException("Illegal math");
                        });

                        await Task.Delay(10);
                    }
                }

                Assert.Fail("Should have thrown an ArithmeticException");
            }
            catch (ArithmeticException) { }
        }

        [TestMethod]
        public void TestCustomThreadpoolWorkItemCount()
        {
            const int numThreads = 4;
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            using (CustomThreadPool pool = new CustomThreadPool(logger.Clone("TestDriverThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestDriver", numThreads))
            {
                ThreadpoolWorkItemCounterTest(pool);
            }
        }

        [Ignore]
        [TestMethod]
        public void TestTaskThreadpoolWorkItemCount()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            using (TaskThreadPool pool = new TaskThreadPool())
            {
                ThreadpoolWorkItemCounterTest(pool);
            }
        }

        [TestMethod]
        public void TestSystemThreadpoolWorkItemCount()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            using (SystemThreadPool pool = new SystemThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty))
            {
                ThreadpoolWorkItemCounterTest(pool);
            }
        }

        private void ThreadpoolWorkItemCounterTest(IThreadPool pool)
        {
            using (CancellationTokenSource cancelTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                bool holdingLock = false;
                bool isRunning = false;
                Assert.AreEqual(0, pool.RunningWorkItems);
                Assert.AreEqual(0, pool.TotalWorkItems);

                pool.EnqueueUserWorkItem(() =>
                {
                    isRunning = true;
                    holdingLock = true;
                    while (holdingLock)
                    {
                        DefaultRealTimeProvider.Singleton.Wait(TimeSpan.FromMilliseconds(1), cancelTokenSource.Token);
                    }

                    isRunning = false;
                });

                SpinWait.SpinUntil(() => isRunning || cancelTokenSource.Token.IsCancellationRequested);
                Assert.AreEqual(1, pool.RunningWorkItems);
                Assert.AreEqual(1, pool.TotalWorkItems);

                holdingLock = false;
                SpinWait.SpinUntil(() => pool.RunningWorkItems == 0 || cancelTokenSource.Token.IsCancellationRequested);
                Assert.AreEqual(0, pool.RunningWorkItems);
                Assert.AreEqual(0, pool.TotalWorkItems);
                Assert.AreEqual(false, isRunning);

                pool.EnqueueUserAsyncWorkItem(async () =>
                {
                    isRunning = true;
                    for (int c = 0; c < 10; c++)
                    {
                        holdingLock = true;
                        while (holdingLock)
                        {
                            await DefaultRealTimeProvider.Singleton.WaitAsync(TimeSpan.FromMilliseconds(1), cancelTokenSource.Token).ConfigureAwait(false);
                        }
                    }
                    isRunning = false;
                });

                SpinWait.SpinUntil(() => isRunning || cancelTokenSource.Token.IsCancellationRequested);

                for (int loop = 0; loop < 10; loop++)
                {
                    SpinWait.SpinUntil(() => holdingLock || cancelTokenSource.Token.IsCancellationRequested);
                    Assert.AreEqual(1, pool.RunningWorkItems);
                    Assert.AreEqual(1, pool.TotalWorkItems);
                    holdingLock = false;
                }

                SpinWait.SpinUntil(() => pool.RunningWorkItems == 0 || cancelTokenSource.Token.IsCancellationRequested);
                Assert.AreEqual(0, pool.RunningWorkItems);
                Assert.AreEqual(0, pool.TotalWorkItems);
                Assert.AreEqual(false, isRunning);
            }
        }

        /// <summary>
        /// Tests that when multiple fixed-capacity thread pools share the same parent pool, their threads are tracked and limited separately
        /// rather then being tied to the overall scheduling rate of the parent pool
        /// </summary>
        [TestMethod]
        public void TestSharedFixedCapacityThreadPools()
        {
            ILogger logger = new ConsoleLogger();
            IMetricCollector reporter = NullMetricCollector.Singleton;
            DimensionSet dimensions = DimensionSet.Empty;
            IRandom fastRandom = new FastRandom();

            using (IThreadPool unlimitedPool = new CustomThreadPool(logger, reporter, DimensionSet.Empty, ThreadPriority.Normal, "WorkerPool", 8))
            {
                IThreadPool fixedPool1 = new FixedCapacityThreadPool(
                    unlimitedPool,
                    NullLogger.Singleton,
                    reporter,
                    dimensions,
                    "FixedWorkerPool1",
                    2,
                    ThreadPoolOverschedulingBehavior.ShedExcessWorkItems);
                IThreadPool fixedPool2 = new FixedCapacityThreadPool(
                    unlimitedPool,
                    NullLogger.Singleton,
                    reporter,
                    dimensions,
                    "FixedWorkerPool2",
                    2,
                    ThreadPoolOverschedulingBehavior.ShedExcessWorkItems);

                Stopwatch timeRan = Stopwatch.StartNew();
                StaticAverage pool1AverageTasks = new StaticAverage();
                StaticAverage pool2AverageTasks = new StaticAverage();
                StaticAverage parentPoolAverageTasks = new StaticAverage();
                while (timeRan.Elapsed < TimeSpan.FromSeconds(5))
                {
                    fixedPool1.EnqueueUserWorkItem(() =>
                    {
                        for (int c = 0; c < 1000000; c++)
                        {
                            fastRandom.NextInt();
                        }
                    });
                    fixedPool2.EnqueueUserWorkItem(() =>
                    {
                        for (int c = 0; c < 1000000; c++)
                        {
                            fastRandom.NextInt();
                        }
                    });
                    pool1AverageTasks.Add(fixedPool1.TotalWorkItems);
                    pool2AverageTasks.Add(fixedPool2.TotalWorkItems);
                    parentPoolAverageTasks.Add(unlimitedPool.TotalWorkItems);
                }

                Assert.AreEqual(2, pool1AverageTasks.Average, 0.5);
                Assert.AreEqual(2, pool2AverageTasks.Average, 0.5);
                Assert.AreEqual(4, parentPoolAverageTasks.Average, 0.5);
            }
        }
    }
}
