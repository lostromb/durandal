using Durandal.Common.Collections;
using Durandal.Common.Instrumentation;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// Implements a thread pool using the built-in TaskFactory class
    /// </summary>
    public class TaskThreadPool : IThreadPool
    {
        private readonly TaskFactory _factory;
        private readonly CancellationTokenSource _cancelToken;
        private readonly string _poolName;
        private readonly int _threadCount;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly ConcurrentQueue<ExceptionDispatchInfo> _workItemExceptionQueue;
        private readonly bool _ignoreExceptions;

        /// <summary>
        /// current count of all running + queued tasks
        /// </summary>
        private int _runningAndQueuedTasks;

        /// <summary>
        /// monotonously incrementing count of all tasks that have ever finished
        /// </summary>
        private long _tasksCompleted = 0;

        private int _disposed = 0;

        public TaskThreadPool(
            WeakPointer<IMetricCollector> metrics = default(WeakPointer<IMetricCollector>),
            DimensionSet dimensions = null,
            string poolName = null,
            TaskCreationOptions creationOptions = TaskCreationOptions.DenyChildAttach,
            bool ignoreExceptions = true)
        {
            _poolName = string.IsNullOrEmpty(poolName) ? "null" : poolName;
            _cancelToken = new CancellationTokenSource();
            _workItemExceptionQueue = new ConcurrentQueue<ExceptionDispatchInfo>();
            _factory = new TaskFactory(
                _cancelToken.Token,
                creationOptions,
                creationOptions == TaskCreationOptions.LongRunning ? TaskContinuationOptions.LongRunning : TaskContinuationOptions.None,
                TaskScheduler.Default); // fixme there should really be a task scheduler instance created per-pool rather than shared
            _metrics = metrics.DefaultIfNull(() => NullMetricCollector.Singleton);
            _dimensions = dimensions ?? DimensionSet.Empty;
            _dimensions = _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_ThreadPoolName, _poolName));
            _runningAndQueuedTasks = 0;
            _ignoreExceptions = ignoreExceptions;
            _threadCount = Math.Min(Environment.ProcessorCount, _factory.Scheduler.MaximumConcurrencyLevel);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~TaskThreadPool()
        {
            Dispose(false);
        }
#endif

        public int RunningWorkItems
        {
            get
            {
                 return Math.Min(_threadCount, _runningAndQueuedTasks);
            }
        }

        public int ThreadCount
        {
            get
            {
                return _threadCount;
            }
        }

        public int TotalWorkItems
        {
            get
            {
                return _runningAndQueuedTasks;
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

            _cancelToken.Cancel();
            _cancelToken.Dispose();
        }

        /// <inheritdoc />
        public void EnqueueUserWorkItem(Action workItem)
        {
            RethrowAnyThreadExceptions();
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_QueueRate, _dimensions);
            Task newTask = _factory.StartNew(workItem);
            Interlocked.Increment(ref _runningAndQueuedTasks);

            newTask.ContinueWith((t) =>
            {
                if (t.IsFaulted)
                {
                    CatchThreadException(ExceptionDispatchInfo.Capture(t.Exception));
                }

                // Remove this task from the running set when it finishes
                Interlocked.Decrement(ref _runningAndQueuedTasks);
                Interlocked.Increment(ref _tasksCompleted);
            });
        }

        /// <inheritdoc />
        public void EnqueueUserAsyncWorkItem(Func<Task> workItem)
        {
            RethrowAnyThreadExceptions();
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_QueueRate, _dimensions);
            Task newTask = _factory.StartNew(workItem);
            Interlocked.Increment(ref _runningAndQueuedTasks);

            newTask.ContinueWith((t) =>
            {
                if (t.IsFaulted)
                {
                    CatchThreadException(ExceptionDispatchInfo.Capture(t.Exception));
                }

                // Remove this task from the running set when it finishes
                Interlocked.Decrement(ref _runningAndQueuedTasks);
                Interlocked.Increment(ref _tasksCompleted);
            });
        }

        /// <inheritdoc />
        public string GetStatus()
        {
            return string.Format("TaskThreadPool THREADS {0} QUEUED {1} RUNNING {2}", ThreadCount, TotalWorkItems, RunningWorkItems);
        }

        /// <inheritdoc />
        public async Task WaitForCurrentTasksToFinish(CancellationToken cancellizer, IRealTimeProvider realTime)
        {
            long targetFinishedTasks = _tasksCompleted + _runningAndQueuedTasks;
            int timeWaited = 0;
            while (_tasksCompleted < targetFinishedTasks &&
                    timeWaited < 2000)
            {
                await realTime.WaitAsync(TimeSpan.FromMilliseconds(5), cancellizer).ConfigureAwait(false);
                timeWaited += 5;
            }
        }

        /// <inheritdoc />
        public void ReportMetrics(IMetricCollector reporter)
        {
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_TotalWorkItems, _dimensions, TotalWorkItems);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_RunningWorkItems, _dimensions, RunningWorkItems);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_UsedCapacity, _dimensions, (100d * (double)TotalWorkItems) / (double)ThreadCount);
        }

        /// <inheritdoc />
        public void InitializeMetrics(IMetricCollector collector)
        {
        }

        private void RethrowAnyThreadExceptions()
        {
            ExceptionDispatchInfo exception;
            if (_workItemExceptionQueue.TryDequeue(out exception))
            {
                exception.Throw();
            }
        }

        private void CatchThreadException(ExceptionDispatchInfo exception)
        {
            if (!_ignoreExceptions)
            {
                _workItemExceptionQueue.Enqueue(exception);
            }
        }
    }
}
