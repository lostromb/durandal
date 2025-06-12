namespace Durandal.Common.Logger
{
    using Durandal.API;
    using Durandal.Common.Time;
    using Durandal.Common.Instrumentation;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.Tasks;
    using Utils;
    using Durandal.Common.Events;

    /// <summary>
    /// This logger abstracts the behavior of multiple loggers into one object.
    /// </summary>
    public class AggregateLogger : LoggerBase
    {
        public AggregateLogger(
            string componentName,
            IThreadPool backgroundLogThreadPool,
            params ILogger[] loggers)
            : base(new AggregateLoggerCore(loggers),
                new LoggerContext()
                {
                    ComponentName = componentName,
                    TraceId = null,
                    ValidLogLevels = LogLevel.All, // OPT this is potentially inefficient; we could save some cycles by doing filtering early on, 
                    MaxLogLevels = LogLevel.All, // but this also has potential for bugs as all the derived loggers won't have the exact same filterset
                    DefaultPrivacyClass = GetDefaultPrivacyClass(loggers),
                    ValidPrivacyClasses = DataPrivacyClassification.All,
                    MaxPrivacyClasses = DataPrivacyClassification.All,
                    BackgroundLoggingThreadPool = backgroundLogThreadPool,
                })
        {
        }

        /// <summary>
        /// Private constructor for creating inherited logger objects
        /// </summary>
        /// <param name="core"></param>
        /// <param name="context"></param>
        private AggregateLogger(AggregateLoggerCore core, LoggerContext context)
            : base(
                core.CloneCore(context.TraceId, context.ComponentName, context.ValidLogLevels, context.DefaultPrivacyClass, context.ValidPrivacyClasses),
                context)
        {
        }

        protected override ILogger CloneImplementation(ILoggerCore core, LoggerContext context)
        {
            return new AggregateLogger((AggregateLoggerCore)core, context);
        }

        private static DataPrivacyClassification GetDefaultPrivacyClass(ILogger[] loggers)
        {
            return loggers.Length == 0 ? DataPrivacyClassification.Unknown : loggers[0].DefaultPrivacyClass;
        }

        public IEnumerable<ILogger> InnerLoggers => ((AggregateLoggerCore)Core).InnerLoggers;

        private class AggregateLoggerCore : ILoggerCore
        {
            private readonly ILogger[] _loggers;

            public AggregateLoggerCore(ILogger[] loggers)
            {
                // Validate that all of the loggers are non-null
                loggers.AssertNonNull(nameof(loggers));
                foreach (ILogger logger in loggers)
                {
                    logger.AssertNonNull(nameof(logger));
                }

                _loggers = loggers;
            }

            private AggregateLoggerCore(ILogger[] loggers, AsyncEvent<LogUpdatedEventArgs> eventEmitter)
            {
                _loggers = loggers;
            }

            public IEnumerable<ILogger> InnerLoggers => _loggers;

            public AggregateLoggerCore CloneCore(
                Guid? traceId,
                string newComponentName,
                LogLevel allowedLogLevels,
                DataPrivacyClassification defaultPrivacyClass,
                DataPrivacyClassification allowedPrivacyClasses)
            {
                ILogger[] clones = new ILogger[_loggers.Length];
                for (int c = 0; c < _loggers.Length; c++)
                {
                    clones[c] = _loggers[c].CreateTraceLogger(traceId).Clone(newComponentName, allowedLogLevels, defaultPrivacyClass, allowedPrivacyClasses);
                }

                return new AggregateLoggerCore(clones);
            }

            public void LoggerImplementation(PooledLogEvent value)
            {
                foreach (ILogger logger in _loggers)
                {
                    value.IncrementReferenceCount();
                    logger.Log(value);
                }

                value.Dispose();
            }

            public void Dispose() { }

            public async Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking)
            {
                // do flush in parallel
                Task[] flushTasks = new Task[_loggers.Length];
                for (int c = 0; c < _loggers.Length; c++)
                {
                    flushTasks[c] = _loggers[c].Flush(cancellizer, realTime, blocking);
                }

                for (int c = 0; c < _loggers.Length; c++)
                {
                    await flushTasks[c].ConfigureAwait(false);
                }
            }
        }
    }
}
