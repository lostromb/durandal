using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Instrumentation
{
    /// <summary>
    /// Represents an object to which a metric collector can output its aggregated metrics, e.g. to a file,
    /// to AppInsights, or some other metric dashboard
    /// </summary>
    public interface IMetricOutput : IDisposable
    {
        Task OutputAggregateMetrics(IReadOnlyDictionary<CounterInstance, double?> currentMetrics);
    }
}
