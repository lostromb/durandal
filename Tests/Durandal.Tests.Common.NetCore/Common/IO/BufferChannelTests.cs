using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Test;
using Durandal.Common.IO;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Collections;

namespace Durandal.Tests.Common.IO
{
    [TestClass]
    public class BufferChannelTests
    {
        [TestMethod]
        public void TestBufferedChannelSendReceive()
        {
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                for (int c = 1; c < 100; c++)
                {
                    channel.Send(c);
                    int recv = channel.Receive(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual(c, recv);
                }
            }
        }

        [TestMethod]
        public async Task TestBufferedChannelSendReceiveAsync()
        {
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                for (int c = 1; c < 100; c++)
                {
                    await channel.SendAsync(c);
                    int recv = channel.Receive(CancellationToken.None, DefaultRealTimeProvider.Singleton);
                    Assert.AreEqual(c, recv);
                }
            }
        }

        [TestMethod]
        public void TestBufferedChannelTryReceive()
        {
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                for (int c = 1; c < 100; c++)
                {
                    channel.Send(c);
                    RetrieveResult<int> rr = channel.TryReceive(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
                    Assert.IsTrue(rr.Success);
                    Assert.AreEqual(c, rr.Result);
                }
            }
        }


        [TestMethod]
        public async Task TestBufferedChannelTryReceiveAsync()
        {
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                for (int c = 1; c < 20; c++)
                {
                    ValueTask<RetrieveResult<int>> task = channel.TryReceiveAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(1));
                    await Task.Delay(10);
                    channel.Send(c);
                    RetrieveResult<int> rr = await task;
                    Assert.IsTrue(rr.Success);
                    Assert.AreEqual(c, rr.Result);
                }
            }
        }

        [TestMethod]
        public void TestBufferedChannelTryReceiveEmptyChannel()
        {
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                Stopwatch timer = Stopwatch.StartNew();
                RetrieveResult<int> rr = channel.TryReceive(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromMilliseconds(100));
                timer.Stop();
                Assert.IsFalse(rr.Success);
                Assert.IsTrue(timer.ElapsedMilliseconds > 100);
            }
        }

        [TestMethod]
        public async Task TestBufferedChannelTryReceiveAsyncEmptyChannel()
        {
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                Stopwatch timer = Stopwatch.StartNew();
                RetrieveResult<int> rr = await channel.TryReceiveAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.FromMilliseconds(100));
                timer.Stop();
                Assert.IsFalse(rr.Success);
                Assert.IsTrue(timer.ElapsedMilliseconds > 100);
            }
        }

        [TestMethod]
        public void TestBufferedChannelTryReceiveWithCancellation()
        {
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            using (CancellationTokenSource cts = new CancellationTokenSource(500))
            {
                Stopwatch timer = Stopwatch.StartNew();
                RetrieveResult<int> rr = channel.TryReceive(cts.Token, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(2));
                timer.Stop();
                Assert.IsFalse(rr.Success);
                Assert.AreEqual(500, timer.ElapsedMilliseconds, delta: 100);
                Assert.AreEqual(500, rr.LatencyMs, delta: 100);
            }
        }

        [TestMethod]
        public async Task TestBufferedChannelTryReceiveAsyncWithCancellation()
        {
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            using (CancellationTokenSource cts = new CancellationTokenSource(500))
            {
                Stopwatch timer = Stopwatch.StartNew();
                RetrieveResult<int> rr = await channel.TryReceiveAsync(cts.Token, DefaultRealTimeProvider.Singleton, TimeSpan.FromSeconds(2));
                timer.Stop();
                Assert.IsFalse(rr.Success);
                Assert.AreEqual(500, timer.ElapsedMilliseconds, delta: 100);
                Assert.AreEqual(500, rr.LatencyMs, delta: 100);
            }
        }

        [TestMethod]
        public void TestBufferedChannelTryReceiveTentativeEmptyChannel()
        {
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                RetrieveResult<int> rr = channel.TryReceive(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsFalse(rr.Success);
            }
        }

        [TestMethod]
        public async Task TestBufferedChannelTryReceiveAsyncTentativeEmptyChannel()
        {
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                RetrieveResult<int> rr = await channel.TryReceiveAsync(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsFalse(rr.Success);
            }
        }

        [TestMethod]
        public void TestBufferedChannelClear()
        {
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                for (int c = 0; c < 100; c++)
                {
                    channel.Send(c);
                }

                channel.Clear();
                RetrieveResult<int> rr = channel.TryReceive(CancellationToken.None, DefaultRealTimeProvider.Singleton, TimeSpan.Zero);
                Assert.IsFalse(rr.Success);
            }
        }

        [TestMethod]
        public async Task TestBufferedChannelSlowPath()
        {
            const int NUM_ITEMS = 100;
            CancellationTokenSource testKiller = new CancellationTokenSource();
            testKiller.CancelAfter(TimeSpan.FromSeconds(30));
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                ILogger logger = new ConsoleLogger();
                using (IThreadPool threadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestThreadPool", 4, false))
                {
                    LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);
                    ManualResetEventSlim startingGate = new ManualResetEventSlim(false);

                    // Consumer task
                    threadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        ILogger consumerLogger = logger.Clone("Consumer");
                        IRealTimeProvider consumerTime = realTime.Fork("Consumer");
                        startingGate.Wait();
                        for (int c = 0; c < NUM_ITEMS; c++)
                        {
                            int nextNumber = await channel.ReceiveAsync(testKiller.Token, consumerTime);
                            consumerLogger.Log("Got " + nextNumber);
                            Assert.AreEqual(nextNumber, c);
                        }
                        consumerTime.Merge();
                    });

                    // Producer task
                    threadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        ILogger producerLogger = logger.Clone("Producer");
                        IRealTimeProvider producerTime = realTime.Fork("Producer");
                        startingGate.Wait();
                        for (int c = 0; c < NUM_ITEMS; c++)
                        {
                            await channel.SendAsync(c);
                            producerLogger.Log("Produced " + c);
                            await producerTime.WaitAsync(TimeSpan.FromMilliseconds(10), testKiller.Token);
                        }
                        producerTime.Merge();
                    });

                    // Wait for threads to start
                    while (threadPool.TotalWorkItems < 2)
                    {
                        await Task.Delay(1);
                    }

                    logger.Log("Set starting gate");
                    startingGate.Set();

                    realTime.Step(TimeSpan.FromMilliseconds(NUM_ITEMS * 20), 100);
                    logger.Log("Advanced time");

                    // Wait for threads to stop
                    while (threadPool.TotalWorkItems > 0)
                    {
                        await Task.Delay(1);
                    }

                    logger.Log("All threads stopped");
                }
            }
        }

        [TestMethod]
        public async Task TestBufferedChannelFastPath()
        {
            const int NUM_ITEMS = 1000;
            CancellationTokenSource testKiller = new CancellationTokenSource();
            testKiller.CancelAfter(TimeSpan.FromSeconds(30));
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                ILogger logger = new ConsoleLogger();
                IRandom rand = new FastRandom();
                using (IThreadPool threadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestThreadPool", 4, false))
                {
                    IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
                    ManualResetEventSlim startingGate = new ManualResetEventSlim(false);

                    // Consumer task
                    threadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        ILogger consumerLogger = logger.Clone("Consumer");
                        startingGate.Wait();
                        for (int c = 0; c < NUM_ITEMS; c++)
                        {
                            int nextNumber = await channel.ReceiveAsync(testKiller.Token, realTime);
                            consumerLogger.Log("Got " + nextNumber);
                            Assert.AreEqual(nextNumber, c);
                            if (rand.NextInt(0, 100) == 0)
                            {
                                await Task.Delay(50);
                            }
                        }
                    });

                    // Producer task
                    threadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        ILogger producerLogger = logger.Clone("Producer");
                        startingGate.Wait();
                        for (int c = 0; c < NUM_ITEMS; c++)
                        {
                            await channel.SendAsync(c);
                            producerLogger.Log("Produced " + c);
                            if (rand.NextInt(0, 100) == 0)
                            {
                                await Task.Delay(50);
                            }
                        }
                    });

                    // Wait for threads to start
                    while (threadPool.TotalWorkItems < 2)
                    {
                        await Task.Delay(1);
                    }

                    logger.Log("Set starting gate");
                    startingGate.Set();

                    // Wait for threads to stop
                    while (threadPool.TotalWorkItems > 0)
                    {
                        await Task.Delay(1);
                    }

                    logger.Log("All threads stopped");
                }
            }
        }

        [TestMethod]
        public async Task TestBufferedChannelMultipleConsumersSlowPath()
        {
            const int NUM_ITEMS = 100;
            const int NUM_CONSUMERS = 4;
            Assert.AreEqual(0, NUM_ITEMS % NUM_CONSUMERS, "Invalid test parameters: Number of items must be evenly divided among all consumers");
            CancellationTokenSource testKiller = new CancellationTokenSource();
            testKiller.CancelAfter(TimeSpan.FromSeconds(30));
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                ILogger logger = new ConsoleLogger();
                using (IThreadPool threadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestThreadPool", NUM_CONSUMERS + 4, false))
                {
                    LockStepRealTimeProvider realTime = new LockStepRealTimeProvider(logger);
                    ManualResetEventSlim startingGate = new ManualResetEventSlim(false);

                    int eventsReceived = 0;

                    // Consumer tasks
                    for (int consumer = 0; consumer < NUM_CONSUMERS; consumer++)
                    {
                        threadPool.EnqueueUserAsyncWorkItem(async () =>
                        {
                            ILogger consumerLogger = logger.Clone("Consumer");
                            IRealTimeProvider consumerTime = realTime.Fork("Consumer");
                            startingGate.Wait();
                            for (int c = 0; c < NUM_ITEMS / NUM_CONSUMERS; c++)
                            {
                                int nextNumber = await channel.ReceiveAsync(testKiller.Token, consumerTime);
                                consumerLogger.Log("Got " + nextNumber);
                                Interlocked.Increment(ref eventsReceived);
                            }
                            consumerTime.Merge();
                        });
                    }

                    // Producer task
                    threadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        ILogger producerLogger = logger.Clone("Producer");
                        IRealTimeProvider producerTime = realTime.Fork("Producer");
                        startingGate.Wait();
                        for (int c = 0; c < NUM_ITEMS; c++)
                        {
                            await channel.SendAsync(c);
                            producerLogger.Log("Produced " + c);
                            await producerTime.WaitAsync(TimeSpan.FromMilliseconds(10), testKiller.Token);
                        }
                        producerTime.Merge();
                    });

                    // Wait for threads to start
                    while (threadPool.TotalWorkItems < 2)
                    {
                        await Task.Delay(1);
                    }

                    logger.Log("Set starting gate");
                    startingGate.Set();

                    realTime.Step(TimeSpan.FromMilliseconds(NUM_ITEMS * 20), 100);
                    logger.Log("Advanced time");

                    // Wait for threads to stop
                    while (threadPool.TotalWorkItems > 0)
                    {
                        await Task.Delay(1);
                    }

                    logger.Log("All threads stopped");
                    Assert.AreEqual(NUM_ITEMS, eventsReceived);
                }
            }
        }

        [TestMethod]
        public async Task TestBufferedChannelMultipleConsumersFastPath()
        {
            const int NUM_ITEMS = 1000;
            const int NUM_CONSUMERS = 4;
            Assert.AreEqual(0, NUM_ITEMS % NUM_CONSUMERS, "Invalid test parameters: Number of items must be evenly divided among all consumers");
            CancellationTokenSource testKiller = new CancellationTokenSource();
            testKiller.CancelAfter(TimeSpan.FromSeconds(30));
            using (BufferedChannel<int> channel = new BufferedChannel<int>())
            {
                ILogger logger = new ConsoleLogger();
                IRandom rand = new FastRandom();
                using (IThreadPool threadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestThreadPool", NUM_CONSUMERS + 4, false))
                {
                    IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
                    ManualResetEventSlim startingGate = new ManualResetEventSlim(false);

                    int eventsReceived = 0;

                    // Consumer tasks
                    for (int consumerTask = 0; consumerTask < NUM_CONSUMERS; consumerTask++)
                    {
                        threadPool.EnqueueUserAsyncWorkItem(async () =>
                        {
                            ILogger consumerLogger = logger.Clone("Consumer");
                            startingGate.Wait();
                            for (int c = 0; c < NUM_ITEMS / NUM_CONSUMERS; c++)
                            {
                                int nextNumber = await channel.ReceiveAsync(testKiller.Token, realTime);
                                consumerLogger.Log("Got " + nextNumber);
                                Interlocked.Increment(ref eventsReceived);
                                if (rand.NextInt(0, 100) == 0)
                                {
                                    await Task.Delay(50);
                                }
                            }
                        });
                    }

                    // Producer task
                    threadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        ILogger producerLogger = logger.Clone("Producer");
                        startingGate.Wait();
                        for (int c = 0; c < NUM_ITEMS; c++)
                        {
                            await channel.SendAsync(c);
                            producerLogger.Log("Produced " + c);
                            if (rand.NextInt(0, 100) == 0)
                            {
                                await Task.Delay(50);
                            }
                        }
                    });

                    // Wait for threads to start
                    while (threadPool.TotalWorkItems < 2)
                    {
                        await Task.Delay(1);
                    }

                    logger.Log("Set starting gate");
                    startingGate.Set();

                    // Wait for threads to stop
                    while (threadPool.TotalWorkItems > 0)
                    {
                        await Task.Delay(1);
                    }

                    logger.Log("All threads stopped");
                    Assert.AreEqual(NUM_ITEMS, eventsReceived);
                }
            }
        }

        [TestMethod]
        public async Task TestBufferedChannelReceiveAsyncHonorsCancellationTokensFastPath()
        {
            BufferedChannel<int> channel = new BufferedChannel<int>();
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                await channel.SendAsync(10);
                int x = await channel.ReceiveAsync(cts.Token, DefaultRealTimeProvider.Singleton);
                cts.Cancel();
                try
                {
                    await channel.ReceiveAsync(cts.Token, DefaultRealTimeProvider.Singleton);
                    Assert.Fail("Should have thrown a TaskCanceledException");
                }
                catch (TaskCanceledException)
                {
                }
            }
        }

        [TestMethod]
        public async Task TestBufferedChannelReceiveAsyncHonorsCancellationTokensSlowPath()
        {
            BufferedChannel<int> channel = new BufferedChannel<int>();
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                await channel.SendAsync(10);
                int x = await channel.ReceiveAsyncSlowPath(cts.Token, DefaultRealTimeProvider.Singleton, TimeSpan.FromMilliseconds(1));
                cts.Cancel();
                try
                {
                    await channel.ReceiveAsyncSlowPath(cts.Token, DefaultRealTimeProvider.Singleton, TimeSpan.FromMilliseconds(1));
                    Assert.Fail("Should have thrown a TaskCanceledException");
                }
                catch (TaskCanceledException)
                {
                }
            }
        }

        [TestMethod]
        public async Task TestBufferedChannelFastPathHandlesEndOfStream()
        {
            const int NUM_ITEMS = 1000;
            CancellationTokenSource testKiller = new CancellationTokenSource();
            testKiller.CancelAfter(TimeSpan.FromSeconds(30));
            using (BufferedChannel<int?> channel = new BufferedChannel<int?>())
            using (Barrier startingGate = new Barrier(3))
            {
                ILogger logger = new ConsoleLogger();
                IRandom rand = new FastRandom();
                IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
                int itemsWritten = 0;
                int itemsRead = 0;

                Task consumerTask = Task.Run(async () =>
                {
                    ILogger consumerLogger = logger.Clone("Consumer");
                    startingGate.SignalAndWait();
                    for (int c = 0; c < NUM_ITEMS; c++)
                    {
                        int? nextNumber = await channel.ReceiveAsync(testKiller.Token, realTime);
                        if (nextNumber.HasValue)
                        {
                            Interlocked.Increment(ref itemsRead);
                            consumerLogger.Log("Got " + nextNumber);
                            Assert.AreEqual(nextNumber, c);
                            if (rand.NextInt(0, 100) == 0)
                            {
                                await Task.Delay(50);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                });

                Task producerTask = Task.Run(async () =>
                {
                    ILogger producerLogger = logger.Clone("Producer");
                    startingGate.SignalAndWait();
                    for (int c = 0; c < NUM_ITEMS; c++)
                    {
                        Interlocked.Increment(ref itemsWritten);
                        await channel.SendAsync(c);
                        producerLogger.Log("Produced " + c);
                        if (rand.NextInt(0, 100) == 0)
                        {
                            await Task.Delay(50);
                        }
                    }

                    channel.Dispose();
                });

                // Wait for threads to start
                startingGate.SignalAndWait();

                await producerTask;
                await consumerTask;

                logger.Log("All threads stopped");
                Assert.AreEqual(itemsWritten, itemsRead);
            }
        }

        [TestMethod]
        public async Task TestRendevousChannelSingleReader()
        {
            const int NUM_ITEMS = 1000;
            CancellationTokenSource testKiller = new CancellationTokenSource();
            testKiller.CancelAfter(TimeSpan.FromSeconds(30));
            RendezvousChannel<int> channel = new RendezvousChannel<int>();
            EventOnlyLogger eventLogger = new EventOnlyLogger("Main", LogLevel.All);
            ILogger consoleLogger = new ConsoleLogger("Main", LogLevel.All);
            ILogger logger = new AggregateLogger("Main", new TaskThreadPool(), eventLogger, consoleLogger);
            IRandom rand = new FastRandom();
            using (IThreadPool threadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestThreadPool", 4, false))
            {
                IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
                ManualResetEventSlim startingGate = new ManualResetEventSlim(false);

                // Consumer task
                threadPool.EnqueueUserAsyncWorkItem(async () =>
                {
                    ILogger consumerLogger = logger.Clone("Consumer");
                    startingGate.Wait();
                    for (int c = 0; c < NUM_ITEMS; c++)
                    {
                        int nextNumber = await channel.ReceiveAsync(testKiller.Token, realTime);
                        consumerLogger.Log("Got " + nextNumber);
                        Assert.AreEqual(nextNumber, c);
                        if (rand.NextInt(0, 100) == 0)
                        {
                            await Task.Delay(50);
                        }
                    }
                });

                // Producer task
                threadPool.EnqueueUserAsyncWorkItem(async () =>
                {
                    ILogger producerLogger = logger.Clone("Producer");
                    startingGate.Wait();
                    for (int c = 0; c < NUM_ITEMS; c++)
                    {
                        await channel.SendAsync(c);
                        producerLogger.Log("Produced " + c);
                        if (rand.NextInt(0, 100) == 0)
                        {
                            await Task.Delay(50);
                        }
                    }
                });

                // Wait for threads to start
                while (threadPool.TotalWorkItems < 2)
                {
                    await Task.Delay(1);
                }

                logger.Log("Set starting gate");
                startingGate.Set();

                // Wait for threads to stop
                while (threadPool.TotalWorkItems > 0)
                {
                    await Task.Delay(1);
                }

                logger.Log("All threads stopped");

                ILoggingHistory history = eventLogger.History;
                Assert.AreEqual(0, history.FilterByCriteria(LogLevel.Err).ToList().Count);
            }
        }

        [TestMethod]
        public async Task TestRendevousChannelMultipleConsumers()
        {
            const int NUM_ITEMS = 1000;
            const int NUM_CONSUMERS = 4;
            Assert.AreEqual(0, NUM_ITEMS % NUM_CONSUMERS, "Invalid test parameters: Number of items must be evenly divided among all consumers");
            CancellationTokenSource testKiller = new CancellationTokenSource();
            testKiller.CancelAfter(TimeSpan.FromSeconds(30));
            RendezvousChannel<int> channel = new RendezvousChannel<int>();
            EventOnlyLogger eventLogger = new EventOnlyLogger("Main", LogLevel.All);
            ILogger consoleLogger = new ConsoleLogger("Main", LogLevel.All);
            ILogger logger = new AggregateLogger("Main", new TaskThreadPool(), eventLogger, consoleLogger);
            IRandom rand = new FastRandom();
            using (IThreadPool threadPool = new CustomThreadPool(logger, NullMetricCollector.Singleton, DimensionSet.Empty, ThreadPriority.Normal, "TestThreadPool", NUM_CONSUMERS + 4, false))
            {
                IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
                ManualResetEventSlim startingGate = new ManualResetEventSlim(false);

                int eventsReceived = 0;

                // Consumer tasks
                for (int consumerTask = 0; consumerTask < NUM_CONSUMERS; consumerTask++)
                {
                    threadPool.EnqueueUserAsyncWorkItem(async () =>
                    {
                        ILogger consumerLogger = logger.Clone("Consumer");
                        startingGate.Wait();
                        for (int c = 0; c < NUM_ITEMS / NUM_CONSUMERS; c++)
                        {
                            int nextNumber = await channel.ReceiveAsync(testKiller.Token, realTime);
                            consumerLogger.Log("Got " + nextNumber);
                            Interlocked.Increment(ref eventsReceived);
                            if (rand.NextInt(0, 100) == 0)
                            {
                                await Task.Delay(50);
                            }
                        }
                    });
                }

                // Producer task
                threadPool.EnqueueUserAsyncWorkItem(async () =>
                {
                    ILogger producerLogger = logger.Clone("Producer");
                    startingGate.Wait();
                    for (int c = 0; c < NUM_ITEMS; c++)
                    {
                        await channel.SendAsync(c);
                        producerLogger.Log("Produced " + c);
                        if (rand.NextInt(0, 100) == 0)
                        {
                            await Task.Delay(50);
                        }
                    }
                });

                // Wait for threads to start
                while (threadPool.TotalWorkItems < 2)
                {
                    await Task.Delay(1);
                }

                logger.Log("Set starting gate");
                startingGate.Set();

                // Wait for threads to stop
                while (threadPool.TotalWorkItems > 0)
                {
                    await Task.Delay(1);
                }

                logger.Log("All threads stopped");
                Assert.AreEqual(NUM_ITEMS, eventsReceived);

                ILoggingHistory history = eventLogger.History;
                Assert.AreEqual(0, history.FilterByCriteria(LogLevel.Err).ToList().Count);
            }
        }

        [TestMethod]
        public void TestBasicBuffer()
        {
            BasicBuffer<int> buf = new BasicBuffer<int>(100);
            Assert.AreEqual(100, buf.Capacity);
            Assert.AreEqual(0, buf.Available);
            buf.Write(5);
            Assert.AreEqual(1, buf.Available);
            Assert.AreEqual(5, buf.Read());
            int[] values = new int[3] { 1, 2, 3 };
            buf.Write(values);
            Assert.AreEqual(3, buf.Available);
            Assert.AreEqual(1, buf.Read());
            Assert.AreEqual(2, buf.Read());
            Assert.AreEqual(3, buf.Read());
            values = new int[5] { 1, 2, 3, 4, 5 };
            buf.Write(values, 3, 2);
            Assert.AreEqual(2, buf.Available);
            Assert.AreEqual(4, buf.Read());
            Assert.AreEqual(5, buf.Read());

            for (int c = 0; c < 80; c++)
            {
                buf.Write(c);
            }

            for (int c = 0; c < 4263; c++)
            {
                buf.Write(c + 80);
                Assert.AreEqual(c, buf.Read());
            }

            for (int c = 0; c < 20; c++)
            {
                buf.Write(c);
            }

            try
            {
                buf.Write(0);
                Assert.Fail("Should have thrown an IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }
        }

        [TestMethod]
        public void TestBasicBufferShort()
        {
            BasicBufferShort buf = new BasicBufferShort(100);
            Assert.AreEqual(100, buf.Capacity);
            Assert.AreEqual(0, buf.Available);
            buf.Write(5);
            Assert.AreEqual(1, buf.Available);
            Assert.AreEqual(5, buf.Read());
            short[] values = new short[3] { 1, 2, 3 };
            buf.Write(values);
            Assert.AreEqual(3, buf.Available);
            Assert.AreEqual(1, buf.Read());
            Assert.AreEqual(2, buf.Read());
            Assert.AreEqual(3, buf.Read());
            values = new short[5] { 1, 2, 3, 4, 5 };
            buf.Write(values, 3, 2);
            Assert.AreEqual(2, buf.Available);
            Assert.AreEqual(4, buf.Read());
            Assert.AreEqual(5, buf.Read());

            for (short c = 0; c < 80; c++)
            {
                buf.Write(c);
            }

            for (short c = 0; c < 4263; c++)
            {
                buf.Write((short)(c + 80));
                Assert.AreEqual(c, buf.Read());
            }

            for (short c = 0; c < 20; c++)
            {
                buf.Write(c);
            }

            try
            {
                buf.Write(0);
                Assert.Fail("Should have thrown an IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }
        }

        [TestMethod]
        public async Task TestLockFreeMemoryBuffer()
        {
            ILogger logger = new ConsoleLogger();
            IRandom rand = new FastRandom(500);
            for (int loop = 0; loop < 100; loop++)
            {
                int bufSize = rand.NextInt(100, 1000);
                int transferSize = rand.NextInt(bufSize, 10000);
                LockFreeMemoryBuffer buffer = new LockFreeMemoryBuffer(bufSize);
                byte[] writeBuf = new byte[transferSize];
                byte[] readBuf = new byte[transferSize];
                for (int c = 0; c < transferSize; c++)
                {
                    writeBuf[c] = (byte)(c % 256);
                }

                // Create a background thread that produces incrementing bytes
                Task writeTask = Task.Run(async () =>
                {
                    try
                    {
                        int amountWritten = 0;
                        while (amountWritten < transferSize)
                        {
                            int toWrite = Math.Min(transferSize - amountWritten, rand.NextInt(1, bufSize * 10));
                            await buffer.WriteAsync(writeBuf, amountWritten, toWrite, CancellationToken.None);
                            amountWritten += toWrite;
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log(e, LogLevel.Err);
                        Assert.Fail("Write task threw an exception");
                    }
                    finally
                    {
                        //logger.Log("Write finished");
                    }
                });

                // And read it on the main thread
                Stopwatch timer = Stopwatch.StartNew();
                Task<int> readTask = buffer.ReadAsync(readBuf, 0, transferSize, CancellationToken.None, DefaultRealTimeProvider.Singleton);
                int totalRead = await readTask;
                await writeTask;
                //logger.Log("Read finished " + totalRead);
                timer.Stop();
                double bytesPerMillisecond = totalRead / (timer.ElapsedMillisecondsPrecise());
                double kbPerSecond = (bytesPerMillisecond * 1000) / 1024;
                logger.Log("Transfer speed " + kbPerSecond + " KB/s");

                Assert.IsTrue(ArrayExtensions.ArrayEquals(readBuf, writeBuf));
            }
        }
    }
}
