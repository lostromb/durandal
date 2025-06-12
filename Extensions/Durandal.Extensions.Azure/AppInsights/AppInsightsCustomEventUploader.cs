

namespace Durandal.Extensions.Azure.AppInsights
{
    using Durandal.API;
    using Durandal.Common.Collections;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Utils;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class AppInsightsCustomEventUploader
    {
        private readonly ILogger _logger;
        private readonly IInstrumentationRepository _instrumentationSource;
        private readonly TelemetryClient _telemetry;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly IStringDecrypterPii _piiDecrypter;

        private static readonly IReadOnlySet<string> LATENCY_KEYS_TO_EXPAND = new FastConcurrentHashSet<string>()
        {
            CommonInstrumentation.Key_Latency_Plugin_Execute,
            CommonInstrumentation.Key_Latency_Plugin_Trigger,
            CommonInstrumentation.Key_Latency_LU_Resolver,
        };

        public AppInsightsCustomEventUploader(ILogger logger, IInstrumentationRepository instrumentation, string connectionString, IMetricCollector metrics, DimensionSet dimensions, IStringDecrypterPii piiDecrypter)
        {
            _logger = logger;
            _instrumentationSource = instrumentation;
            _metrics = new WeakPointer<IMetricCollector>(metrics);
            _piiDecrypter = piiDecrypter;
            _dimensions = dimensions;

            connectionString.AssertNonNullOrEmpty(nameof(connectionString));
            // Assert that the connection string is not an instrumentation key by itself
            Guid blah;
            if (connectionString.Length <= 48 && Guid.TryParse(connectionString, out blah))
            {
                throw new ArgumentException("Plain AppInsights instrumentation keys are deprecated. Please replace the bare key with a full connection string.");
            }

            TelemetryConfiguration telemetryConfig = new TelemetryConfiguration();
            telemetryConfig.ConnectionString = connectionString;
            _telemetry = new TelemetryClient(telemetryConfig);
        }

        public async Task UploadTracesToAppInsights(int batchSize)
        {
            try
            {
                ISet<Guid> unimportedTraceIds = await _instrumentationSource.GetTraceIdsNotImportedToAppInsights(batchSize);

                if (unimportedTraceIds.Count == 0)
                {
                    return;
                }

                IList<UnifiedTrace> traces = await _instrumentationSource.GetTraceData(unimportedTraceIds, _piiDecrypter);

                _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Uploading {0} traces to AppInsights", unimportedTraceIds.Count);

                foreach (UnifiedTrace trace in traces)
                {
                    if (trace == null)
                    {
                        _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_AIMetrics_NullTraces, _dimensions);
                        continue;
                    }

                    EventTelemetry eventData = new EventTelemetry("InstrumentedEvent");
                    
                    foreach (var latencyMetric in trace.Latencies)
                    {
                        if (LATENCY_KEYS_TO_EXPAND.Contains(latencyMetric.Key))
                        {
                            foreach (var latencyEvent in latencyMetric.Value.Values)
                            {
                                if (!string.IsNullOrEmpty(latencyEvent.Id))
                                {
                                    eventData.Metrics["Latency-" + latencyMetric.Key + "-" + latencyEvent.Id] = latencyEvent.Value;
                                }
                            }
                        }

                        if ((latencyMetric.Value?.Average).HasValue)
                        {
                            eventData.Metrics["Latency-" + latencyMetric.Key] = latencyMetric.Value.Average.Value;
                        }
                    }
                    foreach (var sizeMetric in trace.Sizes)
                    {
                        if ((sizeMetric.Value?.Average).HasValue)
                        {
                            eventData.Metrics["Size-" + sizeMetric.Key] = sizeMetric.Value.Average.Value;
                        }
                    }

                    eventData.Timestamp = trace.TraceStart;
                    eventData.Context.Device.Id = trace.ClientId;
                    eventData.Context.Device.Model = trace.ClientVersion;
                    eventData.Context.Device.Type = trace.FormFactor.ToString();
                    eventData.Context.Operation.Id = CommonInstrumentation.FormatTraceId(trace.TraceId);
                    eventData.Context.User.Id = trace.UserId;

                    eventData.Properties["TraceId"] = CommonInstrumentation.FormatTraceId(trace.TraceId);
                    eventData.Properties["TraceStart"] = trace.TraceStart.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    eventData.Properties["TraceEnd"] = trace.TraceEnd.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    eventData.Properties["TraceDuration"] = trace.TraceDuration.ToString();
                    eventData.Properties["ClientId"] = trace.ClientId;
                    eventData.Properties["UserId"] = trace.UserId;
                    eventData.Properties["DialogEventType"] = trace.DialogEventType.ToString();
                    eventData.Properties["InputString"] = trace.InputString;
                    eventData.Properties["ResponseText"] = trace.ResponseText;
                    eventData.Properties["ErrorMessage"] = trace.ErrorMessage;
                    eventData.Properties["ErrorLogCount"] = trace.ErrorLogCount.ToString();
                    eventData.Properties["TriggeredDomain"] = trace.TriggeredDomain;
                    eventData.Properties["TriggeredIntent"] = trace.TriggeredIntent;
                    eventData.Properties["LUConfidence"] = trace.LUConfidence.GetValueOrDefault(0).ToString();
                    eventData.Properties["InputType"] = trace.InteractionType.ToString();
                    eventData.Properties["ClientType"] = trace.ClientType;
                    eventData.Properties["ClientVersion"] = trace.ClientVersion;
                    eventData.Properties["FormFactor"] = trace.FormFactor.ToString();
                    eventData.Properties["LogCount"] = trace.LogCount.ToString();
                    eventData.Properties["QueryFlags"] = trace.QueryFlags.ToString();
                    eventData.Properties["DialogHost"] = trace.DialogHost;
                    eventData.Properties["LUHost"] = trace.LUHost;
                    eventData.Properties["DialogProtocol"] = trace.DialogProtocol;
                    eventData.Properties["LUProtocol"] = trace.LUProtocol;

                    _telemetry.TrackEvent(eventData);
                    _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_AIMetrics_Sent, _dimensions);
                }

                _telemetry.Flush();
                await _instrumentationSource.MarkTraceIdsAsImportedToAppInsights(unimportedTraceIds);
                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_AIMetrics_TracesFinalized, _dimensions, unimportedTraceIds.Count);
            }
            catch (Exception e)
            {
                _logger.Log("Unhandled exception while doing instrumentation appinsights upload", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
        }
    }
}
