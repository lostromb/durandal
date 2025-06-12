using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using Durandal.Common.Time;
using Durandal.Common.Time.Scheduling;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Instrumentation
{
    public class ConsoleMetricOutput : IMetricOutput
    {
        private int _disposed = 0;

        public ConsoleMetricOutput()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ConsoleMetricOutput()
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
            }
        }

        public Task OutputAggregateMetrics(IReadOnlyDictionary<CounterInstance, double?> continuousMetrics)
        {
            int longestMetricName = 0;
            foreach (CounterInstance metricName in continuousMetrics.Keys)
            {
                longestMetricName = Math.Max(longestMetricName, metricName.ToString().Length);
            }

            Console.WriteLine("Reported metrics " + HighPrecisionTimer.GetCurrentUTCTime().ToString());
            foreach (CounterInstance metricName in continuousMetrics.Keys.OrderBy((c) => c))
            {
                double? metricValue = continuousMetrics[metricName];
                Console.WriteLine("{0," + longestMetricName + "} \t{1:F3}", metricName.ToString(), metricValue.GetValueOrDefault(0));
            }

            return DurandalTaskExtensions.NoOpTask;
        }
    }
}
