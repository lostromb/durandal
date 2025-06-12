using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using Durandal.Common.Collections;
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
using Durandal.Tests.Common.Tasks;

namespace Durandal.Tests.Common.Tasks
{
    [TestClass]
    public class BatchedDataProcessorTests
    {
        /// <summary>
        /// Tests basic function of the batched data processor under load
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestBatchedDataProcessorBasic()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));

            using (BasicMultithreadedProcessor processor = new BasicMultithreadedProcessor(logger.Clone("BatchProcessor"), lockStepTime, 0))
            {
                int increment = 0;

                // Simulate 10 seconds of processing, and ensure all events get processed and none are dropped
                for (int step = 0; step < 100; step++)
                {
                    for (int batch = 0; batch < 40; batch++) // this should run the processor at 50% capacity
                    {
                        processor.Ingest(increment.ToString());
                        increment++;
                    }

                    lockStepTime.Step(TimeSpan.FromMilliseconds(10));
                }

                lockStepTime.Step(TimeSpan.FromMilliseconds(10000), 1000);
                await processor.Flush(lockStepTime, TimeSpan.FromSeconds(10));

                // Assert that all items were processed and nothing was dropped
                HashSet<string> processedItems = new HashSet<string>();
                ConcurrentQueue<string> processedQueue = processor.ProcessedItems;
                string rr;
                while (processedQueue.TryDequeue(out rr))
                {
                    Assert.IsFalse(processedItems.Contains(rr), "Item " + rr + " was processed multiple times");
                    processedItems.Add(rr);
                }

                for (int c = 0; c < increment; c++)
                {
                    Assert.IsTrue(processedItems.Contains(c.ToString()), "Item " + c + " was not processed");
                }
            }
        }

        /// <summary>
        /// Tests that batched data processor guarantees some resiliency even when batch tasks occasionally fail
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestBatchedDataProcessorRecoversFromErrors()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));

            using (BasicMultithreadedProcessor processor = new BasicMultithreadedProcessor(logger.Clone("BatchProcessor"), lockStepTime, 0.2))
            {
                int increment = 0;

                // Simulate 10 seconds of processing, and ensure all events get processed and none are dropped
                for (int step = 0; step < 100; step++)
                {
                    for (int batch = 0; batch < 40; batch++) // this should run the processor at 50% capacity
                    {
                        processor.Ingest(increment.ToString());
                        increment++;
                    }

                    lockStepTime.Step(TimeSpan.FromMilliseconds(10));
                }

                lockStepTime.Step(TimeSpan.FromMilliseconds(10000), 1000);
                await processor.Flush(lockStepTime, TimeSpan.FromSeconds(10));

                // Assert that all items were processed and nothing was dropped
                HashSet<string> processedItems = new HashSet<string>();
                ConcurrentQueue<string> processedQueue = processor.ProcessedItems;
                string rr;
                while (processedQueue.TryDequeue(out rr))
                {
                    Assert.IsFalse(processedItems.Contains(rr), "Item " + rr + " was processed multiple times");
                    processedItems.Add(rr);
                }

                for (int c = 0; c < increment; c++)
                {
                    Assert.IsTrue(processedItems.Contains(c.ToString()), "Item " + c + " was not processed");
                }
            }
        }

        /// <summary>
        /// Tests basic function of the batched data processor when overloaded
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestBatchedDataProcessorShedsItemsWhenOverloaded()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));

            using (BasicMultithreadedProcessor processor = new BasicMultithreadedProcessor(logger.Clone("BatchProcessor"), lockStepTime, 0, true))
            {
                int increment = 0;

                // Simulate 10 seconds of processing
                for (int step = 0; step < 100; step++)
                {
                    for (int batch = 0; batch < 150; batch++) // this is about 175% capacity for the processor
                    {
                        processor.Ingest(increment.ToString());
                        increment++;
                    }

                    lockStepTime.Step(TimeSpan.FromMilliseconds(10));
                }

                lockStepTime.Step(TimeSpan.FromMilliseconds(10000), 1000);
                await processor.Flush(lockStepTime, TimeSpan.FromSeconds(10));

                // Count up the number of processed items
                HashSet<string> processedItems = new HashSet<string>();
                ConcurrentQueue<string> processedQueue = processor.ProcessedItems;
                string rr;
                while (processedQueue.TryDequeue(out rr))
                {
                    Assert.IsFalse(processedItems.Contains(rr), "Item " + rr + " was processed multiple times");
                    processedItems.Add(rr);
                }

                Assert.IsTrue(processedItems.Count < increment, "Expected some of the input items to be dropped");
                Assert.IsTrue(processedItems.Count > increment / 2, "Expected at least half of the enqueued items to be processed");
            }
        }


        /// <summary>
        /// Tests basic function of the batched data processor when overloaded
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestBatchedDataProcessorDoesntShedItemsWhenOverloadedIfItsNotAllowed()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));

            using (BasicMultithreadedProcessor processor = new BasicMultithreadedProcessor(logger.Clone("BatchProcessor"), lockStepTime, 0, false))
            {
                int increment = 0;

                // Simulate 10 seconds of processing, and ensure all events get processed and none are dropped
                for (int step = 0; step < 100; step++)
                {
                    for (int batch = 0; batch < 100; batch++) // this is about 125% capacity for the processor
                    {
                        processor.Ingest(increment.ToString());
                        increment++;
                    }

                    lockStepTime.Step(TimeSpan.FromMilliseconds(10));
                }

                lockStepTime.Step(TimeSpan.FromMilliseconds(15000), 1000);
                await processor.Flush(lockStepTime, TimeSpan.FromSeconds(10));

                // Assert that all items were processed and nothing was dropped
                HashSet<string> processedItems = new HashSet<string>();
                ConcurrentQueue<string> processedQueue = processor.ProcessedItems;
                string rr;
                while (processedQueue.TryDequeue(out rr))
                {
                    Assert.IsFalse(processedItems.Contains(rr), "Item " + rr + " was processed multiple times");
                    processedItems.Add(rr);
                }

                for (int c = 0; c < increment; c++)
                {
                    Assert.IsTrue(processedItems.Contains(c.ToString()), "Item " + c + " was not processed");
                }
            }
        }

        private class BasicMultithreadedProcessor : BatchedDataProcessor<string>
        {
            private readonly ConcurrentQueue<string> _processedItems = new ConcurrentQueue<string>();
            private double _errorRate = 0;
            private IRandom _rand = new FastRandom(444);

            public BasicMultithreadedProcessor(ILogger bootstrapLogger, IRealTimeProvider realTime, double errorRate = 0, bool allowShedding = false)
                : base(
                      "TestWorker",
                      new BatchedDataProcessorConfig()
                      {
                          BatchSize = 100,
                          MaxSimultaneousProcesses = 8,
                          DesiredInterval = TimeSpan.FromMilliseconds(50),
                          MinimumBackoffTime = TimeSpan.FromMilliseconds(100),
                          MaximumBackoffTime = TimeSpan.FromMilliseconds(1000),
                          MaxBacklogSize = 10000,
                          AllowDroppedItems = allowShedding,
                      }, 
                      realTime,
                      bootstrapLogger,
                      NullMetricCollector.Singleton,
                      DimensionSet.Empty)
            {
                _errorRate = errorRate;
            }

            public ConcurrentQueue<string> ProcessedItems => _processedItems;

            protected override async ValueTask<bool> Process(ArraySegment<string> items, IRealTimeProvider realTime)
            {
                await realTime.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);

                if (_errorRate <= _rand.NextDouble())
                {
                    foreach (string x in items)
                    {
                        _processedItems.Enqueue(x);
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Tests that only a single thread ever runs when concurrency count is set to one in a batched processor
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task TestBatchedDataProcessorSinglethreaded()
        {
            ILogger logger = new ConsoleLogger();
            LockStepRealTimeProvider lockStepTime = new LockStepRealTimeProvider(logger.Clone("LockStepTime"));

            using (BasicSinglethreadedProcessor processor = new BasicSinglethreadedProcessor(logger.Clone("BatchProcessor"), lockStepTime))
            {
                int increment = 0;

                // Simulate 10 seconds of processing, and ensure all events get processed and none are dropped
                for (int step = 0; step < 100; step++)
                {
                    for (int batch = 0; batch < 6; batch++)
                    {
                        processor.Ingest(increment);
                        increment++;
                    }

                    lockStepTime.Step(TimeSpan.FromMilliseconds(10));
                }

                lockStepTime.Step(TimeSpan.FromMilliseconds(1000));
                await processor.Flush(lockStepTime, TimeSpan.FromSeconds(10));
                Assert.IsFalse(processor.Failed);
            }
        }

        private class BasicSinglethreadedProcessor : BatchedDataProcessor<int>
        {
            private int _lock = 0;
            private bool _failed = false;

            public BasicSinglethreadedProcessor(ILogger bootstrapLogger, IRealTimeProvider realTime)
                : base(
                    "TestWorker",
                    new BatchedDataProcessorConfig()
                    {
                        BatchSize = 100,
                        MaxSimultaneousProcesses = 1,
                        DesiredInterval = TimeSpan.FromMilliseconds(50),
                        MinimumBackoffTime = TimeSpan.FromMilliseconds(100),
                        MaximumBackoffTime = TimeSpan.FromMilliseconds(1000),
                        MaxBacklogSize = 10000,
                        AllowDroppedItems = true,
                    },
                    realTime,
                    bootstrapLogger,
                    NullMetricCollector.Singleton,
                    DimensionSet.Empty)
            {
            }

            public bool Failed => _failed;

            protected override async ValueTask<bool> Process(ArraySegment<int> items, IRealTimeProvider realTime)
            {
                // Detect if multiple threads are in the critical area
                if (Interlocked.Exchange(ref _lock, 1) != 0)
                {
                    _failed = true;
                }

                await realTime.WaitAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);

                if (Interlocked.Exchange(ref _lock, 0) != 1)
                {
                    _failed = true;
                }

                return true;
            }
        }
    }
}
