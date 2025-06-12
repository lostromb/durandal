using Durandal.API;
using Durandal.Common.Instrumentation;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Logger
{
    /// <summary>
    /// Base class for loggers that write messages to an abstract console window such as console stdout, standard trace out, etc.
    /// The actual act of writing to the console is implemented by a delegate function WriteLogDelegate
    /// </summary>
    public class StdOutLoggerBase : LoggerBase
    {
        /// <summary>
        /// A simple delegate which implements how log events are actually implemented
        /// </summary>
        /// <param name="e"></param>
        private delegate void WriteLogDelegate(PooledLogEvent e);
        
        public StdOutLoggerBase(
            IThreadPool backgroundLogThreadPool,
            string componentName = "Main",
            LogLevel validLogLevels = DEFAULT_LOG_LEVELS,
            LogLevel maxLogLevels = LogLevel.All,
            DataPrivacyClassification defaultPrivacyClass = DataPrivacyClassification.SystemMetadata,
            DataPrivacyClassification validPrivacyClasses = DataPrivacyClassification.All,
            DataPrivacyClassification maxPrivacyClasses = DataPrivacyClassification.All)
                : base(new StdOutLoggerCore(),
                      new LoggerContext()
                      {
                          ComponentName = componentName,
                          TraceId = null,
                          ValidLogLevels = validLogLevels,
                          MaxLogLevels = maxLogLevels,
                          DefaultPrivacyClass = defaultPrivacyClass,
                          ValidPrivacyClasses = validPrivacyClasses,
                          MaxPrivacyClasses = maxPrivacyClasses,
                          BackgroundLoggingThreadPool = backgroundLogThreadPool
                      })
        {
            ((StdOutLoggerCore)_core).WriteImpl = LogToOutputImpl;
        }

        /// <summary>
        /// Private constructor for creating inherited logger objects
        /// </summary>
        /// <param name="core"></param>
        /// <param name="context"></param>
        private StdOutLoggerBase(ILoggerCore core, LoggerContext context)
                : base(core, context)
        {
        }

        protected override ILogger CloneImplementation(ILoggerCore core, LoggerContext context)
        {
            return new StdOutLoggerBase(core, context);
        }

        /// <summary>
        /// Implements the specific logic to write log messages to output
        /// </summary>
        /// <param name="logEvent"></param>
        protected virtual void LogToOutputImpl(PooledLogEvent logEvent) { }

        /// <summary>
        /// This is the context object shared between all clones of the console logger
        /// </summary>
        private class StdOutLoggerCore : ILoggerCore
        {
            public StdOutLoggerCore()
            {
            }

            internal WriteLogDelegate WriteImpl { get; set; }

            public void Dispose() { }

            public Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking)
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            public void LoggerImplementation(PooledLogEvent value)
            {
                WriteImpl(value);
            }
        }
    }
}
