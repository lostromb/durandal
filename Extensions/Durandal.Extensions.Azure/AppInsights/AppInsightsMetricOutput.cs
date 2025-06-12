
namespace Durandal.Extensions.Azure.AppInsights
{
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Metrics output which writes to AppInsights
    /// </summary>
    public class AppInsightsMetricOutput : IMetricOutput
    {
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetry;
        private readonly string _serviceName;
        private readonly string _machineName;
        private int _disposed = 0;

        public AppInsightsMetricOutput(ILogger logger, string connectionString)
        {
            connectionString.AssertNonNullOrEmpty(nameof(connectionString));
            // Assert that the connection string is not an instrumentation key by itself
            Guid blah;
            if (connectionString.Length <= 48 && Guid.TryParse(connectionString, out blah))
            {
                throw new ArgumentException("Plain AppInsights instrumentation keys are deprecated. Please replace the bare key with a full connection string.");
            }

            _logger = logger ?? NullLogger.Singleton;
            _logger.Log("Initializing AppInsights metric output...");
            TelemetryConfiguration telemetryConfig = new TelemetryConfiguration();
            telemetryConfig.ConnectionString = connectionString;
            _telemetry = new TelemetryClient(telemetryConfig);
            _serviceName = Process.GetCurrentProcess().ProcessName;
            _machineName = Dns.GetHostName();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AppInsightsMetricOutput()
        {
            Dispose(false);
        }
#endif

        public async Task OutputAggregateMetrics(IReadOnlyDictionary<CounterInstance, double?> currentMetrics)
        {
            await DurandalTaskExtensions.NoOpTask;

            // Isolate all metrics into separate bins based on unique dimension sets
            IDictionary<DimensionSet, Dictionary<string, double>> bins = new Dictionary<DimensionSet, Dictionary<string, double>>();

            foreach (var metric in currentMetrics)
            {
                Dictionary<string, double> bin;
                if (!bins.TryGetValue(metric.Key.Dimensions, out bin))
                {
                    bin = new Dictionary<string, double>();
                    bins[metric.Key.Dimensions] = bin;
                }

                bin[metric.Key.CounterName] = metric.Value.GetValueOrDefault(0);
            }

            // Then output a separate metric report event for each bin
            foreach (var binnedEvents in bins)
            {
                EventTelemetry eventData = new EventTelemetry("MetricReport");
                eventData.Context.Cloud.RoleName = _serviceName;
                eventData.Context.Cloud.RoleInstance = _machineName;
                foreach (var metric in binnedEvents.Value)
                {
                    eventData.Metrics[metric.Key] = metric.Value;
                    foreach (var dimension in binnedEvents.Key)
                    {
                        eventData.Properties[dimension.Key] = dimension.Value;
                    }
                }

                _telemetry.TrackEvent(eventData);
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
                _telemetry.Flush();
            }
        }
    }
}
