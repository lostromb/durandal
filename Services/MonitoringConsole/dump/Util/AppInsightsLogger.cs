using Durandal.Common.Logger;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.Util
{
    /// <summary>
    /// ILogger implementation backed by AppInsights
    /// </summary>
    public class AppInsightsLogger : ILogger
    {
        public const LogLevel DEFAULT_LOG_LEVELS = LogLevel.Std | LogLevel.Err | LogLevel.Wrn | LogLevel.Ins;

        private AppInsightsLoggerCore _loggerImpl;

        private string _thisComponent;
        private string _thisTraceId;

        /// <summary>
        /// Initializes a logger which wraps around an existing telemetry client
        /// </summary>
        /// <param name="existingTelemetry"></param>
        /// <param name="componentName"></param>
        /// <param name="maxLoggingLevel"></param>
        public AppInsightsLogger(TelemetryClient existingTelemetry, string componentName = "Main", LogLevel maxLoggingLevel = DEFAULT_LOG_LEVELS)
        {
            _loggerImpl = new AppInsightsLoggerCore(existingTelemetry);
            MaxLevels = maxLoggingLevel;
            ValidLevels = maxLoggingLevel;
            _thisComponent = componentName ?? "Main";
            _thisTraceId = null;
            Log("AppInsights logger initialized");
        }

        /// <summary>
        /// Initializes a logger that creates a new telemetry client with the specified key
        /// </summary>
        /// <param name="telemetryKey"></param>
        /// <param name="componentName"></param>
        /// <param name="maxLoggingLevel"></param>
        public AppInsightsLogger(string telemetryKey, string componentName = "Main", LogLevel maxLoggingLevel = DEFAULT_LOG_LEVELS)
        {
            if (string.IsNullOrEmpty(telemetryKey))
            {
                throw new ArgumentNullException("AppInsights telemetry key is null or empty");
            }

            _loggerImpl = new AppInsightsLoggerCore(telemetryKey);
            MaxLevels = maxLoggingLevel;
            ValidLevels = maxLoggingLevel;
            _thisComponent = componentName ?? "Main";
            _thisTraceId = null;
            Log("AppInsights logger initialized");
        }

        /// <summary>
        /// Private constructor for creating inherited logger objects
        /// </summary>
        /// <param name="componentName"></param>
        /// <param name="stream"></param>
        private AppInsightsLogger(string componentName, string traceId, AppInsightsLoggerCore core, LogLevel levels, LogLevel maxLevels)
        {
            _thisComponent = componentName;
            _loggerImpl = core;
            _thisTraceId = traceId;
            ValidLevels = levels;
            MaxLevels = maxLevels;
        }

        public ILogger Clone(string newComponentName, string traceId)
        {
            if (newComponentName != null && !newComponentName.Equals(_thisComponent) ||
                (_thisTraceId == null && traceId != null) || (_thisTraceId != null && traceId == null) ||
                (_thisTraceId != null && traceId != null && _thisTraceId.Equals(traceId)))
            {
                return new AppInsightsLogger(newComponentName, traceId, _loggerImpl, ValidLevels, MaxLevels);
            }

            return this;
        }

        public ILogger Clone(string newComponentName)
        {
            if (newComponentName != null && !newComponentName.Equals(_thisComponent))
            {
                return new AppInsightsLogger(newComponentName, _thisTraceId, _loggerImpl, ValidLevels, MaxLevels);
            }

            return this;
        }

        public void SuppressAllOutput()
        {
            _loggerImpl.SuppressAllOutput();
        }

        public void UnsuppressAllOutput()
        {
            _loggerImpl.UnsuppressAllOutput();
        }

        public void Flush(bool blocking = false)
        {
            _loggerImpl.Flush();
        }

        public LogLevel ValidLevels
        {
            get; set;
        }

        public LogLevel MaxLevels
        {
            get; set;
        }

        public string ComponentName
        {
            get
            {
                return _thisComponent;
            }
        }

        public string TraceId
        {
            get
            {
                return _thisTraceId;
            }
            set
            {
                _thisTraceId = value;
            }
        }

        public void Log(object value, LogLevel level = LogLevel.Std, string traceId = null)
        {
            if (value == null)
                Log("null", level, traceId);
            else
                Log(value.ToString(), level, traceId);
        }

        public void Log(string value, LogLevel level = LogLevel.Std, string traceId = null)
        {
            // Convert it into an event
            LogEvent newEvent = new LogEvent(_thisComponent, value ?? "null", level, DateTime.UtcNow, traceId ?? _thisTraceId);
            Log(newEvent);
        }

        public void Log(LogEvent value)
        {
            if ((MaxLevels & ValidLevels & value.Level) != 0)
            {
                _loggerImpl.Log(value);
            }
        }

        public void Log(Func<string> producer, LogLevel level = LogLevel.Std, string traceId = null)
        {
            if ((MaxLevels & ValidLevels & level) != 0)
            {
                Log(producer(), level, traceId);
            }
        }

        public void Log(Exception value, LogLevel level = LogLevel.Err, string traceId = null)
        {
            if ((MaxLevels & ValidLevels & level) != 0)
            {
                _loggerImpl.Log(value, _thisComponent, traceId ?? _thisTraceId);
            }
        }

        public LoggingHistory GetHistory()
        {
            return null;
        }

        public event EventHandler<LogUpdatedEventArgs> LogUpdated
        {
            add { _loggerImpl.LogUpdated += value; }
            remove { _loggerImpl.LogUpdated -= value; }
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// This is the context object shared between all clones of the logger
        /// </summary>
        private class AppInsightsLoggerCore
        {
            private volatile bool _suppressed = false;
            private LogEventEmitter _eventEmitter;
            private TelemetryClient _telemetry;
            private TelemetryConfiguration _telemetryConfig;

            public AppInsightsLoggerCore(TelemetryClient client)
            {
                _eventEmitter = new LogEventEmitter();
                _telemetry = client;
            }

            public AppInsightsLoggerCore(string appKey)
            {
                _eventEmitter = new LogEventEmitter();

                _telemetryConfig = new TelemetryConfiguration()
                {
                    InstrumentationKey = appKey
                };
                _telemetry = new TelemetryClient(_telemetryConfig);
            }

            public void SuppressAllOutput()
            {
                _suppressed = true;
            }

            public void UnsuppressAllOutput()
            {
                _suppressed = false;
            }

            public void Flush()
            {
                _telemetry.Flush();
            }

            public void Log(LogEvent value)
            {
                if (!_suppressed && value.Level != LogLevel.None)
                {
                    Dictionary<string, string> properties = new Dictionary<string, string>();
                    properties["TraceId"] = value.TraceId;
                    properties["Component"] = value.Component;
                    _telemetry.TrackTrace(value.Message, ConvertSeverityLevel(value.Level), properties);
                    OnLogUpdated(value);
                }
            }

            public void Log(Exception value, string component, string traceId)
            {
                if (!_suppressed)
                {
                    Dictionary<string, string> properties = new Dictionary<string, string>();
                    properties["TraceId"] = traceId;
                    properties["Component"] = component;
                    _telemetry.TrackException(value, properties);
                }
            }

            private SeverityLevel ConvertSeverityLevel(LogLevel level)
            {
                switch (level)
                {
                    case LogLevel.Err:
                        return SeverityLevel.Error;
                    case LogLevel.Wrn:
                        return SeverityLevel.Warning;
                    case LogLevel.Std:
                        return SeverityLevel.Information;
                    case LogLevel.Vrb:
                    case LogLevel.Ins:
                        return SeverityLevel.Verbose;
                    default:
                        return SeverityLevel.Verbose;
                }
            }

            public event EventHandler<LogUpdatedEventArgs> LogUpdated
            {
                add { _eventEmitter.Add(value); }
                remove { _eventEmitter.Remove(value); }
            }

            private void OnLogUpdated(LogEvent e)
            {
                LogUpdatedEventArgs args = new LogUpdatedEventArgs(e);
                if (_eventEmitter != null)
                {
                    _eventEmitter.Fire(this, args);
                }
            }
        }
    }
}