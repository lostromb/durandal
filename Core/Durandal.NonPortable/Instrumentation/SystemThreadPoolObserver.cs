using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Class which observes events concerning the system thread pool and emits them as metrics.
    /// </summary>
    public class SystemThreadPoolObserver : IDisposable
    {
        private readonly WeakPointer<IMetricCollector> _collector;
        private readonly DimensionSet _dimensions;
        private readonly IRealTimeProvider _threadLocalTime;
        private readonly CancellationTokenSource _cancelToken;
        private readonly ILogger _logger;
        private readonly TimeSpan CHECK_INTERVAL = TimeSpan.FromMilliseconds(500);
        private int _disposed = 0;

        public SystemThreadPoolObserver(IMetricCollector collector, DimensionSet dimensions, ILogger logger, IRealTimeProvider realTime = null)
        {
            _collector = new WeakPointer<IMetricCollector>(collector);
            _dimensions = dimensions;
            _logger = logger;
            _cancelToken = new CancellationTokenSource();
            _threadLocalTime = (realTime ?? DefaultRealTimeProvider.Singleton).Fork("SystemThreadPoolObserver");
            DurandalTaskExtensions.LongRunningTaskFactory.StartNew(RunThread);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~SystemThreadPoolObserver()
        {
            Dispose(false);
        }
#endif

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
                _cancelToken.Cancel();
                _cancelToken.Dispose();
            }
        }

        private async Task RunThread()
        {
            CancellationToken cancelToken = _cancelToken.Token;
            try
            {
                int workerMin;
                int workerMax;
                int workerAvail;
                int workerRunning;
                int ioMin;
                int ioMax;
                int ioAvail;
                int ioRunning;
                int pastWorkerMin;
                int pastIoMin;
                ThreadPool.GetMinThreads(out pastWorkerMin, out pastIoMin);

                while (!cancelToken.IsCancellationRequested)
                {
                    try
                    {
                        ThreadPool.GetMinThreads(out workerMin, out ioMin);
                        ThreadPool.GetMaxThreads(out workerMax, out ioMax);
                        ThreadPool.GetAvailableThreads(out workerAvail, out ioAvail);
                        workerRunning = workerMax - workerAvail;
                        if (workerRunning > 0)
                        {
                            workerRunning--; // Subtract 1 from the reported "worker running" count, because this code that is currently executing is taking up 1 of those threads
                        }

                        ioRunning = ioMax - ioAvail;
                        double workerCapacityUsed = 100 * (double)workerRunning / (double)Math.Max(1, Environment.ProcessorCount);
                        double ioCapacityUsed = 100 * (double)ioRunning / (double)Math.Max(1, ioMin);

                        if (workerMin < pastWorkerMin)
                        {
                            _collector.Value.ReportInstant(CommonInstrumentation.Key_Counter_WorkerThreadsDestroyed, _dimensions, pastWorkerMin - workerMin);
                        }
                        else if (workerMin > pastWorkerMin)
                        {
                            _collector.Value.ReportInstant(CommonInstrumentation.Key_Counter_WorkerThreadsCreated, _dimensions, workerMin - pastWorkerMin);
                        }
                        if (ioMin < pastIoMin)
                        {
                            _collector.Value.ReportInstant(CommonInstrumentation.Key_Counter_IOThreadsDestroyed, _dimensions, pastIoMin - ioMin);
                        }
                        else if (ioMin > pastIoMin)
                        {
                            _collector.Value.ReportInstant(CommonInstrumentation.Key_Counter_IOThreadsCreated, _dimensions, ioMin - pastIoMin);
                        }

                        _collector.Value.ReportContinuous(CommonInstrumentation.Key_Counter_ReservedWorkerThreads, _dimensions, workerMin);
                        _collector.Value.ReportContinuous(CommonInstrumentation.Key_Counter_ActiveWorkerThreads, _dimensions, workerRunning);
                        _collector.Value.ReportContinuous(CommonInstrumentation.Key_Counter_WorkerThreadCapacityUsed, _dimensions, workerCapacityUsed);
                        _collector.Value.ReportContinuous(CommonInstrumentation.Key_Counter_ReservedIOThreads, _dimensions, ioMin);
                        _collector.Value.ReportContinuous(CommonInstrumentation.Key_Counter_ActiveIOThreads, _dimensions, ioRunning);
                        _collector.Value.ReportContinuous(CommonInstrumentation.Key_Counter_IOThreadCapacityUsed, _dimensions, ioCapacityUsed);

                        pastWorkerMin = workerMin;
                        pastIoMin = ioMin;
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                    finally
                    {
                        await _threadLocalTime.WaitAsync(CHECK_INTERVAL, cancelToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _threadLocalTime.Merge();
            }
        }
    }
}
