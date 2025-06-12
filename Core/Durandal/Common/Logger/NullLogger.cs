namespace Durandal.Common.Logger
{
    using Durandal.API;
    using Durandal.Common.Time;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Tasks;
    using System.Text;
    using Utils;

    /// <summary>
    /// A black hole logger implementation
    /// </summary>
    public class NullLogger : ILogger
    {
        /// <summary>
        /// The singleton null logger object. You should never need to create your own instances.
        /// </summary>
        public static readonly ILogger Singleton = new NullLogger();

        private NullLogger()
        {
        }

        public string ComponentName => "NULL";
        public Guid? TraceId => null;
        public LogLevel ValidLogLevels => LogLevel.None;
        public DataPrivacyClassification DefaultPrivacyClass => DataPrivacyClassification.Unknown;
        public DataPrivacyClassification ValidPrivacyClasses => DataPrivacyClassification.Unknown;

        public ILogger CreateTraceLogger(Guid? traceId, string newComponentName = null) { return this; }
        public ILogger Clone(
            string newComponentName = null,
            LogLevel? allowedLogLevels = null,
            DataPrivacyClassification? defaultPrivacyClass = null,
            DataPrivacyClassification? allowedPrivacyClasses = null) { return this; }
        public void Log(object value, LogLevel level = LogLevel.Std, Guid? traceId = null, DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown, DateTimeOffset? timestamp = null) { }
        public void Log(string value, LogLevel level = LogLevel.Std, Guid? traceId = null, DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown, DateTimeOffset? timestamp = null) { }
        public void Log(StringBuilder value, LogLevel level = LogLevel.Std, Guid? traceId = null, DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown, DateTimeOffset? timestamp = null) { }
        public void Log(Exception value, LogLevel level = LogLevel.Err, Guid? traceId = null, DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown, DateTimeOffset? timestamp = null) { }
        public void Log(Func<string> producer, LogLevel level = LogLevel.Std, Guid? traceId = null, DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown, DateTimeOffset? timestamp = null) { }
        public void LogFormat<T0>(LogLevel level, DataPrivacyClassification privacyClass, string formatString, T0 arg0) { }
        public void LogFormat<T0, T1>(LogLevel level, DataPrivacyClassification privacyClass, string formatString, T0 arg0, T1 arg1) { }
        public void LogFormat<T0, T1, T2>(LogLevel level, DataPrivacyClassification privacyClass, string formatString, T0 arg0, T1 arg1, T2 arg2) { }
        public void LogFormat(LogLevel level, DataPrivacyClassification privacyClass, string formatString, params object[] args) { }
        public void DispatchAsync(Action<ILogger, DateTimeOffset> processorDelegate) { }
        public void Log(LogEvent e) { }
        public void Log(PooledLogEvent e)
        {
            e?.Dispose();
        }

        public void Dispose() { }
        public Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking) { return DurandalTaskExtensions.NoOpTask; }
    }
}
