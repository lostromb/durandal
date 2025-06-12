using Durandal.API;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Logger
{
    /// <summary>
    /// Provides common helper infrastructure for system loggers
    /// </summary>
    public abstract class LoggerBase : ILogger
    {
        public const LogLevel DEFAULT_LOG_LEVELS = LogLevel.Std | LogLevel.Err | LogLevel.Wrn | LogLevel.Ins | LogLevel.Crt;

        private const int MAX_BACKGROUND_LOGGING_TASKS = 8;

        public static readonly IThreadPool DEFAULT_BACKGROUND_LOGGING_THREAD_POOL =
            new FixedCapacityThreadPool(
                new TaskThreadPool(NullMetricCollector.WeakSingleton, DimensionSet.Empty, "DefaultLoggerAsync"),
                NullLogger.Singleton,
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                "DefaultLoggerAsync",
                MAX_BACKGROUND_LOGGING_TASKS,
                ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable);

        protected ILoggerCore _core;
        protected LoggerContext _context;

        protected ILoggerCore Core
        {
            get
            {
                return _core;
            }
        }

        /// <summary>
        /// Initializes the abstract base of a logger implementation
        /// </summary>
        /// <param name="core"></param>
        /// <param name="context"></param>
        protected LoggerBase(ILoggerCore core, LoggerContext context)
        {
            _core = core;
            _context = context;
        }

        /// <inheritdoc/>
        public string ComponentName => _context.ComponentName;

        /// <inheritdoc/>
        public Guid? TraceId => _context.TraceId;

        /// <inheritdoc/>
        public LogLevel ValidLogLevels => _context.ValidLogLevels & _context.MaxLogLevels;

        /// <inheritdoc/>
        public DataPrivacyClassification DefaultPrivacyClass => _context.DefaultPrivacyClass;

        /// <inheritdoc/>
        public DataPrivacyClassification ValidPrivacyClasses => _context.ValidPrivacyClasses & _context.MaxPrivacyClasses;

        protected abstract ILogger CloneImplementation(ILoggerCore core, LoggerContext clonedContext);

        /// <inheritdoc/>
        public ILogger CreateTraceLogger(Guid? traceId, string newComponentName = null)
        {
            newComponentName = string.IsNullOrEmpty(newComponentName) ? _context.ComponentName : newComponentName;
            return CloneImplementation(
                _core,
                _context.Clone(
                    newComponentName,
                    traceId,
                    _context.ValidLogLevels,
                    _context.DefaultPrivacyClass,
                    _context.ValidPrivacyClasses));
        }

        /// <inheritdoc/>
        public ILogger Clone(
            string newComponentName = null,
            LogLevel? allowedLogLevels = null,
            DataPrivacyClassification? defaultPrivacyClass = null,
            DataPrivacyClassification? allowedPrivacyClasses = null)
        {
            string actualComponent = string.IsNullOrEmpty(newComponentName) ? _context.ComponentName : newComponentName;
            LogLevel actualValidLogLevels = allowedLogLevels.GetValueOrDefault(_context.ValidLogLevels);
            DataPrivacyClassification actualDefaultPrivacyClass = defaultPrivacyClass.GetValueOrDefault(_context.DefaultPrivacyClass);
            DataPrivacyClassification actualValidPrivacyClasses = allowedPrivacyClasses.GetValueOrDefault(_context.ValidPrivacyClasses);
            return CloneImplementation(
                _core,
                _context.Clone(
                    actualComponent,
                    _context.TraceId,
                    actualValidLogLevels,
                    actualDefaultPrivacyClass,
                    actualValidPrivacyClasses));
        }

        /// <summary>
        /// All log methods should funnel into this method after filtering.
        /// This method will apply PII encryption if needed and then dispatch to the core
        /// </summary>
        /// <param name="value"></param>
        private void LogInternal(PooledLogEvent value)
        {
            _core.LoggerImplementation(value);

            if (value.Level == LogLevel.Crt)
            {
                // Flush critical messages immediately on the assumption that the program is about to implode
                _core.Flush(CancellationToken.None, DefaultRealTimeProvider.Singleton, true).Await();
            }
        }

        /// <inheritdoc/>
        public void Log(LogEvent value)
        {
            if (value == null)
            {
                return;
            }

            // Determine if the logger should even handle this message
            if ((ValidLogLevels & value.Level) == 0)
            {
                return;
            }

            if (value.PrivacyClassification == DataPrivacyClassification.Unknown)
            {
                value.PrivacyClassification = _context.DefaultPrivacyClass;
            }

            // abort logging if the message contains a privacy class that we are not allowed to output
            if ((value.PrivacyClassification & ~(_context.ValidPrivacyClasses & _context.MaxPrivacyClasses)) != 0)
            {
                return;
            }

            LogInternal(PooledLogEvent.FromLogEvent(value));
        }

        /// <inheritdoc/>
        public void Log(PooledLogEvent value)
        {
            if (value == null)
            {
                return;
            }

            // Determine if the logger should even handle this message
            if ((ValidLogLevels & value.Level) == 0)
            {
                value.Dispose();
                return;
            }

            if (value.PrivacyClassification == DataPrivacyClassification.Unknown)
            {
                value.PrivacyClassification = _context.DefaultPrivacyClass;
            }

            // abort logging if the message contains a privacy class that we are not allowed to output
            if ((value.PrivacyClassification & ~(_context.ValidPrivacyClasses & _context.MaxPrivacyClasses)) != 0)
            {
                value.Dispose();
                return;
            }

            LogInternal(value);
        }

        /// <inheritdoc/>
        public virtual void Log(
            string message,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null)
        {
            // Determine if the logger should even handle this message
            if ((ValidLogLevels & level) == 0)
            {
                return;
            }

            // Determine its PII class
            DataPrivacyClassification actualPrivacyClass = privacyClass == DataPrivacyClassification.Unknown ? DefaultPrivacyClass : privacyClass;

            // Abort logging if the message contains a privacy class that we are not allowed to output
            if ((actualPrivacyClass & ~(_context.ValidPrivacyClasses & _context.MaxPrivacyClasses)) != 0)
            {
                return;
            }

            // Convert it into an event
            PooledLogEvent newEvent = PooledLogEvent.Create(ComponentName, message ?? "null", level, timestamp.GetValueOrDefault(GetHighResolutionTime()), traceId ?? TraceId, actualPrivacyClass);
            LogInternal(newEvent);
        }

        /// <inheritdoc/>
        public virtual void Log(
            StringBuilder value,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null)
        {
            // Determine if the logger should even handle this message
            if ((ValidLogLevels & level) == 0)
            {
                return;
            }

            // Determine its PII class
            DataPrivacyClassification actualPrivacyClass = privacyClass == DataPrivacyClassification.Unknown ? DefaultPrivacyClass : privacyClass;

            // Abort logging if the message contains a privacy class that we are not allowed to output
            if ((actualPrivacyClass & ~(_context.ValidPrivacyClasses & _context.MaxPrivacyClasses)) != 0)
            {
                return;
            }

            // Get a PooledStringBuilder and copy the input string builder into it.
            // The log event then takes ownership of that buffer.
            PooledStringBuilder messageBuffer = StringBuilderPool.Rent();
            StringUtils.CopyAcross(value, messageBuffer.Builder);
            PooledLogEvent newEvent = PooledLogEvent.Create(ComponentName, messageBuffer, level, timestamp.GetValueOrDefault(GetHighResolutionTime()), traceId ?? TraceId, actualPrivacyClass);
            LogInternal(newEvent);
        }

        /// <inheritdoc/>
        public virtual void Log(
            object value,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null)
        {
            // Determine if the logger should even handle this message
            if ((ValidLogLevels & level) == 0)
            {
                return;
            }

            // Determine its PII class
            DataPrivacyClassification actualPrivacyClass = privacyClass == DataPrivacyClassification.Unknown ? DefaultPrivacyClass : privacyClass;

            // Abort logging if the message contains a privacy class that we are not allowed to output
            if ((actualPrivacyClass & ~(_context.ValidPrivacyClasses & _context.MaxPrivacyClasses)) != 0)
            {
                return;
            }

            // Convert it into an event
            string actualLogMessage = value == null ? "null" : value.ToString();
            PooledLogEvent newEvent = PooledLogEvent.Create(ComponentName, actualLogMessage, level, timestamp.GetValueOrDefault(GetHighResolutionTime()), traceId ?? TraceId, actualPrivacyClass);
            LogInternal(newEvent);
        }

        /// <inheritdoc/>
        public virtual void LogFormat<T0>(
            LogLevel level,
            DataPrivacyClassification privacyClass,
            string formatString,
            T0 arg0)
        {
            // Determine if the logger should even handle this message
            if ((ValidLogLevels & level) == 0)
            {
                return;
            }

            // Determine its PII class
            DataPrivacyClassification actualPrivacyClass = privacyClass == DataPrivacyClassification.Unknown ? DefaultPrivacyClass : privacyClass;

            // Abort logging if the message contains a privacy class that we are not allowed to output
            if ((actualPrivacyClass & ~(_context.ValidPrivacyClasses & _context.MaxPrivacyClasses)) != 0)
            {
                return;
            }

            // Convert it into an event
            PooledStringBuilder buffer = StringBuilderPool.Rent();
            buffer.Builder.AppendFormat(formatString, arg0);
            PooledLogEvent newEvent = PooledLogEvent.Create(ComponentName, buffer, level, GetHighResolutionTime(), TraceId, actualPrivacyClass);
            LogInternal(newEvent);
        }

        /// <inheritdoc/>
        public virtual void LogFormat<T0, T1>(
            LogLevel level,
            DataPrivacyClassification privacyClass,
            string formatString,
            T0 arg0,
            T1 arg1)
        {
            // Determine if the logger should even handle this message
            if ((ValidLogLevels & level) == 0)
            {
                return;
            }

            // Determine its PII class
            DataPrivacyClassification actualPrivacyClass = privacyClass == DataPrivacyClassification.Unknown ? DefaultPrivacyClass : privacyClass;

            // Abort logging if the message contains a privacy class that we are not allowed to output
            if ((actualPrivacyClass & ~(_context.ValidPrivacyClasses & _context.MaxPrivacyClasses)) != 0)
            {
                return;
            }

            // Convert it into an event
            PooledStringBuilder buffer = StringBuilderPool.Rent();
            buffer.Builder.AppendFormat(formatString, arg0, arg1);
            PooledLogEvent newEvent = PooledLogEvent.Create(ComponentName, buffer, level, GetHighResolutionTime(), TraceId, actualPrivacyClass);
            LogInternal(newEvent);
        }

        /// <inheritdoc/>
        public virtual void LogFormat<T0, T1, T2>(
            LogLevel level,
            DataPrivacyClassification privacyClass,
            string formatString,
            T0 arg0,
            T1 arg1,
            T2 arg2)
        {
            // Determine if the logger should even handle this message
            if ((ValidLogLevels & level) == 0)
            {
                return;
            }

            // Determine its PII class
            DataPrivacyClassification actualPrivacyClass = privacyClass == DataPrivacyClassification.Unknown ? DefaultPrivacyClass : privacyClass;

            // Abort logging if the message contains a privacy class that we are not allowed to output
            if ((actualPrivacyClass & ~(_context.ValidPrivacyClasses & _context.MaxPrivacyClasses)) != 0)
            {
                return;
            }

            // Convert it into an event
            PooledStringBuilder buffer = StringBuilderPool.Rent();
            buffer.Builder.AppendFormat(formatString, arg0, arg1, arg2);
            PooledLogEvent newEvent = PooledLogEvent.Create(ComponentName, buffer, level, GetHighResolutionTime(), TraceId, actualPrivacyClass);
            LogInternal(newEvent);
        }

        /// <inheritdoc/>
        public virtual void LogFormat(
            LogLevel level,
            DataPrivacyClassification privacyClass,
            string formatString,
            params object[] args)
        {
            // Determine if the logger should even handle this message
            if ((ValidLogLevels & level) == 0)
            {
                return;
            }

            // Determine its PII class
            DataPrivacyClassification actualPrivacyClass = privacyClass == DataPrivacyClassification.Unknown ? DefaultPrivacyClass : privacyClass;

            // Abort logging if the message contains a privacy class that we are not allowed to output
            if ((actualPrivacyClass & ~(_context.ValidPrivacyClasses & _context.MaxPrivacyClasses)) != 0)
            {
                return;
            }

            // Convert it into an event
            PooledStringBuilder buffer = StringBuilderPool.Rent();
            buffer.Builder.AppendFormat(formatString, args);
            PooledLogEvent newEvent = PooledLogEvent.Create(ComponentName, buffer, level, GetHighResolutionTime(), TraceId, actualPrivacyClass);
            LogInternal(newEvent);
        }

        /// <summary>
        /// Standard implementation of exception logger. Marked virtual in case the logger implementation can provide a better solution
        /// </summary>
        /// <param name="value"></param>
        /// <param name="level"></param>
        /// <param name="traceId"></param>
        /// <param name="privacyClass"></param>
        /// <param name="timestamp"></param>
        public virtual void Log(
            Exception value,
            LogLevel level = LogLevel.Err,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null)
        {
            try
            {
                if (value is null)
                {
                    throw new ArgumentNullException("Null exception passed to error logger");
                }
            }
            catch (Exception e)
            {
                // Meta-error logging: If error being logged is null, log an error to say so (we still want the stack trace in this case)
                Log(e, level, traceId, privacyClass, timestamp);
                return;
            }

            if ((ValidLogLevels & level) == 0)
            {
                return;
            }

            // Unwind the stack of inner exceptions to a max depth of 4
            timestamp = timestamp ?? GetHighResolutionTime();
            DataPrivacyClassification actualPrivacyClass = privacyClass == DataPrivacyClassification.Unknown ? DefaultPrivacyClass : privacyClass;
            using (PooledBuffer<Exception> nestedExceptions = BufferPool<Exception>.Rent(4))
            {
                nestedExceptions.Buffer[0] = value;
                Exception inner = value.InnerException;
                int nestCount = 1;
                if (traceId == null)
                {
                    traceId = TraceId;
                }

                while (inner != null && nestCount < nestedExceptions.Length)
                {
                    nestedExceptions.Buffer[nestCount++] = inner;
                    inner = inner.InnerException;
                }

                // Now iterate through in reverse order, starting at the innermost exception,
                // and log each exception's message and stack trace
                for (int c = nestCount - 1; c >= 0; c--)
                {
                    Exception thisException = nestedExceptions.Buffer[c];

                    if ((actualPrivacyClass & (_context.ValidPrivacyClasses & _context.MaxPrivacyClasses)) != 0)
                    {
                        // We are allowed to emit the error message of this privacy class; convert it to a pooled event
                        PooledStringBuilder buffer = StringBuilderPool.Rent();
                        buffer.Builder.Append(thisException.GetType().Name);
                        buffer.Builder.Append(": ");
                        buffer.Builder.Append(thisException.Message);
                        PooledLogEvent newEvent = PooledLogEvent.Create(ComponentName, buffer, level, timestamp.Value, traceId, actualPrivacyClass);
                        LogInternal(newEvent);
                    }

                    // The stack trace always gets treated as SystemMetadata no matter what privacy setting was given so we can still get some debug info even if there is PII in the error message for some reason
                    PooledStringBuilder pooledSb = StringBuilderPool.Rent();
                    thisException.PrintStackTraceToStringBuilder(pooledSb.Builder);
                    if (pooledSb.Builder.Length > 0)
                    {
                        LogInternal(
                            PooledLogEvent.Create(
                                ComponentName,
                                pooledSb,
                                level,
                                timestamp.Value,
                                traceId,
                                DataPrivacyClassification.SystemMetadata));
                    }
                    else
                    {
                        pooledSb.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Basic implementation of deferred logging. Execution will either be run synchronously (if the thread pool is null) or will run on the background thread pool specified at logger creation time.
        /// </summary>
        /// <param name="producer">A function which, if invoked, will produce a log message</param>
        /// <param name="level">The log level of the message to be logged</param>
        /// <param name="traceId">The trace ID of the message to be logged</param>
        /// <param name="privacyClass">The data privacy classification of the message</param>
        /// <param name="timestamp"></param>
        public virtual void Log(
            Func<string> producer,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null)
        {
            if ((ValidLogLevels & level) == 0)
            {
                return;
            }

            DateTimeOffset logEventTime = timestamp.GetValueOrDefault(GetHighResolutionTime());
            Guid? realTraceId = traceId ?? TraceId;
            string realComponentName = ComponentName;
            DataPrivacyClassification actualPrivacyClass = privacyClass == DataPrivacyClassification.Unknown ? DefaultPrivacyClass : privacyClass;

            // Abort logging if the message contains a privacy class that we are not allowed to output
            if ((actualPrivacyClass & ~(_context.ValidPrivacyClasses & _context.MaxPrivacyClasses)) != 0)
            {
                return;
            }

            if (level == LogLevel.Crt ||
                _context.BackgroundLoggingThreadPool == null)
            {
                // Critical log message, or no async thread pool - do logging on current thread
                string actualLogMessage = producer();
                Log(new LogEvent(realComponentName, actualLogMessage, level, logEventTime, realTraceId, actualPrivacyClass));
            }
            else
            {
                _context.BackgroundLoggingThreadPool.EnqueueUserWorkItem(() =>
                {
                    string actualLogMessage = producer();
                    Log(new LogEvent(realComponentName, actualLogMessage, level, logEventTime, realTraceId, actualPrivacyClass));
                });
            }
        }

        /// <inheritdoc/>
        public virtual void DispatchAsync(Action<ILogger, DateTimeOffset> processorDelegate)
        {
            DateTimeOffset logEventTime = GetHighResolutionTime();
            
            if (_context.BackgroundLoggingThreadPool == null)
            {
                // No async thread pool - do logging on current thread
                processorDelegate(this, logEventTime);
            }
            else
            {
                _context.BackgroundLoggingThreadPool.EnqueueUserWorkItem(() =>
                {
                    processorDelegate(this, logEventTime);
                });
            }
        }

        /// <summary>
        /// Waits for background (asynchronous) log events to finish processing before continuing.
        /// Subclasses of this class should do base.Flush() in their overrides.
        /// </summary>
        /// <param name="cancellizer"></param>
        /// <param name="realTime"></param>
        /// <param name="blocking"></param>
        public virtual async Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking = false)
        {
            if (blocking && _context.BackgroundLoggingThreadPool != null)
            {
                await _context.BackgroundLoggingThreadPool.WaitForCurrentTasksToFinish(cancellizer, realTime).ConfigureAwait(false);
            }

            await _core.Flush(cancellizer, realTime, blocking).ConfigureAwait(false);
        }

        protected DateTimeOffset GetHighResolutionTime()
        {
            return HighPrecisionTimer.GetCurrentUTCTime();
        }
    }
}
