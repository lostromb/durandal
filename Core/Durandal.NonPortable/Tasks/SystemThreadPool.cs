using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Instrumentation;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// Implements a thread pool using the system-provided ThreadPool class. All threads used here are shared between all instances of SystemThreadPool,
    /// which may cause unintended behavior.
    /// </summary>
    public class SystemThreadPool : IThreadPool
    {
        private readonly int _numThreads;
        private readonly ILogger _logger;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private int _disposed = 0;

        /// <summary>
        /// current count of all running + queued tasks
        /// </summary>
        private int _queuedAndRunningTasks = 0;

        /// <summary>
        /// current count of running tasks
        /// </summary>
        private int _runningTasks = 0;

        /// <summary>
        /// monotonously incrementing count of all tasks that have ever finished
        /// </summary>
        private long _tasksCompleted = 0;

        public SystemThreadPool(ILogger logger, IMetricCollector metrics, DimensionSet dimensions)
        {
            _numThreads = Environment.ProcessorCount;
            _logger = logger;
            _metrics = new WeakPointer<IMetricCollector>(metrics);
            _dimensions = dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_ThreadPoolName, "SystemPool"));
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~SystemThreadPool()
        {
            Dispose(false);
        }
#endif

        public int RunningWorkItems
        {
            get
            {
                return _runningTasks;
            }
        }

        public int ThreadCount
        {
            get
            {
                return _numThreads;
            }
        }

        public int TotalWorkItems
        {
            get
            {
                return _queuedAndRunningTasks;
            }
        }

        public void EnqueueUserWorkItem(Action workItem)
        {
            Interlocked.Increment(ref _queuedAndRunningTasks);
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_QueueRate, _dimensions);
            ThreadPool.QueueUserWorkItem((s) =>
                {
                    Interlocked.Increment(ref _runningTasks);
                    try
                    {
                        workItem();
                    }
                    catch (Exception e)
                    {
                        _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_UnhandledExceptions, _dimensions);
                        _logger.Log(e, LogLevel.Err);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _runningTasks);
                        Interlocked.Decrement(ref _queuedAndRunningTasks);
                        Interlocked.Increment(ref _tasksCompleted);
                    }
                });
        }

        public void EnqueueUserAsyncWorkItem(Func<Task> workItem)
        {
            Interlocked.Increment(ref _queuedAndRunningTasks);
            _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_QueueRate, _dimensions);
            ThreadPool.QueueUserWorkItem(async (s) =>
                {
                    Interlocked.Increment(ref _runningTasks);
                    try
                    {
                        await workItem().ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_ThreadPool_UnhandledExceptions, _dimensions);
                        _logger.Log(e, LogLevel.Err);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _runningTasks);
                        Interlocked.Decrement(ref _queuedAndRunningTasks);
                        Interlocked.Increment(ref _tasksCompleted);
                    }
                });
        }

        public string GetStatus()
        {
            return string.Format("SystemThreadPool THREADS {0} QUEUED {1} RUNNING {2}", _numThreads, _queuedAndRunningTasks, _runningTasks);
        }
        
        public async Task WaitForCurrentTasksToFinish(CancellationToken cancellizer, IRealTimeProvider realTime)
        {
            long targetFinishedTasks = _tasksCompleted + _queuedAndRunningTasks;
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

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
            }
        }
    }
}
