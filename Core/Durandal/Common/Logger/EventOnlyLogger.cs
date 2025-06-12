namespace Durandal.Common.Logger
{
    using Durandal.API;
    using Durandal.Common.Events;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Utils;

    /// <summary>
    /// A logger which doesn't output its logs anywhere, but only makes them available as events and as a logging history
    /// </summary>
    public class EventOnlyLogger : LoggerBase
    {
        public EventOnlyLogger(
            string componentName = "Main",
            LogLevel validLogLevels = DEFAULT_LOG_LEVELS,
            LogLevel maxLogLevels = LogLevel.All,
            DataPrivacyClassification maxPrivacyClasses = DataPrivacyClassification.All,
            DataPrivacyClassification defaultPrivacyClass = DataPrivacyClassification.SystemMetadata,
            IThreadPool backgroundLogThreadPool = null)
            : base(new EventLoggerCore(),
                      new LoggerContext()
                      {
                          ComponentName = componentName,
                          TraceId = null,
                          ValidLogLevels = validLogLevels,
                          MaxLogLevels = maxLogLevels,
                          DefaultPrivacyClass = defaultPrivacyClass,
                          ValidPrivacyClasses = maxPrivacyClasses,
                          MaxPrivacyClasses = maxPrivacyClasses,
                          BackgroundLoggingThreadPool = backgroundLogThreadPool,
                      })
        {
        }

        /// <summary>
        /// Private constructor for creating inherited logger objects
        /// </summary>
        /// <param name="core"></param>
        /// <param name="context"></param>
        private EventOnlyLogger(ILoggerCore core, LoggerContext context)
                : base(core, context)
        {
        }

        protected override ILogger CloneImplementation(ILoggerCore core, LoggerContext context)
        {
            return new EventOnlyLogger(core, context);
        }

        /// <summary>
        /// Gets the history of log events that passed through this logger
        /// </summary>
        public ILoggingHistory History => ((EventLoggerCore)Core).GetLoggingHistory();

        /// <summary>
        /// Gets the event which is fired when log events are raised
        /// </summary>
        public AsyncEvent<LogUpdatedEventArgs> LogUpdatedEvent => ((EventLoggerCore)Core).LogUpdatedEvent;

        /// <summary>
        /// Recursively trying to find an EventOnlyLogger inside of a potentially nested aggregate logger structure.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static EventOnlyLogger TryExtractFromAggregate(ILogger input)
        {
            if (input == null)
            {
                return null;
            }

            if (input is EventOnlyLogger)
            {
                return input as EventOnlyLogger;
            }

            if (input is AggregateLogger)
            {
                AggregateLogger agg = input as AggregateLogger;
                foreach (ILogger subLogger in agg.InnerLoggers)
                {
                    EventOnlyLogger extracted = TryExtractFromAggregate(subLogger);
                    if (extracted != null)
                    {
                        return extracted;
                    }
                }

            }

            return null;
        }

        /// <summary>
        /// This is the context object shared between all clones of the console logger
        /// </summary>
        private class EventLoggerCore : ILoggerCore
        {
            private LoggingHistory _history = null;

            public EventLoggerCore()
            {
                _history = new LoggingHistory();
                LogUpdatedEvent = new AsyncEvent<LogUpdatedEventArgs>();
            }

            public ILoggingHistory GetLoggingHistory()
            {
                return _history;
            }

            public void LoggerImplementation(PooledLogEvent value)
            {
                // OPT this sucks but I don't see how else we can emit pooled buffers as messages to many listeners
                // Also, storing pooled objects into a log history and expecting users to properly manage ownership is not going to happen
                LogEvent convertedBackToAnEvent = value.ToLogEvent();
                value.Dispose();

                _history.Add(convertedBackToAnEvent);

                if (LogUpdatedEvent.HasSubscribers)
                {
                    DurandalTaskExtensions.LossyThreadPool.EnqueueUserWorkItem(() => OnLogUpdated(convertedBackToAnEvent)); // fixme remove the lambda
                }
            }

            public AsyncEvent<LogUpdatedEventArgs> LogUpdatedEvent { get; private set; }

            private void OnLogUpdated(LogEvent e)
            {
                LogUpdatedEventArgs args = new LogUpdatedEventArgs(e);
                LogUpdatedEvent.FireInBackground(this, args, NullLogger.Singleton, DefaultRealTimeProvider.Singleton);
            }

            public void Dispose() { }

            public Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking)
            {
                return DurandalTaskExtensions.NoOpTask;
            }
        }
    }
}
