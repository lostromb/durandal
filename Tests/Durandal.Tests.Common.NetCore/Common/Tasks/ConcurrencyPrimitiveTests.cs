using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Tests.Common.Tasks
{
    [TestClass]
    [DoNotParallelize]
    public class ConcurrencyPrimitiveTests
    {
        [TestMethod]
        public void TestResourcePoolEmptyObjectInstantiation()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            using (ResourcePool<ArgumentNullException> pool = new ResourcePool<ArgumentNullException>(5, logger, DimensionSet.Empty, "TestPool"))
            {
                PooledResource<ArgumentNullException> t = null;
                Assert.IsTrue(pool.TryGetResource(out t));
                Assert.IsTrue(t.Value.ParamName == null);
                Assert.IsTrue(pool.ReleaseResource(t));
            }
        }

        [TestMethod]
        public void TestResourcePoolPopulatedObjectInstantiation()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            using (ResourcePool<ArgumentNullException> pool = new ResourcePool<ArgumentNullException>(5, logger, DimensionSet.Empty, "TestPool", "ParamName"))
            {
                PooledResource<ArgumentNullException> t = null;
                Assert.IsTrue(pool.TryGetResource(out t));
                Assert.IsTrue(t.Value.ParamName == "ParamName");
                Assert.IsTrue(pool.ReleaseResource(t));
            }
        }

        [TestMethod]
        public void TestResourcePoolLinear()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            IList<string> resources = new List<string>();
            resources.Add("one");
            resources.Add("two");
            resources.Add("three");
            resources.Add("four");
            using (ResourcePool<string> pool = new ResourcePool<string>(resources, logger, DimensionSet.Empty, "TestPool"))
            {
                PooledResource<string> t = null, one = null, two = null, three = null, four = null, dummy = null;
                Assert.IsTrue(pool.TryGetResource(out four));
                Assert.AreEqual("two", four.Value);
                Assert.IsTrue(pool.TryGetResource(out three));
                Assert.AreEqual("three", three.Value);
                Assert.IsTrue(pool.TryGetResource(out one));
                Assert.AreEqual("four", one.Value);
                Assert.IsTrue(pool.TryGetResource(out two));
                Assert.AreEqual("one", two.Value);

                // Pool is occupied - should return false
                Assert.IsFalse(pool.TryGetResource(out t));

                Assert.IsTrue(pool.ReleaseResource(three));
                Assert.IsTrue(pool.TryGetResource(out three));

                // Attempting to double-lease - don't allow it
                try
                {
                    pool.TryGetResource(out dummy, TimeSpan.FromMilliseconds(10));
                    Assert.Fail("Should have thrown an exception for double-leasing");
                }
                catch (Exception) { }

                Assert.IsTrue(pool.ReleaseResource(one));
                Assert.IsTrue(pool.TryGetResource(out one));
                try
                {
                    Assert.IsFalse(pool.TryGetResource(out dummy, TimeSpan.FromMilliseconds(10)));
                    Assert.Fail("Should have thrown an exception for double-leasing");
                }
                catch (Exception) { }

                Assert.IsTrue(pool.ReleaseResource(two));
                Assert.IsTrue(pool.TryGetResource(out two));
                try
                {
                    Assert.IsFalse(pool.TryGetResource(out dummy, TimeSpan.FromMilliseconds(10)));
                    Assert.Fail("Should have thrown an exception for double-leasing");
                }
                catch (Exception) { }

                Assert.IsTrue(pool.ReleaseResource(four));
                try
                {
                    Assert.IsTrue(pool.TryGetResource(out dummy));
                    Assert.Fail("Should have thrown an exception for double-leasing");
                }
                catch (Exception) { }

                Assert.IsFalse(pool.TryGetResource(out t, TimeSpan.FromMilliseconds(10)));
            }
        }

        [TestMethod]
        public async Task TestResourcePoolLinearAsync()
        {
            ILogger logger = new ConsoleLogger("Test", LogLevel.Err | LogLevel.Ins | LogLevel.Std | LogLevel.Vrb | LogLevel.Wrn);
            IList<string> resources = new List<string>();
            resources.Add("one");
            resources.Add("two");
            resources.Add("three");
            resources.Add("four");
            using (ResourcePool<string> pool = new ResourcePool<string>(resources, logger, DimensionSet.Empty, "TestPool"))
            {
                PooledResource<string> one = null, two = null, three = null, four = null;
                RetrieveResult<PooledResource<string>> rr;
                rr = await pool.TryGetResourceAsync().ConfigureAwait(false);
                Assert.IsTrue(rr.Success);
                four = rr.Result;
                Assert.AreEqual("two", four.Value);
                rr = await pool.TryGetResourceAsync().ConfigureAwait(false);
                Assert.IsTrue(rr.Success);
                three = rr.Result;
                Assert.AreEqual("three", three.Value);
                rr = await pool.TryGetResourceAsync().ConfigureAwait(false);
                Assert.IsTrue(rr.Success);
                one = rr.Result;
                Assert.AreEqual("four", one.Value);
                rr = await pool.TryGetResourceAsync().ConfigureAwait(false);
                Assert.IsTrue(rr.Success);
                two = rr.Result;
                Assert.AreEqual("one", two.Value);

                // Pool is occupied - should return false
                rr = await pool.TryGetResourceAsync().ConfigureAwait(false);
                Assert.IsFalse(rr.Success);

                Assert.IsTrue(pool.ReleaseResource(three));
                rr = await pool.TryGetResourceAsync().ConfigureAwait(false);
                Assert.IsTrue(rr.Success);
                three = rr.Result;

                rr = await pool.TryGetResourceAsync(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
                Assert.IsFalse(rr.Success);
            }
        }

        [TestMethod]
        public void TestResourcePoolThreaded()
        {
            ILogger logger = new DetailedConsoleLogger("Test");
            IList<object> resources = new List<object>();
            resources.Add(new Cube3f(0, 0, 0, 0, 0, 0));
            resources.Add(new Vector3f());
            resources.Add(new object());
            resources.Add(new BasicBuffer<int>(1));
            using (ResourcePool<object> pool = new ResourcePool<object>(resources, logger, DimensionSet.Empty, "TestPool"))
            {
                using (CustomThreadPool threads = new CustomThreadPool(logger.Clone("TestDriverThreadPool"), NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestDriver", 16))
                {
                    for (int c = 0; c < threads.ThreadCount; c++)
                    {
                        threads.EnqueueUserWorkItem(() =>
                        {
                            IRandom rand = new FastRandom();
                            PooledResource<object> t = null;
                            for (int z = 0; z < 100; z++)
                            {
                                Assert.IsNull(t);
                                Assert.IsTrue(pool.TryGetResource(out t, TimeSpan.FromSeconds(10)));
                                Thread.Sleep(rand.NextInt(0, 20));
                                Assert.IsTrue(pool.ReleaseResource(t));
                                Assert.IsNull(t);
                            }
                        });
                    }

                    int timeWaited = 0;
                    while (threads.TotalWorkItems > 0)
                    {
                        Thread.Sleep(10);
                        timeWaited += 10;
                        Assert.IsTrue(timeWaited < 20000);
                    }
                }
            }
        }

        [TestMethod]
        public void TestAtomicFlag()
        {
            int flag = 0;
            Assert.IsFalse(AtomicOperations.GetAndClearFlag(ref flag));
            AtomicOperations.SetFlag(ref flag);
            Assert.IsTrue(AtomicOperations.GetAndClearFlag(ref flag));
            Assert.IsFalse(AtomicOperations.GetAndClearFlag(ref flag));
            AtomicOperations.SetFlag(ref flag, true);
            AtomicOperations.SetFlag(ref flag, false);
            Assert.IsFalse(AtomicOperations.GetAndClearFlag(ref flag));
        }

        [TestMethod]
        public void TestReaderWriterLockAsync()
        {
            ReaderWriterLockAsync readWriteLock = new ReaderWriterLockAsync(16);
            SemaphoreSlim outputMutex = new SemaphoreSlim(1);
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom();
            int currentValue = 0;

            List<Tuple<bool, int>> outputs = new List<Tuple<bool, int>>();

            // Start a bunch of reader and writer tasks
            // Have them write their output deterministically to the output list
            using (IThreadPool testPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ThreadPool", 20))
            {
                for (int c = 0; c < 1000; c++)
                {
                    float next = rand.NextFloat();
                    if (next < 0.05f)
                    {
                        // Schedule a write task
                        testPool.EnqueueUserAsyncWorkItem(async () =>
                        {
                            int handle = await readWriteLock.EnterWriteLockAsync();
                            currentValue += 1;
                            await Task.Yield();
                            outputs.Add(new Tuple<bool, int>(true, currentValue));
                            readWriteLock.ExitWriteLock(handle);
                        });
                    }
                    else if (next < 0.1f)
                    {
                        // Schedule an upgraded read task
                        testPool.EnqueueUserAsyncWorkItem(async () =>
                        {
                            int handle = await readWriteLock.EnterReadLockAsync();
                            await outputMutex.WaitAsync();
                            outputs.Add(new Tuple<bool, int>(false, currentValue));
                            outputMutex.Release();
                            handle = await readWriteLock.UpgradeToWriteLockAsync(handle);
                            currentValue += 1;
                            outputs.Add(new Tuple<bool, int>(true, currentValue));
                            handle = readWriteLock.DowngradeToReadLock(handle);
                            await outputMutex.WaitAsync();
                            outputs.Add(new Tuple<bool, int>(false, currentValue));
                            outputMutex.Release();
                            readWriteLock.ExitReadLock(handle);
                        });
                    }
                    else if (next < 0.15f)
                    {
                        // Schedule a task to get the write lock but then abort halfway through
                        testPool.EnqueueUserAsyncWorkItem(async () =>
                        {
                            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(rand.NextInt(1, 400))))
                            {
                                try
                                {
                                    int handle = await readWriteLock.EnterWriteLockAsync(cts.Token);
                                    readWriteLock.ExitWriteLock(handle);
                                }
                                catch (TaskCanceledException) { }
                            }
                        });
                    }
                    else if (next < 0.2f)
                    {
                        // Schedule a task to get the read lock but then abort halfway through
                        testPool.EnqueueUserAsyncWorkItem(async () =>
                        {
                            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(rand.NextInt(1, 400))))
                            {
                                try
                                {
                                    int handle = await readWriteLock.EnterReadLockAsync(cts.Token);
                                    readWriteLock.ExitReadLock(handle);
                                }
                                catch (TaskCanceledException) { }
                            }
                        });
                    }
                    else
                    {
                        // Schedule a regular read task
                        testPool.EnqueueUserAsyncWorkItem(async () =>
                        {
                            int handle = await readWriteLock.EnterReadLockAsync();
                            await outputMutex.WaitAsync();
                            outputs.Add(new Tuple<bool, int>(false, currentValue));
                            outputMutex.Release();
                            readWriteLock.ExitReadLock(handle);
                        });
                    }
                }

                int timeWaited = 0;
                while (testPool.TotalWorkItems > 0 && timeWaited < 30000)
                {
                    Thread.Sleep(10);
                    timeWaited += 10;
                }

                Assert.AreEqual(0, testPool.TotalWorkItems);
            }

            // Now make sure all the outputs were in order
            currentValue = 0;
            foreach (Tuple<bool, int> output in outputs)
            {
                // Console.WriteLine("{0} {1}", output.Item1, output.Item2);
                if (output.Item1)
                {
                    currentValue++;
                }

                Assert.AreEqual(currentValue, output.Item2);
            }
        }

        [TestMethod]
        public async Task TestReaderWriterLockAsyncWriteStarvation()
        {
            const int NUM_READERS = 20;
            using (CancellationTokenSource testTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                ReaderWriterLockAsync readWriteLock = new ReaderWriterLockAsync(NUM_READERS);
                ILogger logger = new ConsoleLogger();
                int readerIncrement = 0;

                // Here's a thread which continuously spawns readers on the lock
                Task readerFactory = Task.Run(() =>
                {
                    logger.Log("Producer thread started", LogLevel.Vrb);
                    CancellationToken timeoutToken = testTimeout.Token;
                    using (IThreadPool basePool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ThreadPool", NUM_READERS))
                    using (IThreadPool fixedPool = new FixedCapacityThreadPool(basePool,
                        logger.Clone("FixedCapacityPool"),
                        NullMetricCollector.Singleton,
                        DimensionSet.Empty,
                        "FixedCapacityPool",
                        NUM_READERS))
                    {
                        while (!timeoutToken.IsCancellationRequested)
                        {
                            logger.Log("Starting read thread", LogLevel.Vrb);
                            fixedPool.EnqueueUserAsyncWorkItem(async () =>
                            {
                                int handle = await readWriteLock.EnterReadLockAsync(timeoutToken);
                                int readerNum = Interlocked.Increment(ref readerIncrement);
                                try
                                {

                                    logger.Log("Reader " + readerNum + " has the lock", LogLevel.Vrb);
                                    await Task.Delay(500, timeoutToken);
                                }
                                finally
                                {
                                    logger.Log("Reader " + readerNum + " is releasing the lock", LogLevel.Vrb);
                                    readWriteLock.ExitReadLock(handle);
                                }
                            });
                        }
                    }
                });

                // Now try and acquire the write lock. See how many iterations it takes
                logger.Log("Waiting for readers to start", LogLevel.Vrb);
                while (readerIncrement < NUM_READERS)
                {
                    await Task.Delay(1);
                }

                logger.Log("Starting to get write lock", LogLevel.Vrb);
                int startReaderCount = readerIncrement;
                int hWrite = await readWriteLock.EnterWriteLockAsync(testTimeout.Token);
                try
                {
                    logger.Log("Got write lock", LogLevel.Vrb);
                    int endReaderCount = readerIncrement;
                    Assert.IsFalse(testTimeout.Token.IsCancellationRequested);
                    Assert.IsTrue(endReaderCount - startReaderCount <= NUM_READERS * 2);
                }
                finally
                {
                    readWriteLock.ExitWriteLock(hWrite);
                    testTimeout.Cancel();
                    await readerFactory;
                }
            }
        }

        [TestMethod]
        public void TestReaderWriterLockMicroInvalidSizeSmall()
        {
            try
            {
                new ReaderWriterLockMicro(0);
                Assert.Fail("Expected an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void TestReaderWriterLockMicroInvalidSizeLarge()
        {
            try
            {
                new ReaderWriterLockMicro(512);
                Assert.Fail("Expected an ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [TestMethod]
        public void TestReaderWriterLockMicroInvalidSizeNonPowerOfTwo()
        {
            try
            {
                new ReaderWriterLockMicro(63);
                Assert.Fail("Expected an ArgumentException");
            }
            catch (ArgumentException) { }
        }

        [TestMethod]
        public void TestReaderWriterLockMicro()
        {
            const int numWriteThreads = 2;
            const int numReadThreads = 8;

            ReaderWriterLockMicro readWriteLock = new ReaderWriterLockMicro(4);
            ILogger logger = new ConsoleLogger();
            int currentValue = 0;

            List<Tuple<bool, int>> outputs = new List<Tuple<bool, int>>();

            // Start a bunch of reader and writer tasks
            // Have them write their output deterministically to the output list
            using (IThreadPool testPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "ThreadPool", numWriteThreads + numReadThreads))
            using (CancellationTokenSource cts = new CancellationTokenSource())
            using (Barrier barrier = new Barrier(numWriteThreads + numReadThreads + 1))
            {
                CancellationToken cancelToken = cts.Token;
                for (int thread = 0; thread < numWriteThreads; thread++)
                {
                    // Schedule a write task
                    testPool.EnqueueUserWorkItem(() =>
                    {
                        barrier.SignalAndWait();
                        try
                        {
                            while (!cancelToken.IsCancellationRequested)
                            {
                                readWriteLock.EnterWriteLock();
                                currentValue += 1;
                                outputs.Add(new Tuple<bool, int>(true, currentValue));
                                readWriteLock.ExitWriteLock();
                            }
                        }
                        finally
                        {
                            barrier.SignalAndWait();
                        }
                    });
                }

                for (int thread = 0; thread < numReadThreads; thread++)
                {
                    // Schedule a regular read task
                    testPool.EnqueueUserWorkItem(() =>
                    {
                        barrier.SignalAndWait();
                        try
                        {
                            while (!cancelToken.IsCancellationRequested)
                            {
                                uint handle = readWriteLock.EnterReadLock();
                                lock (outputs)
                                {
                                    outputs.Add(new Tuple<bool, int>(false, currentValue));
                                }

                                readWriteLock.ExitReadLock(handle);
                            }
                        }
                        finally
                        {
                            barrier.SignalAndWait();
                        }
                    });
                }

                barrier.SignalAndWait();
                cts.CancelAfter(TimeSpan.FromMilliseconds(500));
                barrier.SignalAndWait();
            }

            // Now make sure all the outputs were in order
            Assert.AreNotEqual(0, outputs.Count);
            currentValue = 0;
            foreach (Tuple<bool, int> output in outputs)
            {
                // Console.WriteLine("{0} {1}", output.Item1, output.Item2);
                if (output.Item1)
                {
                    currentValue++;
                }

                Assert.AreEqual(currentValue, output.Item2);
            }
        }

        [TestMethod]
        public async Task TestAutoResetEventAsync()
        {
            AutoResetEventAsync signal = new AutoResetEventAsync(false);
            ILogger logger = new ConsoleLogger();
            int currentValue = 0;
            const int NUM_TASKS = 10000;
            List<Task> tasks = new List<Task>();

            for (int c = 0; c < NUM_TASKS; c++)
            {
                // Queue up a bunch of tasks that are "chained" together like a fuse, waiting for the signal, updating value, and then setting the signal for the next task
                tasks.Add(Task.Run(async () =>
                    {
                        await signal.WaitAsync().ConfigureAwait(false);
                        int localVal = Thread.VolatileRead(ref currentValue);

                        // Hey! We have the value! Does anyone else want to jump in here?
                        for (int d = 0; d < 10; d++)
                        {
                            await Task.Yield();
                            Thread.Yield();
                        }

                        // Guess not. Update the value then signal the next thread
                        Thread.VolatileWrite(ref currentValue, localVal + 1);
                        signal.Set();
                    }));
            }

            // Light the fuse
            signal.Set();

            // Then wait for all tasks to finish
            foreach (Task t in tasks)
            {
                await t;
            }

            Assert.AreEqual(NUM_TASKS, Thread.VolatileRead(ref currentValue));
        }

        [TestMethod]
        public async Task TestAsyncLockSlim()
        {
            ILogger logger = new ConsoleLogger();
            AsyncLockSlim toTest = new AsyncLockSlim();
            long numberLocked = 0;
            long numberInterlocked = 0;
            const int NUM_THREADS = 16;
            int threadsFinished = 0;
            using (IThreadPool threadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestPool", NUM_THREADS, false))
            {
                CancellationTokenSource cancelToken = new CancellationTokenSource();
                using (ManualResetEventSlim startingPistol = new ManualResetEventSlim(false))
                {
                    for (int c = 0; c < NUM_THREADS; c++)
                    {
                        threadPool.EnqueueUserAsyncWorkItem(async () =>
                        {
                            try
                            {
                                IRandom rand = new FastRandom();
                                startingPistol.Wait();
                                while (!cancelToken.IsCancellationRequested)
                                {
                                    int method = rand.NextInt(0, 4);
                                    if (method == 0)
                                    {
                                        await toTest.GetLockAsync();
                                        try
                                        {
                                            numberLocked = numberLocked + 1;
                                        }
                                        finally
                                        {
                                            toTest.Release();
                                        }
                                    }
                                    else if (method == 1)
                                    {
                                        await toTest.GetLockAsync();
                                        try
                                        {
                                            numberLocked = numberLocked + 1;
                                            await Task.Delay(1);
                                        }
                                        finally
                                        {
                                            toTest.Release();
                                        }
                                    }
                                    else if (method == 2)
                                    {
                                        toTest.GetLock();
                                        try
                                        {
                                            numberLocked = numberLocked + 1;
                                        }
                                        finally
                                        {
                                            toTest.Release();
                                        }
                                    }
                                    else
                                    {
                                        toTest.GetLock();
                                        try
                                        {
                                            numberLocked = numberLocked + 1;
                                            await Task.Delay(1);
                                        }
                                        finally
                                        {
                                            toTest.Release();
                                        }
                                    }

                                    Interlocked.Increment(ref numberInterlocked);
                                }
                            }
                            finally
                            {
                                Interlocked.Increment(ref threadsFinished);
                            }
                        });
                    }

                    startingPistol.Set();
                    cancelToken.CancelAfter(TimeSpan.FromSeconds(5));

                    while (threadsFinished < NUM_THREADS)
                    {
                        await Task.Delay(100);
                    }
                }
            }

            Assert.AreEqual(numberInterlocked, numberLocked);
        }

        [TestMethod]
        public void TestAsyncLockTryGetLock()
        {
            ILogger logger = new ConsoleLogger();
            CancellationTokenSource testFinishedCancelizer = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            CancellationToken testFinishedCancelToken = testFinishedCancelizer.Token;

            AsyncLockSlim lock1 = new AsyncLockSlim();
            AsyncLockSlim lock2 = new AsyncLockSlim();
            int value1 = 0;
            int value2 = 0;
            
            Barrier startingPistol = new Barrier(3);
            Task thread1 = Task.Run(() =>
            {
                startingPistol.SignalAndWait();
                try
                {
                    while (!testFinishedCancelToken.IsCancellationRequested)
                    {
                        if (lock1.TryGetLock())
                        {
                            if (lock2.TryGetLock())
                            {
                                value1++;
                                value2++;
                                lock2.Release();
                            }

                            lock1.Release();
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e);
                }
            });

            Task thread2 = Task.Run(() =>
            {
                startingPistol.SignalAndWait();
                try
                {
                    while (!testFinishedCancelToken.IsCancellationRequested)
                    {
                        if (lock2.TryGetLock())
                        {
                            if (lock1.TryGetLock())
                            {
                                value1++;
                                value2++;
                                lock1.Release();
                            }

                            lock2.Release();
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Log(e);
                }
            });

            startingPistol.SignalAndWait();

            Assert.IsTrue(thread1.AwaitWithTimeout(10000), "Deadlocked");
            Assert.IsTrue(thread2.AwaitWithTimeout(10000), "Deadlocked");

            Assert.AreNotEqual(0, value1);
            Assert.AreEqual(value1, value2);
        }
    }
}
