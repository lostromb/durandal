using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.Common.Tasks;
using Durandal.API;
using Durandal.Common.Remoting.Protocol;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Instrumentation;
using Durandal.Common.Utils;
using Durandal.Common.Time;
using System.Diagnostics;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting.Proxies
{
    public class RemotedLogger : LoggerBase
    {
        public RemotedLogger(
            WeakPointer<RemoteDialogMethodDispatcher> dispatcher,
            IRealTimeProvider realTime,
            ILogger fallbackLogger,
            string componentName = "Main",
            LogLevel validLogLevels = DEFAULT_LOG_LEVELS)
            : base(new RemotedLoggerCore(dispatcher, validLogLevels, realTime, fallbackLogger),
                  new LoggerContext()
                  {
                      ComponentName = componentName,
                      TraceId = null,
                      ValidLogLevels = validLogLevels,
                      MaxLogLevels = LogLevel.All,
                      DefaultPrivacyClass = DataPrivacyClassification.SystemMetadata,
                      ValidPrivacyClasses = DataPrivacyClassification.All,
                      MaxPrivacyClasses = DataPrivacyClassification.All,
                      BackgroundLoggingThreadPool = null
                  })
        {
        }

        /// <summary>
        /// Private constructor for creating inherited logger objects
        /// </summary>
        /// <param name="core"></param>
        /// <param name="context"></param>
        private RemotedLogger(
            ILoggerCore core,
            LoggerContext context)
                : base(core, context)
        {
        }

        protected override ILogger CloneImplementation(
            ILoggerCore core,
            LoggerContext context)
        {
            return new RemotedLogger(core, context);
        }

        /// <summary>
        /// Housekeeping that's necessary because remoted loggers are often transient and tied to the lifetime of a post office mailbox
        /// </summary>
        public void DisposeOfCore()
        {
            this.Core.Dispose();
        }

        /// <summary>
        /// This is the context object shared between all clones of the console logger.
        /// We use a committer to batch multiple log events to a queue and then dump the entire queue to a serialized event list
        /// periodically in the background.
        /// </summary>
        private class RemotedLoggerCore : ILoggerCore
        {
            private readonly WeakPointer<RemoteDialogMethodDispatcher> _dispatcher;
            private readonly Queue<LogEvent> _events = new Queue<LogEvent>();
            private readonly LogLevel _levelsToLog = DEFAULT_LOG_LEVELS;
            private readonly ILogger _fallbackLogger;
            private int _disposed = 0;

            public RemotedLoggerCore(WeakPointer<RemoteDialogMethodDispatcher> dispatcher, LogLevel levels, IRealTimeProvider realTime, ILogger fallbackLogger)
            {
                _dispatcher = dispatcher;
                _levelsToLog = levels;
                _fallbackLogger = fallbackLogger ?? NullLogger.Singleton;
                DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
            }

#if TRACK_IDISPOSABLE_LEAKS
            ~RemotedLoggerCore()
            {
                Dispose(false);
            }
#endif

            public void LoggerImplementation(PooledLogEvent value)
            {
                // OPT this is a bit inefficient but we'll live with it
                LogEvent toEnqueue = value.ToLogEvent();

                lock(_events)
                {
                    _events.Enqueue(toEnqueue);
                }

                value.Dispose();
            }

            private async Task FlushEventsInternal(IRealTimeProvider realTime)
            {
                InstrumentationEventList eventList = new InstrumentationEventList();

                lock (_events)
                {
                    while (_events.Count > 0)
                    {
                        LogEvent e = _events.Dequeue();
                        eventList.Events.Add(InstrumentationEvent.FromLogEvent(e));
                    }
                }

                try
                {
                    await _dispatcher.Value.Logger_Log(eventList, realTime).ConfigureAwait(false);
                }

                catch (Exception e)
                {
                    // What can we do here? We evidently can't log the error without triggering an infinite loop.
                    // Best we can do is write to a bootstrap logger or debug console
                    _fallbackLogger.Log("Unhandled exception in remote logger", LogLevel.Err);
                    _fallbackLogger.Log(e, LogLevel.Err);
                }
            }

            public Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking)
            {
                return FlushEventsInternal(realTime);
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
                }
            }
        }
    }
}
