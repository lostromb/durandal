using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.IO;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// The purpose of this class is to create an asynchronous queue which ingests events, batches them and then processes them somehow.
    /// The typical scenario is for an instrumentation queue that sends results to a remote database in batches.
    /// The desirable properties are that ingestion is low-latency, processing is done asynchronously in batches for better performance, and
    /// the processor handles things like bad network or no connectivity gracefully while still doing all it can to guarantee delivery.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class BatchedDataProcessor<T> : IMetricSource, IDisposable
    {
        private const int INGESTION_SEMAPHORE_COUNT = 16;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly string _processorName;
        private readonly BatchedDataProcessorConfig _config;

        /// <summary>
        /// If the queue is full, this is used to force callers to wait on Ingest() until there is room available. Only if allowDroppedWorkItems = false
        /// </summary>
        private readonly SemaphoreSlim _ingestionSemaphore;

        private ConcurrentQueue<PooledBuffer<T>> _backlog = new ConcurrentQueue<PooledBuffer<T>>();
        private ConcurrentQueue<T> _ingestedItems = new ConcurrentQueue<T>();
        private int _eventsInBacklog = 0;
        private IRealTimeProvider _realTime;
        private DateTimeOffset _nextProcessTime = DateTimeOffset.MinValue;
        private DateTimeOffset _nextIngestTime = DateTimeOffset.MinValue;
        private Task _backgroundTask;
        private TimeSpan _currentBackoffTime;
        private readonly CancellationTokenSource _cancelTokenSource;
        private readonly CancellationToken _cancelToken;
        private readonly ILogger _logger = NullLogger.Singleton; // TODO add logging to this class
        private int _disposed = 0;

        /// <summary>
        /// Creats a batched data processor that will start ingesting and processing data in real time
        /// </summary>
        /// <param name="processorName">The name of this processor, for logging and debugging</param>
        /// <param name="config">Configuration for this processor</param>
        /// <param name="realTime">Real time implementation</param>
        /// <param name="bootstrapLogger">Logger for error messages</param>
        /// <param name="metrics">Metric collector</param>
        /// <param name="dimensions">Dimensions for metrics</param>
        public BatchedDataProcessor(
            string processorName,
            BatchedDataProcessorConfig config,
            IRealTimeProvider realTime,
            ILogger bootstrapLogger = null,
            IMetricCollector metrics = null,
            DimensionSet dimensions = null)
        {
            _metrics = new WeakPointer<IMetricCollector>(metrics ?? NullMetricCollector.Singleton);
            _cancelTokenSource = new CancellationTokenSource();
            _cancelToken = _cancelTokenSource.Token;
            _logger = bootstrapLogger ?? NullLogger.Singleton;
            _processorName = processorName;
            _metricDimensions = dimensions ?? DimensionSet.Empty;
            _metricDimensions = _metricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BatchProcessorName, _processorName));
            _metrics.Value.AddMetricSource(this);
            _config = config.AssertNonNull(nameof(config));
            _realTime = realTime.Fork("BatchProcessorMainTimer");
            _nextProcessTime = _realTime.Time + _config.DesiredInterval;
            _nextIngestTime = _realTime.Time + _config.DesiredInterval;
            _currentBackoffTime = TimeSpan.Zero;
            if (!_config.AllowDroppedItems)
            {
                _ingestionSemaphore = new SemaphoreSlim(INGESTION_SEMAPHORE_COUNT);
            }

            _backgroundTask = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(BackgroundTask);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~BatchedDataProcessor()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// This will run continuously as a thread in the background, and will do the following work:
        /// - Periodically check how many items are ingested. If there are enough to form a batch, create a batch of work and queue it to the backlog
        /// - If there are too many items in the backlog, prune them
        /// - If it is time to start processing the backlog, delegate backlog items to worker threads, run those threads, and then handle their results.
        /// </summary>
        /// <returns></returns>
        private async Task BackgroundTask()
        {
            T[] scratch = new T[_config.BatchSize];
            int scratchIdx = 0;
            PooledBuffer<T>[] enqueuedInputs = new PooledBuffer<T>[_config.MaxSimultaneousProcesses];
            Task<bool>[] runningTasks = new Task<bool>[_config.MaxSimultaneousProcesses];
            //T[] mainProcessingBuffer = new T[_maxSimultaneousProcesses * _batchSize];
            //int mainProcessingBufferIdx = 0;

            // Time increment to wait if there's no work to be performed.
            // 30ms minimum time is chosen specifically so it won't trigger a high-precision wait from the time provider.
            TimeSpan waitLoopInterval = TimeSpan.FromMilliseconds(Math.Max(30, _config.DesiredInterval.TotalMilliseconds / 10));

            while (!_cancelToken.IsCancellationRequested)
            {
                bool didAnyWork = false;
                bool isHoldingIngestionSemaphore = false;

                // Clear the ingestion queue to form a batch, and add it to the backlog
                if (_nextIngestTime < _realTime.Time || _ingestedItems.ApproximateCount >= _config.BatchSize)
                {
                    didAnyWork = true;
                    // Break large batches into smaller parts so as not to overwhelm the processor
                    while (_ingestedItems.ApproximateCount > 0 && _eventsInBacklog < _config.MaxBacklogSize)
                    {
                        scratchIdx = 0;
                        T ingestedItem;
                        while (scratchIdx < _config.BatchSize && _ingestedItems.TryDequeue(out ingestedItem))
                        {
                            scratch[scratchIdx++] = ingestedItem;
                        }

                        //_logger?.Log("Creating a batch of " + scratchIdx + " items");
                        PooledBuffer<T> ingestedBatch = BufferPool<T>.Rent(scratchIdx);
                        ArrayExtensions.MemCopy(scratch, 0, ingestedBatch.Buffer, 0, scratchIdx);
                        _backlog.Enqueue(ingestedBatch);
                        _eventsInBacklog += ingestedBatch.Length;
                    }

                    // Prune old items from the backlog by clearing the ingestion queue if backlog is full
                    if (_eventsInBacklog >= _config.MaxBacklogSize)
                    {
                        if (_config.AllowDroppedItems)
                        {
                            // If we are allowed to drop work items, drop them
                            int itemsCleared = 0;
                            T dequeuedItem;
                            while (_ingestedItems.TryDequeue(out dequeuedItem))
                            {
                                // Handle the case where batched items are disposable and we
                                // are dropping them from the queue (can happen if we are passing
                                // things like pooled buffers through this processor)
                                (dequeuedItem as IDisposable)?.Dispose();
                                itemsCleared++;
                            }

                            if (itemsCleared > 0)
                            {
                                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_BatchProcessor_ItemsDropped, _metricDimensions, itemsCleared);
                                _logger?.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata, "{0} is culling {1} items from the queue", _processorName, itemsCleared);
                            }
                        }
                        else
                        {
                            // Acquire all ingestion semaphores. This will cause anyone trying to enqueue more work items to block.
                            for (int sem = 0; sem < INGESTION_SEMAPHORE_COUNT; sem++)
                            {
                                await _ingestionSemaphore.WaitAsync().ConfigureAwait(false);
                            }

                            isHoldingIngestionSemaphore = true;
                        }
                    }

                    _nextIngestTime = _realTime.Time + _config.DesiredInterval;
                }

                if (!isHoldingIngestionSemaphore && _currentBackoffTime > TimeSpan.Zero)
                {
                    //_logger?.Log("Wait " + _currentBackoffTime);
                    didAnyWork = true;
                    await _realTime.WaitAsync(_currentBackoffTime, _cancelToken).ConfigureAwait(false);
                }

                // See if we should start processing the backlog
                if (_eventsInBacklog > 0 && (isHoldingIngestionSemaphore || _nextProcessTime < _realTime.Time))
                {
                    didAnyWork = true;
                    bool allSuccess = true;
                    bool allFailure = true;

                    if (_config.MaxSimultaneousProcesses == 1)
                    {
                        // Special optimized case if the batch process is serialized.
                        // Don't bother queueing separate tasks, just await them directly.
                        int numEventsQueued = 0;
                        PooledBuffer<T> queuedBacklogItem;
                        while (_backlog.TryDequeue(out queuedBacklogItem))
                        {
                            enqueuedInputs[numEventsQueued] = queuedBacklogItem;
                            _eventsInBacklog -= enqueuedInputs[numEventsQueued].Length;
                            PooledBuffer<T> thisInput = enqueuedInputs[numEventsQueued];
                            
                            try
                            {
                                allSuccess = await Process(new ArraySegment<T>(thisInput.Buffer, 0, thisInput.Length), _realTime).ConfigureAwait(false);
                            }
                            catch (Exception e)
                            {
                                _logger.Log(e, LogLevel.Err);
                                allSuccess = false;
                            }

                            allFailure = !allSuccess;
                            if (!allSuccess)
                            {
                                allSuccess = false;
                                _backlog.Enqueue(thisInput);
                                _eventsInBacklog += thisInput.Length;
                                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_BatchProcessor_ItemsFailed, _metricDimensions, thisInput.Length);
                            }
                            else
                            {
                                allFailure = false;
                                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_BatchProcessor_ItemsSucceeded, _metricDimensions, thisInput.Length);
                                thisInput.Dispose();
                                enqueuedInputs[numEventsQueued] = null;
                            }
                        }
                    }
                    else
                    {
                        int numEventsQueued = 0;
                        PooledBuffer<T> queuedBacklogItem;
                        while (numEventsQueued < _config.MaxSimultaneousProcesses && _backlog.TryDequeue(out queuedBacklogItem))
                        {
                            enqueuedInputs[numEventsQueued] = queuedBacklogItem;
                            _eventsInBacklog -= enqueuedInputs[numEventsQueued].Length;
                            // Treading a bit carefully - we have to make sure this closure is honored in the thread
                            // As long as we declare local copies of each variable it should be fine
                            PooledBuffer<T> closure = enqueuedInputs[numEventsQueued];
                            IRealTimeProvider threadLocalTime = _realTime.Fork("BatchProcessorChildWorkerThread");
                            runningTasks[numEventsQueued] = Task.Run(async () =>
                            {
                                try
                                {
                                    return await Process(new ArraySegment<T>(closure.Buffer, 0, closure.Length), threadLocalTime).ConfigureAwait(false);
                                }
                                catch (Exception e)
                                {
                                    _logger.Log(e, LogLevel.Err);
                                    return false;
                                }
                                finally
                                {
                                    threadLocalTime.Merge();
                                }
                            });

                            numEventsQueued++;
                        }

                        for (int c = 0; c < numEventsQueued; c++)
                        {
                            if (_realTime.IsForDebug)
                            {
                                // Special case for non-realtime -- need to consume VT while waiting for background processes to finish
                                while (!runningTasks[c].IsFinished())
                                {
                                    await _realTime.WaitAsync(TimeSpan.FromMilliseconds(1), _cancelToken).ConfigureAwait(false);
                                }
                            }

                            bool success = await runningTasks[c].ConfigureAwait(false);
                            PooledBuffer<T> thisInput = enqueuedInputs[c];
                            if (!success)
                            {
                                //_logger?.Log("One task failed");
                                allSuccess = false;
                                _backlog.Enqueue(thisInput);
                                _eventsInBacklog += thisInput.Length;
                                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_BatchProcessor_ItemsFailed, _metricDimensions, thisInput.Length);
                            }
                            else
                            {
                                allFailure = false;
                                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_BatchProcessor_ItemsSucceeded, _metricDimensions, thisInput.Length);
                                thisInput.Dispose();
                                enqueuedInputs[c] = null;
                            }
                        }
                    }
                    
                    if (allSuccess)
                    {
                        //_logger?.Log("All success");
                        // If the backlog is empty then we can cool off for a while
                        if (_eventsInBacklog == 0)
                        {
                            //Debug.WriteLine("I'm taking a break for " + _desiredInterval + "ms");
                            _nextProcessTime = _realTime.Time + _config.DesiredInterval;
                        }
                    }
                    else if (allFailure)
                    {
                        //_logger?.Log("All failure");
                        _currentBackoffTime = TimeSpan.FromTicks(Math.Min(_currentBackoffTime.Ticks * 2, _config.MaximumBackoffTime.Ticks));
                        _currentBackoffTime = TimeSpan.FromTicks(Math.Max(_currentBackoffTime.Ticks, _config.MinimumBackoffTime.Ticks));
                        //Debug.WriteLine("I'm backing off for " + _currentBackoffTime);
                    }
                    else
                    {
                        // If at least some of the tasks succeeded, reset the backoff time
                        _currentBackoffTime = TimeSpan.Zero;
                    }
                }

                if (isHoldingIngestionSemaphore)
                {
                    // If we blocked all ingestion because the queue was full, we can release it again
                    _ingestionSemaphore.Release(INGESTION_SEMAPHORE_COUNT);
                }

                if (!didAnyWork)
                {
                    // Sleep for a tick to prevent busy loops.
                    await _realTime.WaitAsync(waitLoopInterval, _cancelToken).ConfigureAwait(false);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                Stop();
                _backgroundTask.Await(); // BUGBUG what if this hangs forever?
                _cancelTokenSource.Dispose();
                _ingestionSemaphore?.Dispose();
            }
        }

        /// <summary>
        /// Stops the background processing of events in this queue
        /// </summary>
        public void Stop()
        {
            _cancelTokenSource.Cancel();
        }

        /// <summary>
        /// Ingests a new event or value into the processing queue.
        /// </summary>
        /// <param name="value"></param>
        public void Ingest(T value)
        {
            if (_config.AllowDroppedItems)
            {
                _ingestedItems.Enqueue(value);
            }
            else
            {
                _ingestionSemaphore.Wait();
                try
                {
                    _ingestedItems.Enqueue(value);
                }
                finally
                {
                    _ingestionSemaphore.Release();
                }
            }
        }

        public async Task Flush(IRealTimeProvider realTime, TimeSpan maxTimeToWait)
        {
            int maxTime = (int)maxTimeToWait.TotalMilliseconds;
            int timeWaited = 0;
            if (maxTime == 0)
            {
                // Non-blocking path
                _nextProcessTime = realTime.Time - TimeSpan.FromSeconds(1);
                _nextIngestTime = realTime.Time - TimeSpan.FromSeconds(1);
            }
            else
            {
                // Partially blocking path
                while ((_eventsInBacklog > 0 || _ingestedItems.ApproximateCount > 0) && timeWaited < maxTime)
                {
                    _nextProcessTime = realTime.Time - TimeSpan.FromSeconds(1);
                    _nextIngestTime = _nextProcessTime;
                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), _cancelToken).ConfigureAwait(false);
                    timeWaited += 10;
                }
            }
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_BatchProcessor_ItemsQueued, _metricDimensions, (double)_ingestedItems.ApproximateCount);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_BatchProcessor_BacklogItems, _metricDimensions, (double)_eventsInBacklog);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_BatchProcessor_BacklogPercent, _metricDimensions, ((double)_eventsInBacklog * 100) / (double)_config.MaxBacklogSize);
        }

        public void InitializeMetrics(IMetricCollector collector)
        {
        }

        /// <summary>
        /// Implemented by your subclass to process a batch of items or events.
        /// </summary>
        /// <param name="items">The batch of items to be processed</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>True if processing was a success</returns>
        protected abstract ValueTask<bool> Process(ArraySegment<T> items, IRealTimeProvider realTime);
    }
}
