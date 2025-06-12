using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using System.Runtime.ExceptionServices;
using Durandal.Common.Instrumentation;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.Collections;
using Durandal.Common.Cache;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// Thread pool that is backed by a privately owned group of threads, so you are guaranteed to not be shared on any other system pool.
    /// The downside of this is that managing the threads manually incurs a slight bit of overhead compared to other pools.
    /// OPT: When we queue async tasks to the custom thread pool the thread is still blocked during await() operations.
    /// You can hide this by oversaturating the thread count, but it's still an inefficiency. Really, we should
    /// get rid of this class entirely and use a custom task scheduler context that provides its own threads.
    /// </summary>
    public class CustomThreadPool : IThreadPool
    {
        /// <summary>
        /// The interval of time (in ms) that threads will check to see if the pool is being disposed of.
        /// </summary>
        private const int CANCEL_QUERY_INTERVAL_MS = 100;

        private readonly int _threadCount;
        private readonly string _poolName;
        private readonly ILogger _logger;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly WorkerThread[] _threads;
        private readonly CancellationTokenSource _cancelToken;
        private readonly AutoResetEvent _queueActivity;
        private readonly Queue<Func<Task>> _workItems;
        private readonly ReaderWriterLockSlim _threadArrayLock;
        private readonly LockFreeCache<ExceptionDispatchInfo> _exceptions;
        private readonly bool _hideExceptions;
        private readonly ThreadPriority _threadPriority;

        /// <summary>
        /// current count of running tasks
        /// </summary>
        private int _runningWorkItems = 0;

        /// <summary>
        /// current count of all running + queued tasks
        /// </summary>
        private int _totalWorkItems = 0;

        /// <summary>
        /// monotonously incrementing count of all tasks that have ever finished
        /// </summary>
        private long _tasksCompleted = 0;

        private int _disposed = 0;

        internal delegate Func<Task> GetWorkItem();

        public CustomThreadPool(
            ILogger logger,
            IMetricCollector metrics,
            DimensionSet dimensions,
            ThreadPriority threadPriority = ThreadPriority.Normal,
            string poolName = "ThreadPool",
            int threadCount = -1,
            bool hideExceptions = true)
        {
            _threadCount = threadCount;
            if (_threadCount <= 0)
            {
                _threadCount = Environment.ProcessorCount;
            }

            // Sanity - make sure thread count can't go below 3
            _threadCount = System.Math.Max(3, _threadCount);

            _threadPriority = threadPriority;
            _poolName = poolName;
            _logger = logger;
            _metrics = new WeakPointer<IMetricCollector>(metrics ?? NullMetricCollector.Singleton);
            _dimensions = dimensions ?? DimensionSet.Empty;
            _dimensions = _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_ThreadPoolName, _poolName));
            _threads = new WorkerThread[_threadCount];
            _cancelToken = new CancellationTokenSource();
            _workItems = new Queue<Func<Task>>();
            _queueActivity = new AutoResetEvent(false);
            _threadArrayLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _hideExceptions = hideExceptions;
            _exceptions = new LockFreeCache<ExceptionDispatchInfo>(8);

            _threadArrayLock.EnterWriteLock();
            // Start up the threads
            for (int c = 0; c < _threadCount; c++)
            {
                _threads[c] = new WorkerThread(_poolName, c, _logger.Clone("ThreadPool-" + _poolName + ":" + c), DequeueUserWorkItem, WorkItemFinishedCallback, _threadPriority, _exceptions);
                _threads[c].Start();
            }
            _threadArrayLock.ExitWriteLock();

            _metrics.Value.AddMetricSource(this);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        ~CustomThreadPool()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns the total number of threads in this pool
        /// </summary>
        public int ThreadCount
        {
            get
            {
                return _threadCount;
            }
        }

        /// <summary>
        /// Gets the number of work items actively running
        /// </summary>
        public int RunningWorkItems
        {
            get
            {
                return _runningWorkItems;
            }
        }

        /// <summary>
        /// Gets the total number of running and queued work items in this pool's work queue
        /// </summary>
        public int TotalWorkItems
        {
            get
            {
                return _totalWorkItems;
            }
        }

        /// <summary>
        /// Enqueues a single stateless action to the thread pool's queue.
        /// </summary>
        /// <param name="workItem"></param>
        public void EnqueueUserWorkItem(Action workItem)
        {
            if (_cancelToken.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(CustomThreadPool), "Cannot queue items to a disposed thread pool");
            }

            EnsureThreadPoolHealth();
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_QueueRate, _dimensions);

            lock (_workItems)
            {
                _workItems.Enqueue(async () => { workItem(); await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false); });
                _queueActivity.Set();
                _totalWorkItems++;
            }
        }

        /// <summary>
        /// Enqueues a single stateless action to the thread pool's queue.
        /// </summary>
        /// <param name="workItem"></param>
        public void EnqueueUserAsyncWorkItem(Func<Task> workItem)
        {
            if (_cancelToken.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(CustomThreadPool), "Cannot queue items to a disposed thread pool");
            }

            EnsureThreadPoolHealth();
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_QueueRate, _dimensions);

            lock (_workItems)
            {
                _workItems.Enqueue(workItem);
                _queueActivity.Set();
                _totalWorkItems++;
            }
        }

        public string GetStatus()
        {
            return string.Format("CustomThreadPool THREADS {0} QUEUED {1} RUNNING {2}", ThreadCount, TotalWorkItems, RunningWorkItems);
        }

        public async Task WaitForCurrentTasksToFinish(CancellationToken cancellizer, IRealTimeProvider realTime)
        {
            long targetFinishedTasks = _tasksCompleted + _totalWorkItems;
            int timeWaited = 0;
            while (_tasksCompleted < targetFinishedTasks &&
                    timeWaited < 2000)
            {
                await realTime.WaitAsync(TimeSpan.FromMilliseconds(5), cancellizer).ConfigureAwait(false);
                timeWaited += 5;
            }
        }

        /// <summary>
        /// If any unhandled exceptions have happened inside of user work items, and hideExceptions is set to false,
        /// this method will re-throw those exceptions on the calling thread. Normally this health check happens
        /// continually as tasks are queued up. This is mainly used for unit test purposes.
        /// </summary>
        public void PropagateExceptionsIfAny()
        {
            EnsureThreadPoolHealth();
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_TotalWorkItems, _dimensions, TotalWorkItems);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_RunningWorkItems, _dimensions, RunningWorkItems);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_UsedCapacity, _dimensions, (100d * (double)TotalWorkItems) / (double)ThreadCount);
        }

        public void InitializeMetrics(IMetricCollector collector)
        {
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

            _metrics.Value.RemoveMetricSource(this);

            _cancelToken.Cancel();
            DateTimeOffset startOfCleanUp = DateTimeOffset.Now;

            // Give the threads 1000ms to stop themselves
            int CLEANUP_TIMEOUT = 1000;
            bool allThreadsDone = false;
            while (!allThreadsDone && DateTimeOffset.Now.Ticks - startOfCleanUp.Ticks < (10000 * CLEANUP_TIMEOUT))
            {
                allThreadsDone = true;
                foreach (WorkerThread thread in _threads)
                {
                    allThreadsDone = allThreadsDone && thread.Finished;
                }

                Thread.Sleep(10);
            }

            if (!allThreadsDone)
            {
                // Forcibly terminate bad threads
                foreach (WorkerThread thread in _threads)
                {
                    if (!thread.Finished)
                    {
                        thread.Cancel();
                    }
                }
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _cancelToken.Dispose();
                _queueActivity.Dispose();
                _threadArrayLock.Dispose();
            }

            _runningWorkItems = 0;
        }


        private void EnsureThreadPoolHealth()
        {
            _threadArrayLock.EnterUpgradeableReadLock();
            try
            {
                for (int c = 0; c < _threadCount; c++)
                {
                    if (_threads[c] == null || _threads[c].Finished)
                    {
                        _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_FatalErrors, _dimensions);
                        _threadArrayLock.EnterWriteLock();
                        try
                        {
                            _logger.Log("Thread " + c + " has died! Rise from your grave...", LogLevel.Wrn);
                            _threads[c] = new WorkerThread(_poolName, c, _logger.Clone("ThreadPool-" + _poolName + ":" + c), DequeueUserWorkItem, WorkItemFinishedCallback, _threadPriority, _exceptions);
                            _threads[c].Start();
                        }
                        finally
                        {
                            _threadArrayLock.ExitWriteLock();
                        }
                    }
                }
            }
            finally
            {
                _threadArrayLock.ExitUpgradeableReadLock();
            }

            // Also rethrow any exceptions that arose on the pool previously
            ExceptionDispatchInfo ex = _exceptions.TryDequeue();
            while (ex != null)
            {
                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_UnhandledExceptions, _dimensions);
                if (!_hideExceptions)
                {
                    ex.Throw();
                }

                ex = _exceptions.TryDequeue();
            }
        }

        /// <summary>
        /// Most threads will spend their time waiting in this loop when there's no work to be done
        /// </summary>
        /// <returns></returns>
        private Func<Task> DequeueUserWorkItem()
        {
            while (!_cancelToken.IsCancellationRequested)
            {
                // Wait for a signal to tell us there's a work item available
                if (_queueActivity.WaitOne(CANCEL_QUERY_INTERVAL_MS))
                {
                    lock (_workItems)
                    {
                        // Is there an item to take? Then take it
                        int count = _workItems.Count;
                        if (count > 0)
                        {
                            _runningWorkItems++;

                            if (count > 1)
                            {
                                // If there will still be an item after we take ours, set the signal before we leave
                                _queueActivity.Set();
                            }

                            return _workItems.Dequeue();
                        }
                    }
                }
            }

            return null;
        }

        private void WorkItemFinishedCallback()
        {
            lock (_workItems)
            {
                _runningWorkItems--;
                _totalWorkItems--;
                _tasksCompleted++;

                if (_workItems.Count > 0)
                {
                    _queueActivity.Set();
                }
            }
        }

        private class WorkerThread
        {
            private readonly Thread _systemThread;
            private readonly GetWorkItem _workItemSource;
            private readonly Action _workItemFinishedCallback;
            private readonly string _poolName;
            private readonly int _threadIndex;
            private readonly ILogger _threadLogger;
            private readonly LockFreeCache<ExceptionDispatchInfo> _exceptionCache;
            private volatile bool _isFinished = false;

            public WorkerThread(string poolName, int index, ILogger logger, GetWorkItem workItemSource, Action workItemFinishedCallback, ThreadPriority threadPriority, LockFreeCache<ExceptionDispatchInfo> exceptionCache)
            {
                _poolName = poolName;
                _threadIndex = index;
                _workItemSource = workItemSource;
                _workItemFinishedCallback = workItemFinishedCallback;
                _threadLogger = logger;
                _exceptionCache = exceptionCache.AssertNonNull(nameof(exceptionCache));

                _systemThread = new Thread(new ThreadStart(Run));
                _systemThread.IsBackground = true;
                _systemThread.Name = _poolName + "-" + _threadIndex;
                _systemThread.Priority = threadPriority;
            }

            public void Start()
            {
                _systemThread.Start();
            }

            public void Cancel()
            {
                if (_systemThread.IsAlive)
                {
#if NETFRAMEWORK
                    _systemThread.Abort();
                    _threadLogger.Log("Thread cancelled", LogLevel.Vrb);
#endif
                }
            }

            private void Run()
            {
                try
                {
                    _threadLogger.Log("Thread started", LogLevel.Vrb);
                    while (!_isFinished)
                    {
                        Func<Task> nextAction = _workItemSource();
                        if (nextAction == null)
                        {
                            // If we get a null signal from this channel it is an explicit signal to shut down this thread
                            _isFinished = true;
                        }
                        else
                        {
                            // Swallow all exceptions. We could somehow pass it on a channel and re-throw it on the main thread,
                            // but I haven't cared enough to implement that yet. TODO
                            Task task;
                            try
                            {
                                // This should be one of the few bridges between sync -> async that we have in our code.
                                // This is allowed because inside the thread pool task we're at the lowest chain of execution,
                                // so there's nothing to await to
                                task = nextAction();
                                task.Await();
                            }
                            catch (AggregateException e)
                            {
                                Exception innerException = e.InnerException;
                                _threadLogger.Log("Unhandled exception in user work item", LogLevel.Err);
                                _threadLogger.Log(innerException, LogLevel.Err);
                                _exceptionCache.TryEnqueue(ExceptionDispatchInfo.Capture(innerException));
                            }
                            catch (Exception e)
                            {
                                _threadLogger.Log("Unhandled exception in user work item", LogLevel.Err);
                                _threadLogger.Log(e, LogLevel.Err);
                                _exceptionCache.TryEnqueue(ExceptionDispatchInfo.Capture(e));
                            }
                            finally
                            {
                                if (_workItemFinishedCallback != null)
                                {
                                    _workItemFinishedCallback();
                                }
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    _threadLogger.Log("Task canceled event raised", LogLevel.Std);
                }
                catch (ThreadAbortException)
                {
                    _threadLogger.Log("Thread aborted event raised", LogLevel.Std);
                }
                catch (Exception e)
                {
                    _threadLogger.Log("Unhandled FATAL exception in thread pool", LogLevel.Err);
                    _threadLogger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _threadLogger.Log("Thread has stopped running", LogLevel.Std);
                    _isFinished = true;
                }
            }

            public bool Finished
            {
                get
                {
                    return !_systemThread.IsAlive || _isFinished;
                }
            }
        }
    }
}
