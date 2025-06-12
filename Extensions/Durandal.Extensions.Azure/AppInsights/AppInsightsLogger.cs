
namespace Durandal.Extensions.Azure.AppInsights
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Utils;
    using Durandal.Common.Logger;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using System.Net;
    using Durandal.Common.Time;
    using Durandal.Common.Instrumentation;
    using Microsoft.ApplicationInsights.Extensibility;
    using Durandal.API;
    using Durandal.Common.Tasks;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// A logger which writes to azure app insights
    /// </summary>  
    public class AppInsightsLogger : LoggerBase
    {
        public AppInsightsLogger(
            string connectionString,
            string componentName = "Main",
            IThreadPool backgroundLogThreadPool = null,
            LogLevel validLogLevels = DEFAULT_LOG_LEVELS,
            LogLevel maxLogLevels = LogLevel.All,
            DataPrivacyClassification maxPrivacyClasses = DataPrivacyClassification.All,
            DataPrivacyClassification defaultPrivacyClass = DataPrivacyClassification.SystemMetadata)
                : base(new AppInsightsLoggerCore(connectionString),
                      new LoggerContext()
                      {
                          ComponentName = componentName,
                          TraceId = null,
                          ValidLogLevels = validLogLevels,
                          MaxLogLevels = maxLogLevels,
                          DefaultPrivacyClass = defaultPrivacyClass,
                          ValidPrivacyClasses = DataPrivacyClassification.All,
                          MaxPrivacyClasses = maxPrivacyClasses,
                          BackgroundLoggingThreadPool = backgroundLogThreadPool
                      })
        {
        }

        /// <summary>
        /// Private constructor for creating inherited logger objects
        /// </summary>
        /// <param name="core">Logger core</param>
        /// <param name="context">Cloned context</param>
        private AppInsightsLogger(ILoggerCore core, LoggerContext context)
                : base(core, context)
        {
        }

        protected override ILogger CloneImplementation(ILoggerCore core, LoggerContext context)
        {
            return new AppInsightsLogger(core, context);
        }

        public override void Log(Exception exception, LogLevel level = LogLevel.Err, Guid? traceId = null, DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown, DateTimeOffset? timestamp = null)
        {
            if ((ValidLogLevels & level) != 0)
            {
                if (privacyClass == DataPrivacyClassification.Unknown)
                {
                    privacyClass = DefaultPrivacyClass;
                }

                // abort logging if the message contains a privacy class that we are not allowed to output
                if ((privacyClass & ~(ValidPrivacyClasses)) != 0)
                {
                    return;
                }

                ((AppInsightsLoggerCore)Core).LogException(exception, level, traceId, timestamp.GetValueOrDefault(GetHighResolutionTime()), privacyClass);
            }
        }

        /// <summary>
        /// This is the context object shared between all clones of the app insights logger
        /// </summary>
        private class AppInsightsLoggerCore : ILoggerCore
        {
            private readonly TelemetryClient _telemetry;
            private readonly string _serviceName;
            private readonly string _machineName;

            private int _disposed = 0;

            public AppInsightsLoggerCore(string connectionString)
            {
                connectionString.AssertNonNullOrEmpty(nameof(connectionString));

                // Assert that the connection string is not an instrumentation key by itself
                Guid blah;
                if (connectionString.Length <= 48 && Guid.TryParse(connectionString, out blah))
                {
                    throw new ArgumentException("Plain AppInsights instrumentation keys are deprecated. Please replace the bare key with a full connection string.");
                }

                _serviceName = Process.GetCurrentProcess().ProcessName;
                _machineName = Dns.GetHostName();
                TelemetryConfiguration telemetryConfig = new TelemetryConfiguration();
                telemetryConfig.ConnectionString = connectionString;
                _telemetry = new TelemetryClient(telemetryConfig);
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~AppInsightsLoggerCore()
            {
                Dispose(false);
            }
#endif

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
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

            public Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking)
            {
                _telemetry.Flush();
                return DurandalTaskExtensions.NoOpTask;
            }

            public void LoggerImplementation(PooledLogEvent value)
            {
                // OPT this is one path where we can no longer rely on pooled log buffers, without
                // writing our own implementation of the appinsights transport layer
                TraceTelemetry eventData = new TraceTelemetry(value.ToString());
                eventData.SeverityLevel = ConvertLogLevel(value.Level);
                if (value.TraceId.HasValue)
                {
                    eventData.Context.Operation.Id = CommonInstrumentation.FormatTraceId(value.TraceId.Value);
                }

                eventData.Context.Cloud.RoleName = _serviceName;
                eventData.Context.Cloud.RoleInstance = _machineName;
                eventData.Timestamp = value.Timestamp;
                // FIXME we don't pass privacy classification at all
                _telemetry.TrackTrace(eventData);
                value.Dispose();
            }

            public void LogException(Exception exception, LogLevel level, Guid? traceId, DateTimeOffset timestamp, DataPrivacyClassification privacyClassification)
            {
                ExceptionTelemetry telemetryData = new ExceptionTelemetry(exception);
                telemetryData.SeverityLevel = ConvertLogLevel(level);
                if (traceId.HasValue)
                {
                    telemetryData.Context.Operation.Id = CommonInstrumentation.FormatTraceId(traceId);
                }

                telemetryData.Context.Cloud.RoleName = _serviceName;
                telemetryData.Context.Cloud.RoleInstance = _machineName;
                telemetryData.Timestamp = timestamp;
                // FIXME we don't pass privacy classification at all
                _telemetry.TrackException(telemetryData);
            }

            private static SeverityLevel ConvertLogLevel(LogLevel level)
            {
                switch (level)
                {
                    case LogLevel.Err:
                        return SeverityLevel.Error;
                    case LogLevel.Wrn:
                        return SeverityLevel.Warning;
                    case LogLevel.Ins:
                        return SeverityLevel.Information;
                    case LogLevel.Std:
                        return SeverityLevel.Information;
                    case LogLevel.Vrb:
                        return SeverityLevel.Verbose;
                    default:
                        return SeverityLevel.Information;
                }
            }
        }
    }
}
