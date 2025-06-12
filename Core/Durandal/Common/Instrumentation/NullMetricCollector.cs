using Durandal.Common.ServiceMgmt;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    public sealed class NullMetricCollector : IMetricCollector
    {
        public static readonly NullMetricCollector Singleton = new NullMetricCollector();
        public static readonly WeakPointer<IMetricCollector> WeakSingleton = new WeakPointer<IMetricCollector>(Singleton);
        private static readonly Dictionary<CounterInstance, double?> NULL_DICT = new Dictionary<CounterInstance, double?>();

        private NullMetricCollector() { }
        
        public void ReportInstant(string counter, DimensionSet dimensions, int increment = 1) { }

        public void ReportContinuous(string counter, DimensionSet dimensions, double value) { }

        public void ReportPercentile(string counter, DimensionSet dimensions, double value) { }

        public void AddMetricSource(IMetricSource reportable) { }

        public void RemoveMetricSource(IMetricSource reportable) { }

        public IReadOnlyDictionary<CounterInstance, double?> GetCurrentMetrics()
        {
            return NULL_DICT;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "Static singleton class is never disposed")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1063:Implement IDisposable Correctly", Justification = "Static singleton class is never disposed")]
        public void Dispose() { }

        public IReadOnlyDictionary<CounterInstance, double?> GetCurrentMetrics(string metricName)
        {
            return NULL_DICT;
        }

        public double? GetCurrentMetric(string metricName)
        {
            return null;
        }
    }
}
