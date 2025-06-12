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

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// Thread pool that is backed by a privately owned group of threads, so you are guaranteed to not be shared on any other system pool.
    /// The downside of this is that managing the threads manually incurs a slight bit of overhead compared to other pools.
    /// </summary>
    public class CustomThreadPool : SynchronizationContext, IThreadPool
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
        private readonly CancellationTokenSource _cancelizer;
        private readonly ThreadPriority _threadPriority;
        private readonly TaskScheduler _threadPoolTaskScheduler;
        private int _nextThreadIndex = -1;

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

        public CustomThreadPool(
            ILogger logger,
            IMetricCollector metrics,
            DimensionSet dimensions,
            ThreadPriority threadPriority = ThreadPriority.Normal,
            string poolName = "ThreadPool",
            int? threadCount = null,
            bool hideExceptions = true)
        {
            
            if (!threadCount.HasValue)
            {
                _threadCount = Math.Max(1, Environment.ProcessorCount);
            }
            else
            {
                _threadCount = threadCount.Value;
                if (_threadCount < 1)
                {
                    throw new ArgumentOutOfRangeException("Thread count must be positive");
                }
            }

            _threadPriority = threadPriority;
            _poolName = poolName;
            _logger = logger;
            _metrics = new WeakPointer<IMetricCollector>(metrics ?? NullMetricCollector.Singleton);
            _dimensions = dimensions ?? DimensionSet.Empty;
            _dimensions = _dimensions.Combine(new MetricDimension("Pool", _poolName));
            _threads = new WorkerThread[_threadCount];
            _cancelizer = new CancellationTokenSource();

            // Create the task scheduler that will just delegate to this pool for all work items
            SynchronizationContext oldSynchContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(this);
            _threadPoolTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(oldSynchContext);

            // Start up the threads
            for (int c = 0; c < _threadCount; c++)
            {
                _threads[c] = new WorkerThread(
                    new WeakPointer<CustomThreadPool>(this),
                    _poolName,
                    c,
                    _logger.Clone("ThreadPool-" + _poolName + ":" + c),
                    _threadPriority,
                    _metrics,
                    _dimensions,
                    _cancelizer.Token,
                    _threadPoolTaskScheduler,
                    hideExceptions);
                _threads[c].Start();
            }

            _metrics.Value.AddMetricSource(this);
            Durandal.Common.Utils.DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
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
            if (_cancelizer.IsCancellationRequested)
            {
                throw new ObjectDisposedException("Cannot queue items to a disposed thread pool");
            }

            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_QueueRate, _dimensions);

            uint targetThreadIdx = ((uint)Interlocked.Increment(ref _nextThreadIndex)) % (uint)_threadCount;
            //_logger.Log("Queuing synchronous work to thread " + targetThreadIdx, LogLevel.Vrb);
            _threads[targetThreadIdx].EnqueueUserWorkItem(workItem, WorkItemCallback);
            Interlocked.Increment(ref _totalWorkItems);
        }

        /// <summary>
        /// Enqueues a single stateless action to the thread pool's queue.
        /// </summary>
        /// <param name="workItem"></param>
        public void EnqueueUserAsyncWorkItem(Func<Task> workItem)
        {
            if (_cancelizer.IsCancellationRequested)
            {
                throw new ObjectDisposedException("Cannot queue items to a disposed thread pool");
            }

            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_QueueRate, _dimensions);
            uint targetThreadIdx = ((uint)Interlocked.Increment(ref _nextThreadIndex)) % (uint)_threadCount;
            //_logger.Log("Queuing async work to thread " + targetThreadIdx, LogLevel.Vrb);
            _threads[targetThreadIdx].EnqueueUserWorkItem(workItem, WorkItemCallback);
            Interlocked.Increment(ref _totalWorkItems);
        }

        private void WorkItemCallback()
        {
            Interlocked.Decrement(ref _totalWorkItems);
            Interlocked.Increment(ref _tasksCompleted);
        }

        public string GetStatus()
        {
            return Durandal.Common.Utils.StringBuilderPool.Format("CustomThreadPool THREADS {0} QUEUED {1} RUNNING {2}", ThreadCount, TotalWorkItems, RunningWorkItems);
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
            foreach (WorkerThread thread in _threads)
            {
                thread.EnsureThreadHealth();
            }
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_TotalWorkItems, TotalWorkItems, _dimensions);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_RunningWorkItems, RunningWorkItems, _dimensions);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_UsedCapacity, (100d * (double)TotalWorkItems) / (double)ThreadCount, _dimensions);
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
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
            {
                return;
            }

            _metrics.Value.RemoveMetricSource(this);

            _cancelizer.Cancel();
            DateTimeOffset startOfCleanUp = DateTimeOffset.Now;

            // Give the threads 1000ms to stop themselves
            int CLEANUP_TIMEOUT = 1000;
            bool allThreadsDone = false;
            while (!allThreadsDone && DateTimeOffset.Now.Ticks - startOfCleanUp.Ticks < (10000 * CLEANUP_TIMEOUT))
            {
                allThreadsDone = true;
                foreach (WorkerThread thread in _threads)
                {
                    allThreadsDone = allThreadsDone && thread.IsFinished;
                }

                Thread.Sleep(10);
            }

            if (!allThreadsDone)
            {
                // Forcibly terminate bad threads
                foreach (WorkerThread thread in _threads)
                {
                    if (!thread.IsFinished)
                    {
                        thread.KillThread();
                    }
                }
            }

            Durandal.Common.Utils.DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _cancelizer.Dispose();
            }

            _runningWorkItems = 0;
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            uint targetThreadIdx = ((uint)Interlocked.Increment(ref _nextThreadIndex)) % (uint)_threadCount;
            _threads[targetThreadIdx].EnqueueUserWorkItem(d, state);
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            uint targetThreadIdx = ((uint)Interlocked.Increment(ref _nextThreadIndex)) % (uint)_threadCount;
            _threads[targetThreadIdx].EnqueueUserWorkItem(d, state);
        }

        private class WorkerThread
        {
            private readonly WeakPointer<CustomThreadPool> _owningPool;
            private readonly AutoResetEvent _queueActivity;
            private readonly ConcurrentQueue<InternalWorkItem> _workItems;
            private readonly string _poolName;
            private readonly int _threadIndex;
            private readonly WeakPointer<IMetricCollector> _metrics;
            private readonly DimensionSet _dimensions;
            private readonly ILogger _threadLogger;
            private readonly CancellationToken _threadAbort;
            private readonly bool _hideExceptions;
            private readonly LockFreeCache<ExceptionDispatchInfo> _exceptionQueue;
            private readonly ThreadPriority _threadPriority;
            private readonly TaskScheduler _threadPoolTaskScheduler;
            private bool _threadFinished;
            private Thread _systemThread;

            public WorkerThread(
                WeakPointer<CustomThreadPool> owningPool,
                string poolName,
                int index,
                ILogger logger,
                ThreadPriority threadPriority,
                WeakPointer<IMetricCollector> metrics,
                DimensionSet dimensions,
                CancellationToken threadAbort,
                TaskScheduler threadPoolTaskScheduler,
                bool hideExceptions)
            {
                _owningPool = owningPool.AssertNonNull(nameof(owningPool));
                _poolName = poolName.AssertNonNull(nameof(metrics));
                _threadIndex = index;
                _queueActivity = new AutoResetEvent(false);
                _workItems = new ConcurrentQueue<InternalWorkItem>();
                _threadLogger = logger.AssertNonNull(nameof(metrics));
                _threadPriority = threadPriority;
                _metrics = metrics.AssertNonNull(nameof(metrics));
                _dimensions = dimensions.AssertNonNull(nameof(metrics));
                _threadAbort = threadAbort;
                _hideExceptions = hideExceptions;
                _exceptionQueue = new LockFreeCache<ExceptionDispatchInfo>(4);
                _threadFinished = false;
                _threadPoolTaskScheduler = threadPoolTaskScheduler.AssertNonNull(nameof(threadPoolTaskScheduler));

                _systemThread = new Thread(new ThreadStart(RunThreadLoop));
                _systemThread.IsBackground = true;
                _systemThread.Name = _poolName + "-" + _threadIndex;
                _systemThread.Priority = _threadPriority;
            }

            public void Start()
            {
                _systemThread.Start();
            }

            public void KillThread()
            {
                if (_systemThread.IsAlive)
                {
                    _systemThread.Abort();
                    _threadFinished = true;
                    _threadLogger.Log("Thread aborted", LogLevel.Vrb);
                }
            }

            public void EnqueueUserWorkItem(Action work, Action workFinished)
            {
                EnsureThreadHealth();
                _workItems.Enqueue(new InternalWorkItem(work, workFinished));
                _queueActivity.Set();
            }

            public void EnqueueUserWorkItem(Func<Task> work, Action workFinished)
            {
                EnsureThreadHealth();
                _workItems.Enqueue(new InternalWorkItem(work, workFinished));
                _queueActivity.Set();
            }

            public void EnqueueUserWorkItem(SendOrPostCallback work, object state)
            {
                EnsureThreadHealth();
                _workItems.Enqueue(new InternalWorkItem(work, state));
                _queueActivity.Set();
            }

            public bool IsFinished
            {
                get
                {
                    return _threadFinished;
                }
            }

            public void EnsureThreadHealth()
            {
                //if (_systemThread == null || !_systemThread.IsAlive)
                //{
                //    _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_FatalErrors, _dimensions);
                //    _threadLogger.Log("Worker thread has died! Rise from your grave...", LogLevel.Wrn);
                //    _systemThread = new Thread(new ThreadStart(Run));
                //    _systemThread.IsBackground = true;
                //    _systemThread.Name = _poolName + "-" + _threadIndex;
                //    _systemThread.Priority = _threadPriority;
                //}
                //else
                {
                    // Also rethrow any exceptions that arose on the pool previously
                    ExceptionDispatchInfo ex = _exceptionQueue.TryDequeue();
                    while (ex != null)
                    {
                        _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_UnhandledExceptions, _dimensions);
                        if (!_hideExceptions)
                        {
                            ex.Throw();
                        }

                        ex = _exceptionQueue.TryDequeue();
                    }
                }
            }

            private void RunThreadLoop()
            {
                SynchronizationContext.SetSynchronizationContext(_owningPool.Value);
                try
                {
                    _threadLogger.Log("Thread started with managed ID " + Thread.CurrentThread.ManagedThreadId, LogLevel.Vrb);
                    while (!_threadAbort.IsCancellationRequested)
                    {
                        InternalWorkItem nextAction = DequeueUserWorkItem();
                        if (!_threadAbort.IsCancellationRequested)
                        {
                            try
                            {
                                Interlocked.Increment(ref _owningPool.Value._runningWorkItems);
                                if (nextAction.SyncWork != null)
                                {
                                    //_threadLogger.Log("Processing Action on thread " + Thread.CurrentThread.ManagedThreadId, LogLevel.Vrb);
                                    nextAction.SyncWork();
                                }
                                else if (nextAction.SendOrPost != null)
                                {
                                    //_threadLogger.Log("Processing Action on thread " + Thread.CurrentThread.ManagedThreadId, LogLevel.Vrb);
                                    nextAction.SendOrPost(nextAction.ThreadState);
                                }
                                else if (nextAction.TaskProducer != null)
                                {
                                    //_threadLogger.Log("Processing Func<Task> on thread " + Thread.CurrentThread.ManagedThreadId, LogLevel.Vrb);
                                    Task runningTask = nextAction.TaskProducer();
                                    if (nextAction.WorkFinished != null)
                                    {
                                        runningTask.ContinueWith((t) => nextAction.WorkFinished, _threadPoolTaskScheduler);
                                    }

                                    runningTask.GetHashCode();

                                    //Task<Task> garbage = _taskFactory.StartNew(nextAction.TaskProducer);
                                    //if (nextAction.WorkFinished != null)
                                    //{
                                    //    garbage.Unwrap().ContinueWith((t) => nextAction.WorkFinished);
                                    //}
                                }
                                //else
                                //{
                                //    //_threadLogger.Log("Processing Task on thread " + Thread.CurrentThread.ManagedThreadId, LogLevel.Vrb);
                                //    base.TryExecuteTask(nextAction.AsyncTask);
                                //}
                            }
                            catch (AggregateException e)
                            {
                                Exception innerException = e.InnerException;
                                _threadLogger.Log("Unhandled exception in user work item", LogLevel.Err);
                                _threadLogger.Log(innerException, LogLevel.Err);
                                _exceptionQueue.TryEnqueue(ExceptionDispatchInfo.Capture(innerException));
                            }
                            catch (Exception e)
                            {
                                _threadLogger.Log("Unhandled exception in user work item", LogLevel.Err);
                                _threadLogger.Log(e, LogLevel.Err);
                                _exceptionQueue.TryEnqueue(ExceptionDispatchInfo.Capture(e));
                            }
                            finally
                            {
                                if (nextAction.WorkFinished != null)
                                {
                                    nextAction.WorkFinished();
                                }

                                Interlocked.Decrement(ref _owningPool.Value._runningWorkItems);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
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
                }
            }

            /// <summary>
            /// Most threads will spend their time waiting in this loop when there's no work to be done
            /// </summary>
            /// <returns></returns>
            private InternalWorkItem DequeueUserWorkItem()
            {
                InternalWorkItem returnVal;

                // First, just try and dequeue
                if (_workItems.TryDequeue(out returnVal))
                {
                    return returnVal;
                }

                while (!_threadAbort.IsCancellationRequested)
                {
                    // Wait for a signal to tell us there's a work item available
                    if (_queueActivity.WaitOne(CANCEL_QUERY_INTERVAL_MS))
                    {
                        // Is there an item to take? Then take it
                        if (_workItems.TryDequeue(out returnVal))
                        {
                            return returnVal;
                        }
                    }
                }

                return returnVal;
            }

            private struct InternalWorkItem
            {
                public Action SyncWork;
                public Action WorkFinished;
                public Task AsyncTask;
                public Func<Task> TaskProducer;
                public SendOrPostCallback SendOrPost;
                public object ThreadState;

                public InternalWorkItem(Action work, Action workFinished)
                {
                    SyncWork = work;
                    WorkFinished = workFinished;
                    AsyncTask = null;
                    TaskProducer = null;
                    SendOrPost = null;
                    ThreadState = null;
                }

                public InternalWorkItem(Task work)
                {
                    SyncWork = null;
                    WorkFinished = null;
                    AsyncTask = work;
                    TaskProducer = null;
                    SendOrPost = null;
                    ThreadState = null;
                }

                public InternalWorkItem(SendOrPostCallback work, object state)
                {
                    SyncWork = null;
                    WorkFinished = null;
                    AsyncTask = null;
                    TaskProducer = null;
                    SendOrPost = work;
                    ThreadState = state;
                }

                public InternalWorkItem(Func<Task> work, Action workFinished)
                {
                    SyncWork = null;
                    WorkFinished = workFinished;
                    AsyncTask = null;
                    TaskProducer = work;
                    SendOrPost = null;
                    ThreadState = null;
                }
            }
        }
    }
}
