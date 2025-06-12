using Durandal.API;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Logger
{
    /// <summary>
    /// Wraps a regular ILogger implementation, passing along many of its functions straight through,
    /// but turning set() and clone() operations into no-ops so that service ID and trace ID cannot be changed.
    /// </summary>
    public class ImmutableLogger : ILogger
    {
        private readonly ILogger _impl;

        public ImmutableLogger(ILogger impl)
        {
            _impl = impl.AssertNonNull(nameof(impl));
        }

        public string ComponentName =>_impl.ComponentName;
        public Guid? TraceId => _impl.TraceId;
        public LogLevel ValidLogLevels => _impl.ValidLogLevels;
        public DataPrivacyClassification ValidPrivacyClasses => _impl.ValidPrivacyClasses;
        public DataPrivacyClassification DefaultPrivacyClass => _impl.DefaultPrivacyClass;

        public ILogger Clone(
            string newComponentName = null,
            LogLevel? allowedLogLevels = null,
            DataPrivacyClassification? defaultPrivacyClass = null,
            DataPrivacyClassification? allowedPrivacyClasses = null)
        {
            return this;
        }

        public ILogger CreateTraceLogger(Guid? traceId, string newComponentName = null)
        {
            return this;
        }

        public Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking = false)
        {
            return _impl.Flush(cancellizer, realTime, blocking);
        }

        public void Log(LogEvent value)
        {
            _impl.Log(value);
        }

        public void Log(PooledLogEvent value)
        {
            _impl.Log(value);
        }

        public void Log(
            Func<string> producer,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null)
        {
            _impl.Log(producer, level, _impl.TraceId, privacyClass, timestamp);
        }

        public void Log(
            string value,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null)
        {
            _impl.Log(value, level, _impl.TraceId, privacyClass, timestamp);
        }

        public void Log(
            StringBuilder value,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null)
        {
            _impl.Log(value, level, _impl.TraceId, privacyClass, timestamp);
        }

        public void Log(
            Exception value,
            LogLevel level = LogLevel.Err,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null)
        {
            _impl.Log(value, level, _impl.TraceId, privacyClass, timestamp);
        }

        public void Log(
            object value,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null)
        {
            _impl.Log(value, level, _impl.TraceId, privacyClass, timestamp);
        }

        public void LogFormat<T0>(
            LogLevel level,
            DataPrivacyClassification privacyClass,
            string formatString,
            T0 arg0)
        {
            _impl.LogFormat(level, privacyClass, formatString, arg0);
        }

        public void LogFormat<T0, T1>(
            LogLevel level,
            DataPrivacyClassification privacyClass,
            string formatString,
            T0 arg0,
            T1 arg1)
        {
            _impl.LogFormat(level, privacyClass, formatString, arg0, arg1);
        }

        public void LogFormat<T0, T1, T2>(
            LogLevel level,
            DataPrivacyClassification privacyClass,
            string formatString,
            T0 arg0,
            T1 arg1,
            T2 arg2)
        {
            _impl.LogFormat(level, privacyClass, formatString, arg0, arg1, arg2);
        }

        public void LogFormat(
            LogLevel level,
            DataPrivacyClassification privacyClass,
            string formatString,
            params object[] args)
        {
            _impl.LogFormat(level, privacyClass, formatString, args);
        }

        public void DispatchAsync(Action<ILogger, DateTimeOffset> processorDelegate)
        {
            _impl.DispatchAsync(processorDelegate);
        }
    }
}
