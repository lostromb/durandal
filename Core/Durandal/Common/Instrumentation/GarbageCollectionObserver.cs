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
    /// Class which observes events concerning the garbage collector and emits them as metrics.
    /// </summary>
    public class GarbageCollectionObserver : IDisposable
    {
        private readonly WeakPointer<IMetricCollector> _collector;
        private readonly DimensionSet _dimensions;
        private readonly IRealTimeProvider _threadLocalTime;
        private readonly CancellationTokenSource _cancelToken;
        private readonly TimeSpan CHECK_INTERVAL = TimeSpan.FromMilliseconds(500);
        private int _disposed = 0;

        public GarbageCollectionObserver(IMetricCollector collector, DimensionSet dimensions, IRealTimeProvider realTime = null)
        {
            _collector = new WeakPointer<IMetricCollector>(collector);
            _dimensions = dimensions;
            _cancelToken = new CancellationTokenSource();
            _threadLocalTime = (realTime ?? DefaultRealTimeProvider.Singleton).Fork("GarbageCollectorObserver");
            DurandalTaskExtensions.LongRunningTaskFactory.StartNew(RunThread);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~GarbageCollectionObserver()
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
                int[] genCounter = new int[GC.MaxGeneration + 1];
                while (!cancelToken.IsCancellationRequested)
                {
                    for (int c = 0; c <= GC.MaxGeneration; c++)
                    {
                        int curCount = GC.CollectionCount(c);
                        if (genCounter[c] != curCount)
                        {
                            genCounter[c] = curCount;
                            _collector.Value.ReportInstant("GC Gen " + c + " Collections / sec", _dimensions);
                        }
                    }

                    await _threadLocalTime.WaitAsync(CHECK_INTERVAL, cancelToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _threadLocalTime.Merge();
            }
        }
    }
}
