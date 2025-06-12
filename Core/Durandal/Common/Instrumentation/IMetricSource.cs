using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Represents an object that is able to proactively report metrics about itself to an IMetricReporter.
    /// A reportable is typically something like a resource pool or throughput meter that measures usage,
    /// or it could be an interface into system counters such as hard drive or CPU usage. It defines one method
    /// which is periodically called by a <see cref="IMetricCollector"/>  in order to query the latest status.
    /// </summary>
    public interface IMetricSource
    {
        /// <summary>
        /// Called by a metric reporter when it wants to know the latest status of this object.
        /// The implementation of this method will typically make calls to
        /// <see cref="IMetricCollector.ReportContinuous(string, double, DimensionSet)"/> with its current status numbers.
        /// </summary>
        /// <param name="reporter"></param>
        void ReportMetrics(IMetricCollector reporter);

        void InitializeMetrics(IMetricCollector collector);
    }
}
