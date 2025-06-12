namespace Durandal.Common.Tasks
{
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements a thread pool which limits the number of backlogged work items to fixed number, to prevent work items from reaching a critical mass and halting a machine.
    /// This limiting is done either by ignoring work items once the load reaches maximum (for non-essential program functions such as logging) or by blocking the caller until
    /// a thread is available, essentially performing throttling of the whole process.
    /// </summary>
    public class FixedCapacityThreadPool : IThreadPool
    {
        /// <summary>
        /// Upper limit to the capacity of a fixed capacity thread pool
        /// </summary>
        private const int MAX_MAX_CAPACITY = 10000;
        
        private readonly WeakPointer<IThreadPool> _internalPool;
        private readonly string _poolName;
        private readonly ThreadPoolOverschedulingBehavior _overschedulingBehavior;
        private readonly TimeSpan _overschedulingParam;
        private readonly ILogger _logger;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private int _maxCapacity;

        /// <summary>
        /// current count of running + queued work items that have been delegated to the internal pool
        /// </summary>
        private int _ownedWorkItems;
        
        /// <summary>
        /// monotonously incrementing count of all tasks that have ever finished
        /// </summary>
        private long _tasksCompleted = 0;

        private int _disposed = 0;

        /// <summary>
        /// Creates a thread pool that wraps around a base implementation, but specifies a fixed queued work item capacity
        /// </summary>
        /// <param name="internalPool">The pool to dispatch the actual tasks to</param>
        /// <param name="logger"></param>
        /// <param name="metrics"></param>
        /// <param name="dimensions"></param>
        /// <param name="poolName">Name to use for debugging and logging</param>
        /// <param name="maxCapacity">The maximum pool capacity, measured in total (running + queued) work items. If null, this defaults to the number of threads in the thread pool.</param>
        /// <param name="overschedulingBehavior">The desired behavior of the pool once it has reached capacity. You can choose to ignore incoming work items or throttle them in some way</param>
        /// <param name="overschedulingParam">A parameter to be passed to the oversheduling logic, whose function is dependent on that logic (but it's generally a timeout value of some kind)</param>
        public FixedCapacityThreadPool(
            IThreadPool internalPool,
            ILogger logger,
            IMetricCollector metrics,
            DimensionSet dimensions,
            string poolName = "FixedThreadPool",
            int? maxCapacity = null,
            ThreadPoolOverschedulingBehavior overschedulingBehavior = ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable,
            TimeSpan overschedulingParam = default(TimeSpan))
        {
            if (maxCapacity.HasValue && maxCapacity.Value < 1)
            {
                throw new ArgumentException("Maximum capacity must be greater than 0");
            }

            _internalPool = new WeakPointer<IThreadPool>(internalPool);
            _poolName = poolName;
            _maxCapacity = Math.Min(MAX_MAX_CAPACITY, maxCapacity.GetValueOrDefault(_internalPool.Value.ThreadCount));
            _overschedulingBehavior = overschedulingBehavior;
            _overschedulingParam = overschedulingParam;
            _logger = logger;
            _metrics = new WeakPointer<IMetricCollector>(metrics ?? NullMetricCollector.Singleton);
            _dimensions = dimensions ?? DimensionSet.Empty;
            _dimensions = _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_ThreadPoolName, _poolName));
            _metrics.Value.AddMetricSource(this);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FixedCapacityThreadPool()
        {
            Dispose(false);
        }
#endif

        public int RunningWorkItems
        {
            get
            {
                return Math.Min(_ownedWorkItems, _internalPool.Value.ThreadCount);
            }
        }

        /// <summary>
        /// The maximum number of work items that can run concurrently on this <see cref="FixedCapacityThreadPool"/>.
        /// You can set this value dynamically to adjust how many threads should be allowed to run
        /// </summary>
        public int ThreadCount
        {
            get
            {
                return _maxCapacity;
            }
            set
            {
                _maxCapacity = value;
            }
        }

        public int TotalWorkItems
        {
            get
            {
                return _ownedWorkItems;
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
                _metrics.Value.RemoveMetricSource(this);
            }
        }

        public void EnqueueUserAsyncWorkItem(Func<Task> workItem)
        {
            int timeWaited = 0;
            if (_overschedulingBehavior == ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable)
            {
                DateTimeOffset waitBegin = HighPrecisionTimer.GetCurrentUTCTime();
                while (_ownedWorkItems >= _maxCapacity &&
                    (_overschedulingParam == default(TimeSpan) || HighPrecisionTimer.GetCurrentUTCTime() - waitBegin < _overschedulingParam))
                {
                    DurandalTaskExtensions.Block(1, CancellationToken.None);
                    timeWaited += 1;
                }

                EnqueueUserAsyncWorkItemInternal(workItem);
            }
            else if (_overschedulingBehavior == ThreadPoolOverschedulingBehavior.ShedExcessWorkItems)
            {
                if (_ownedWorkItems < _maxCapacity)
                {
                    EnqueueUserAsyncWorkItemInternal(workItem);
                }
                else
                {
                    _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_ShedItems, _dimensions);
                    _logger.Log("Shedding work items because thread pool is oversaturated", LogLevel.Wrn);
                }
            }
            else if (_overschedulingBehavior == ThreadPoolOverschedulingBehavior.LinearThrottle)
            {
                int oversaturationAmount = _ownedWorkItems - _maxCapacity;
                if (oversaturationAmount > 0)
                {
                    int timeToWait = (int)_overschedulingParam.TotalMilliseconds * oversaturationAmount;
                    DurandalTaskExtensions.Block(timeToWait, CancellationToken.None);
                    timeWaited += timeToWait;
                }

                EnqueueUserAsyncWorkItemInternal(workItem);
            }
            else if (_overschedulingBehavior == ThreadPoolOverschedulingBehavior.QuadraticThrottle)
            {
                int oversaturationAmount = Math.Min(64, _ownedWorkItems - _maxCapacity);
                if (oversaturationAmount > 0)
                {
                    int timeToWait = (int)_overschedulingParam.TotalMilliseconds * oversaturationAmount * oversaturationAmount;
                    DurandalTaskExtensions.Block(timeToWait, CancellationToken.None);
                    timeWaited += timeToWait;
                }

                EnqueueUserAsyncWorkItemInternal(workItem);
            }
            else
            {
                throw new NotImplementedException("Unknown overscheduling behavior " + _overschedulingBehavior.ToString());
            }

            if (timeWaited > 0)
            {
                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_CapacityThrottles, _dimensions);
                _logger.Log("Throttled new work items by " + timeWaited + "ms because thread pool is saturated", LogLevel.Wrn);
            }
        }

        public void EnqueueUserWorkItem(Action workItem)
        {
            int timeWaited = 0;
            if (_overschedulingBehavior == ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable)
            {
                DateTimeOffset waitBegin = HighPrecisionTimer.GetCurrentUTCTime();
                while (_ownedWorkItems >= _maxCapacity &&
                    (_overschedulingParam == default(TimeSpan) || HighPrecisionTimer.GetCurrentUTCTime() - waitBegin < _overschedulingParam))
                {
                    DurandalTaskExtensions.Block(1, CancellationToken.None);
                    timeWaited += 1;
                }

                EnqueueUserWorkItemInternal(workItem);
            }
            else if (_overschedulingBehavior == ThreadPoolOverschedulingBehavior.ShedExcessWorkItems)
            {
                // Only queue the work item if we are below max capacity, otherwise ignore it
                if (_ownedWorkItems < _maxCapacity)
                {
                    EnqueueUserWorkItemInternal(workItem);
                }
                else
                {
                    _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_ShedItems, _dimensions);
                    _logger.Log("Shedding work items because thread pool is oversaturated", LogLevel.Wrn);
                }
            }
            else if (_overschedulingBehavior == ThreadPoolOverschedulingBehavior.LinearThrottle)
            {
                int oversaturationAmount = _maxCapacity - _ownedWorkItems;
                if (oversaturationAmount > 0)
                {
                    int timeToWait = (int)_overschedulingParam.TotalMilliseconds * oversaturationAmount;
                    DurandalTaskExtensions.Block(timeToWait, CancellationToken.None);
                    timeWaited += timeToWait;
                }

                EnqueueUserWorkItemInternal(workItem);
            }
            else if (_overschedulingBehavior == ThreadPoolOverschedulingBehavior.QuadraticThrottle)
            {
                int oversaturationAmount = Math.Min(64, _maxCapacity - _ownedWorkItems);
                if (oversaturationAmount > 0)
                {
                    int timeToWait = (int)_overschedulingParam.TotalMilliseconds * oversaturationAmount * oversaturationAmount;
                    DurandalTaskExtensions.Block(timeToWait, CancellationToken.None);
                    timeWaited += timeToWait;
                }

                EnqueueUserWorkItemInternal(workItem);
            }
            else
            {
                throw new NotImplementedException("Unknown overscheduling behavior " + _overschedulingBehavior.ToString());
            }
            
            if (timeWaited > 0)
            {
                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_CapacityThrottles, _dimensions);
                _logger.Log("Throttled new work items by " + timeWaited + "ms because thread pool is saturated", LogLevel.Wrn);
            }
        }

        public string GetStatus()
        {
            return string.Format("FixedCapacityThreadPool THREADS {0} QUEUED {1} RUNNING {2} (internal pool: {3})", ThreadCount, TotalWorkItems, RunningWorkItems, _internalPool.Value.GetStatus());
        }
        
        public async Task WaitForCurrentTasksToFinish(CancellationToken cancellizer, IRealTimeProvider realTime)
        {
            long targetFinishedTasks = _tasksCompleted + _ownedWorkItems;
            int timeWaited = 0;
            while (_tasksCompleted < targetFinishedTasks &&
                    timeWaited < 2000)
            {
                await realTime.WaitAsync(TimeSpan.FromMilliseconds(5), cancellizer).ConfigureAwait(false);
                timeWaited += 5;
            }
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_TotalWorkItems, _dimensions, _ownedWorkItems);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_RunningWorkItems, _dimensions, _ownedWorkItems);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ThreadPool_UsedCapacity, _dimensions, (100d * (double)_ownedWorkItems) / (double)_maxCapacity);
        }

        public void InitializeMetrics(IMetricCollector collector)
        {
            _internalPool.Value.InitializeMetrics(collector);
        }

        private void EnqueueUserAsyncWorkItemInternal(Func<Task> workItem)
        {
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_QueueRate, _dimensions);
            Interlocked.Increment(ref _ownedWorkItems);
            _internalPool.Value.EnqueueUserAsyncWorkItem(async () =>
            {
                try
                {
                    await workItem().ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref _ownedWorkItems);
                    Interlocked.Increment(ref _tasksCompleted);
                }
            });
        }

        private void EnqueueUserWorkItemInternal(Action workItem)
        {
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_QueueRate, _dimensions);
            Interlocked.Increment(ref _ownedWorkItems);
            _internalPool.Value.EnqueueUserWorkItem(() =>
            {
                try
                {
                    workItem();
                }
                finally
                {
                    Interlocked.Decrement(ref _ownedWorkItems);
                    Interlocked.Increment(ref _tasksCompleted);
                }
            });
        }
    }
}
