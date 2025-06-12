using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Test;
using Durandal.Common.Utils;
using Durandal.Common.Cache;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Time.Scheduling;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Cache
{
    [TestClass]
    public class WorkSharingCacheTests
    {
        #region Non-Async

        [TestMethod]
        public async Task TestWorkSharingCacheNormal()
        {
            ILogger logger = new ConsoleLogger();
            SlowValueProducer valueProducer = new SlowValueProducer();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(NullLogger.Singleton);
            WorkSharingCache<float, float> cache = new WorkSharingCache<float, float>(
                valueProducer.Calculate,
                cacheLifetime: TimeSpan.FromMilliseconds(100),
                cacheCapacity: 100000);

            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken testCancelToken = testCancelSource.Token;
                const int ITERATIONS = 20;
                int requestsMade = 0;
                List<Task<bool>> allTasks = new List<Task<bool>>();
                IRandom rand = new FastRandom(61116);
                for (int iter = 0; iter < ITERATIONS; iter++)
                {
                    for (int thread = 0; thread < 10; thread++)
                    {
                        IRealTimeProvider threadLocalTime = realTime.Fork("TestThread" + iter);
                        allTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                float input = rand.NextInt(0, 10);
                                float output = cache.ProduceValue(input, threadLocalTime, testCancelToken, timeout: TimeSpan.FromMilliseconds(1000));
                                bool result = output == (float)Math.Sqrt(input);
                                return result;
                            }
                            finally
                            {
                                threadLocalTime.Merge();
                            }
                        }));

                        requestsMade++;
                    }
                    
                    // can't step more than 50ms at first otherwise the work items might not start deterministically
                    realTime.Step(TimeSpan.FromMilliseconds(10));
                    // this should let the work finish
                    realTime.Step(TimeSpan.FromMilliseconds(50));
                }

                realTime.Step(TimeSpan.FromSeconds(1), 30);

                foreach (Task<bool> t in allTasks)
                {
                    Assert.IsTrue(await t.ConfigureAwait(false));
                }

                // Verify that the cache actually did something by reducing the number of producer calls
                Assert.IsTrue(valueProducer.InvocationCount < requestsMade);

                // But also validate that cache entries expired after the initial 10 were calculated
                Assert.IsTrue(valueProducer.InvocationCount > 10);
            }
        }

        [TestMethod]
        public async Task TestWorkSharingCacheLimitedCapacity()
        {
            ILogger logger = new ConsoleLogger();
            SlowValueProducer valueProducer = new SlowValueProducer();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(NullLogger.Singleton);
            WorkSharingCache<float, float> cache = new WorkSharingCache<float, float>(
                valueProducer.Calculate,
                cacheLifetime: TimeSpan.FromMilliseconds(100),
                cacheCapacity: 5);

            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken testCancelToken = testCancelSource.Token;
                const int ITERATIONS = 20;
                int requestsMade = 0;
                List<Task<bool>> allTasks = new List<Task<bool>>();
                IRandom rand = new FastRandom(61116);
                for (int iter = 0; iter < ITERATIONS; iter++)
                {
                    for (int thread = 0; thread < 10; thread++)
                    {
                        IRealTimeProvider threadLocalTime = realTime.Fork("TestThread" + iter);
                        allTasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                float input = rand.NextInt(0, 10);
                                float output = cache.ProduceValue(input, threadLocalTime, testCancelToken, timeout: TimeSpan.FromMilliseconds(1000));
                                bool result = output == (float)Math.Sqrt(input);
                                return result;
                            }
                            finally
                            {
                                threadLocalTime.Merge();
                            }
                        }));

                        requestsMade++;
                    }

                    // can't step more than 50ms at first otherwise the work items might not start deterministically
                    realTime.Step(TimeSpan.FromMilliseconds(10));
                    // this should let the work finish
                    realTime.Step(TimeSpan.FromMilliseconds(50));
                }

                realTime.Step(TimeSpan.FromSeconds(1), 30);

                foreach (Task<bool> t in allTasks)
                {
                    Assert.IsTrue(await t.ConfigureAwait(false));
                }

                // Verify that the cache actually did something by reducing the number of producer calls
                Assert.IsTrue(valueProducer.InvocationCount < requestsMade);

                // But also validate that cache entries expired after the initial 10 were calculated
                Assert.IsTrue(valueProducer.InvocationCount > 10);
            }
        }

        private class SlowValueProducer
        {
            private int _invocationCount = 0;

            public int InvocationCount => _invocationCount;

            public float Calculate(float input, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                Interlocked.Increment(ref _invocationCount);
                realTime.Wait(TimeSpan.FromMilliseconds(50), cancelToken);
                return (float)Math.Sqrt(input);
            }
        }

        [TestMethod]
        public void TestWorkSharingCacheThrowsExceptions()
        {
            ILogger logger = new ConsoleLogger();
            WorkSharingCache<int, int> cache = new WorkSharingCache<int, int>(ThrowException, TimeSpan.FromMilliseconds(500), 100000);
            for (int c = 0; c < 10; c++)
            {
                try
                {
                    int output = cache.ProduceValue(c, DefaultRealTimeProvider.Singleton, CancellationToken.None, timeout: TimeSpan.FromMilliseconds(500));
                    Assert.Fail("Should have thrown exception");
                }
                catch (ArithmeticException)
                {
                }
                catch (AggregateException e)
                {
                    Assert.IsNotNull(e.InnerException);
                    Assert.IsInstanceOfType(e.InnerException, typeof(ArithmeticException));
                }
            }
        }

        [TestMethod]
        public void TestWorkSharingCacheInvalidTimeout()
        {
            ILogger logger = new ConsoleLogger();
            WorkSharingCache<int, int> cache = new WorkSharingCache<int, int>(Timeout, TimeSpan.FromMilliseconds(500), 100000);
            for (int c = 0; c < 10; c++)
            {
                try
                {
                    int output = cache.ProduceValue(c, DefaultRealTimeProvider.Singleton, CancellationToken.None, timeout: TimeSpan.Zero);
                    Assert.Fail("Should have thrown ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }
        }

        [TestMethod]
        public async Task TestWorkSharingCacheRecoversFromTimeout()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            WorkSharingCache<int, int> cache = new WorkSharingCache<int, int>(
                Timeout,
                cacheLifetime: TimeSpan.FromSeconds(10),
                cacheCapacity: 100000);

            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken testCancelToken = testCancelSource.Token;

                // Start some tasks that will timeout
                List<Task<int>> tasks = new List<Task<int>>();
                for (int c = 0; c < 10; c++)
                {
                    IRealTimeProvider threadLocalTime = realTime.Fork("TestThread" + c);
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            // producer task takes 1000ms to run
                            return cache.ProduceValue(5, threadLocalTime, testCancelToken, timeout: TimeSpan.FromMilliseconds(100));
                        }
                        finally
                        {
                            threadLocalTime.Merge();
                        }
                    }));
                }

                realTime.Step(TimeSpan.FromSeconds(5), 100);

                // Assert that timeouts happened
                foreach (Task<int> task in tasks)
                {
                    try
                    {
                        await task.ConfigureAwait(false);
                        Assert.Fail("Should have thrown a TimeoutException");
                    }
                    catch (TimeoutException) { }
                }

                // But now the producer has finished so it should produce a value instantly
                Assert.AreEqual(5, cache.ProduceValue(5, realTime, testCancelToken, timeout: TimeSpan.FromMilliseconds(100)));
            }
        }

        private static int ThrowException(int input, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            Thread.Yield();
            throw new ArithmeticException();
        }

        private static int Timeout(int input, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            realTime.Wait(TimeSpan.FromMilliseconds(1000), cancelToken);
            return input;
        }

        #endregion

        #region Async

        [TestMethod]
        public async Task TestWorkSharingCacheAsyncNormal()
        {
            ILogger logger = new ConsoleLogger();
            SlowValueAsyncProducer valueProducer = new SlowValueAsyncProducer();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(NullLogger.Singleton);
            WorkSharingCacheAsync<float, float> cache = new WorkSharingCacheAsync<float, float>(
                valueProducer.CalculateAsync,
                cacheLifetime: TimeSpan.FromMilliseconds(200),
                cacheCapacity: 100000);

            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken testCancelToken = testCancelSource.Token;
                const int NUM_TASKS = 1000;
                Task<bool>[] allTasks = new Task<bool>[NUM_TASKS];
                IRandom rand = new FastRandom(61116);
                for (int c = 0; c < NUM_TASKS; c++)
                {
                    IRealTimeProvider threadLocalTime = realTime.Fork("TestThread" + c);
                    allTasks[c] = Task.Run(async () =>
                    {
                        try
                        {
                            float input = rand.NextInt(0, 10);
                            float output = await cache.ProduceValue(input, threadLocalTime, testCancelToken, timeout: TimeSpan.FromMilliseconds(1000)).ConfigureAwait(false);
                            bool result = output == (float)Math.Sqrt(input);
                            return result;
                        }
                        finally
                        {
                            threadLocalTime.Merge();
                        }
                    });

                    if ((c % 10) == 0)
                    {
                        // can't step more than 50ms at a time otherwise the work items might not start deterministically
                        realTime.Step(TimeSpan.FromMilliseconds(10));
                    }
                }

                realTime.Step(TimeSpan.FromSeconds(1), 30);

                for (int c = 0; c < NUM_TASKS; c++)
                {
                    Assert.IsTrue(await allTasks[c].ConfigureAwait(false));
                }

                // Verify that the cache actually did something by reducing the number of producer calls
                Assert.IsTrue(valueProducer.InvocationCount < NUM_TASKS);

                // But also validate that cache entries expired after the initial 10 were calculated
                Assert.IsTrue(valueProducer.InvocationCount > 10);
            }
        }

        [TestMethod]
        public async Task TestWorkSharingCacheAsyncLimitedCapacity()
        {
            ILogger logger = new ConsoleLogger();
            SlowValueAsyncProducer valueProducer = new SlowValueAsyncProducer();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(NullLogger.Singleton);
            WorkSharingCacheAsync<float, float> cache = new WorkSharingCacheAsync<float, float>(
                valueProducer.CalculateAsync,
                cacheLifetime: TimeSpan.FromMilliseconds(200),
                cacheCapacity: 5);

            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken testCancelToken = testCancelSource.Token;
                const int NUM_TASKS = 1000;
                Task<bool>[] allTasks = new Task<bool>[NUM_TASKS];
                IRandom rand = new FastRandom(61116);
                for (int c = 0; c < NUM_TASKS; c++)
                {
                    IRealTimeProvider threadLocalTime = realTime.Fork("TestThread" + c);
                    allTasks[c] = Task.Run(async () =>
                    {
                        try
                        {
                            float input = rand.NextInt(0, 10);
                            float output = await cache.ProduceValue(input, threadLocalTime, testCancelToken, timeout: TimeSpan.FromMilliseconds(1000)).ConfigureAwait(false);
                            bool result = output == (float)Math.Sqrt(input);
                            return result;
                        }
                        finally
                        {
                            threadLocalTime.Merge();
                        }
                    });

                    if ((c % 10) == 0)
                    {
                        // can't step more than 50ms at a time otherwise the work items might not start deterministically
                        realTime.Step(TimeSpan.FromMilliseconds(10));
                    }
                }

                realTime.Step(TimeSpan.FromSeconds(1), 30);

                for (int c = 0; c < NUM_TASKS; c++)
                {
                    Assert.IsTrue(await allTasks[c].ConfigureAwait(false));
                }

                // Verify that the cache actually did something by reducing the number of producer calls
                Assert.IsTrue(valueProducer.InvocationCount < NUM_TASKS);

                // But also validate that cache entries expired after the initial 10 were calculated
                Assert.IsTrue(valueProducer.InvocationCount > 10);
            }
        }

        private class SlowValueAsyncProducer
        {
            private int _invocationCount = 0;

            public int InvocationCount => _invocationCount;

            public async Task<float> CalculateAsync(float input, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                Interlocked.Increment(ref _invocationCount);
                await realTime.WaitAsync(TimeSpan.FromMilliseconds(50), cancelToken).ConfigureAwait(false);
                return (float)Math.Sqrt(input);
            }
        }

        [TestMethod]
        public async Task TestWorkSharingCacheAsyncThrowsExceptions()
        {
            ILogger logger = new ConsoleLogger();
            WorkSharingCacheAsync<int, int> cache = new WorkSharingCacheAsync<int, int>(ThrowExceptionAsync, TimeSpan.FromMilliseconds(500), 100000);
            for (int c = 0; c < 10; c++)
            {
                try
                {
                    int output = await cache.ProduceValue(c, DefaultRealTimeProvider.Singleton, CancellationToken.None, timeout: TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
                    Assert.Fail("Should have thrown exception");
                }
                catch (ArithmeticException)
                {
                }
            }
        }

        [TestMethod]
        public async Task TestWorkSharingCacheAsyncInvalidTimeout()
        {
            ILogger logger = new ConsoleLogger();
            WorkSharingCacheAsync<int, int> cache = new WorkSharingCacheAsync<int, int>(ThrowExceptionAsync, TimeSpan.FromMilliseconds(500), 100000);
            for (int c = 0; c < 10; c++)
            {
                try
                {
                    int output = await cache.ProduceValue(c, DefaultRealTimeProvider.Singleton, CancellationToken.None, timeout: TimeSpan.Zero).ConfigureAwait(false);
                    Assert.Fail("Should have thrown ArgumentOutOfRangeException");
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }
        }

        [TestMethod]
        public async Task TestWorkSharingCacheAsyncRecoversFromTimeout()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));
            WorkSharingCacheAsync<int, int> cache = new WorkSharingCacheAsync<int, int>(
                TimeoutAsync,
                cacheLifetime: TimeSpan.FromSeconds(10),
                cacheCapacity: 100000);

            using (CancellationTokenSource testCancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                CancellationToken testCancelToken = testCancelSource.Token;

                // Start some tasks that will timeout
                List<Task<int>> tasks = new List<Task<int>>();
                for (int c = 0; c < 10; c++)
                {
                    IRealTimeProvider threadLocalTime = realTime.Fork("TestThread" + c);
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // producer task takes 1000ms to run
                            return await cache.ProduceValue(5, threadLocalTime, testCancelToken, timeout: TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
                        }
                        finally
                        {
                            threadLocalTime.Merge();
                        }
                    }));
                }

                realTime.Step(TimeSpan.FromSeconds(5), 100);

                // Assert that timeouts happened
                foreach (Task<int> task in tasks)
                {
                    try
                    {
                        await task.ConfigureAwait(false);
                        Assert.Fail("Should have thrown a TimeoutException");
                    }
                    catch (TimeoutException) { }
                }

                // But now the producer has finished so it should produce a value instantly
                Assert.AreEqual(5, await cache.ProduceValue(5, realTime, testCancelToken, timeout: TimeSpan.FromMilliseconds(100)).ConfigureAwait(false));
            }
        }

        private static async Task<int> ThrowExceptionAsync(int input, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await Task.Yield();
            throw new ArithmeticException();
        }

        private static async Task<int> TimeoutAsync(int input, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await realTime.WaitAsync(TimeSpan.FromMilliseconds(1000), cancelToken).ConfigureAwait(false);
            return input;
        }

        #endregion
    }
}
