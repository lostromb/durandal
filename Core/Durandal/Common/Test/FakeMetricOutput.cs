using Durandal.Common.Instrumentation;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Test
{
    /// <summary>
    /// IMetricOutput instance which caches all metrics in-memory so that test code can query later which counters have been seen and reported
    /// </summary>
    public class FakeMetricOutput : IMetricOutput
    {
        private readonly List<IDictionary<CounterInstance, double?>> _metricHistory = new List<IDictionary<CounterInstance, double?>>();
        private int _disposed = 0;

        public FakeMetricOutput()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FakeMetricOutput()
        {
            Dispose(false);
        }
#endif

        public Task OutputAggregateMetrics(IReadOnlyDictionary<CounterInstance, double?> currentMetrics)
        {
            Dictionary<CounterInstance, double?> copied = new Dictionary<CounterInstance, double?>();
            foreach (var kvp in currentMetrics)
            {
                copied.Add(kvp.Key, kvp.Value);
            }

            lock (_metricHistory)
            {
                _metricHistory.Add(copied);
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        public void Reset()
        {
            lock (_metricHistory)
            {
                _metricHistory.Clear();
            }
        }

        /// <summary>
        /// Asserts that a given metric was seen before with a non-zero value
        /// </summary>
        /// <param name="counterName">The simple name of the counter to check</param>
        /// <returns></returns>
        public bool MetricHasValue(string counterName)
        {
            lock (_metricHistory)
            {
                foreach (IDictionary<CounterInstance, double?> dict in _metricHistory)
                {
                    foreach (var kvp in dict)
                    {
                        if (string.Equals(counterName, kvp.Key.CounterName))
                        {
                            if (kvp.Value.HasValue && kvp.Value.Value > 0)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Retrieves the latest recorded value for a specific metric with at least the specified dimensions.
        /// </summary>
        /// <param name="counterName">The name of the counter to look for.</param>
        /// <param name="requiredDimensions">The minimum required dimensions to match when searching for metrics.</param>
        /// <returns></returns>
        public double? GetLatestMetricValue(string counterName, DimensionSet requiredDimensions)
        {
            double? latestValue = null;
            lock (_metricHistory)
            {
                foreach (IDictionary<CounterInstance, double?> dict in _metricHistory)
                {
                    foreach (var kvp in dict)
                    {
                        if (string.Equals(counterName, kvp.Key.CounterName) &&
                            requiredDimensions.IsSubsetOf(kvp.Key.Dimensions) &&
                            kvp.Value.HasValue)
                        {
                            latestValue = kvp.Value;
                            break;
                        }
                    }
                }
            }

            return latestValue;
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
