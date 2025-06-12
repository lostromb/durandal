using Durandal.API;
using Durandal.Common.Instrumentation;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Logger
{
    /// <summary>
    /// Stores constant and query-specific context (trace ID, component ID, valid levels, thread pool) for logger instances.
    /// </summary>
    public class LoggerContext
    {
        // VARIABLE
        public string ComponentName { get; set; }
        public Guid? TraceId { get; set; }
        public LogLevel ValidLogLevels { get; set; }
        public DataPrivacyClassification DefaultPrivacyClass { get; set; }
        public DataPrivacyClassification ValidPrivacyClasses { get; set; }

        // CONSTANT
        public LogLevel MaxLogLevels { get; set; }
        public DataPrivacyClassification MaxPrivacyClasses { get; set; }

        /// <summary>
        /// The thread pool to delegate async logging events to (that is, invocations of Log() that accept Func(() => {}) parameters for deferred logging of complex data).
        /// If this thread pool is null (which is the default), then all deferred logging will be done synchronously.
        /// </summary>
        public IThreadPool BackgroundLoggingThreadPool { get; set; }

        public LoggerContext Clone(
            string componentName,
            Guid? traceId,
            LogLevel validLogLevels,
            DataPrivacyClassification defaultPrivacyClass,
            DataPrivacyClassification validPrivacyClasses)
        {
            return new LoggerContext()
            {
                ComponentName = componentName,
                TraceId = traceId,
                ValidLogLevels = validLogLevels,
                DefaultPrivacyClass = defaultPrivacyClass,
                ValidPrivacyClasses = validPrivacyClasses,
                MaxLogLevels = this.MaxLogLevels,
                MaxPrivacyClasses = this.MaxPrivacyClasses,
                BackgroundLoggingThreadPool = this.BackgroundLoggingThreadPool,
            };
        }
    }
}
