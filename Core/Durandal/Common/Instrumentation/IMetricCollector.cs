using Durandal.Common.Net.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    public interface IMetricCollector : IDisposable
    {
        /// <summary>
        /// Reports that a discrete event has occurred, such as an exception being raised or an external call being made.
        /// The metrics reporter will automatically aggregate instantaneous events over time to convert this metric to a rate of events per second
        /// </summary>
        /// <param name="counter">The name of the counter to increment</param>
        /// <param name="dimensions">The dimensions (string properties) to apply to this metric</param>
        /// <param name="increment">The increment value. Will be 1 in most cases, but any number (even negative) is allowed</param>
        void ReportInstant(string counter, DimensionSet dimensions, int increment = 1);

        /// <summary>
        /// Reports a continuous metric, such as CPU or pooled resource usage. These metrics are typically reported proactively.
        /// </summary>
        /// <param name="counter">The name of the counter you are reporting</param>
        /// <param name="value">The continuous value of the metric to report</param>
        /// <param name="dimensions">The dimensions (string properties) to apply to this metric</param>
        void ReportContinuous(string counter, DimensionSet dimensions, double value);

        /// <summary>
        /// Behaves the same as <see cref="ReportInstant">ReportInstant</see>, except that values are tracked as standard percentiles (p25, p50, p75, p95, p99)
        /// rather than as a single moving average value.
        /// </summary>
        /// <param name="counter"></param>
        /// <param name="value"></param>
        /// <param name="dimensions"></param>
        void ReportPercentile(string counter, DimensionSet dimensions, double value);

        /// <summary>
        /// Retrieves the current set of metrics as a dictionary
        /// </summary>
        /// <returns></returns>
        IReadOnlyDictionary<CounterInstance, double?> GetCurrentMetrics();

        /// <summary>
        /// Retrieves the current set of metrics with the given metric name, as a dictionary
        /// </summary>
        /// <returns></returns>
        IReadOnlyDictionary<CounterInstance, double?> GetCurrentMetrics(string metricName);

        /// <summary>
        /// Retrieves the first value found for the metric with the given metric name (including _p0.99... for percentiles)
        /// </summary>
        /// <returns></returns>
        double? GetCurrentMetric(string metricName);

        /// <summary>
        /// Associates this metric collector with an object which is able to provide proactive continuous metrics.
        /// The source is typically something like a resource pool or throughput meter that measures usage,
        /// or it could be an interface into system counters such as hard drive or CPU usage.
        /// Once registered, this metric collector will periodically invoke ReportMetrics on the source to get current data.
        /// </summary>
        /// <param name="reportable"></param>
        void AddMetricSource(IMetricSource reportable);

        void RemoveMetricSource(IMetricSource reportable);
    }
}
