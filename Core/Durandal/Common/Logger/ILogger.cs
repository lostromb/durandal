namespace Durandal.Common.Logger
{
    using Durandal.API;
    using Durandal.Common.Time;
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Utils;

    /// <summary>
    /// Represents a logging system based on immutable logger instances which are cloned and passed around into functions.
    /// Each "shell" logger typically wraps around a hidden "core" object. The "shell" contains immutable context such as
    /// the current trace ID or allowable log levels, and these are used to automatically decorate messages which are
    /// dispatched to the core.
    /// By cloning lightweight logger shells and passing them through functions, you can easily implement trace logging that carries
    /// the context forward in a platform- and thread-agnostic way. You can also implement your own core for adapting messages to other
    /// systems such as NLog or Microsoft.Extensions.Logging
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Creates a clone of this logger with a new trace ID
        /// but which is directed to the same underlying stream. Each program component will then
        /// only have to manage its own local clone of the logger.
        /// The returned object MUST be a unique immutable logger instance, it cannot simply "return this;"
        /// </summary>
        /// <param name="traceId">The trace ID to set for the new instance.</param>
        /// <param name="newComponentName">The new component ID to use for the clone (such as the classname that uses it). If null, the name will remain unchanged</param>
        /// <returns>A new logger instance</returns>
        ILogger CreateTraceLogger(Guid? traceId, string newComponentName = null);

        /// <summary>
        /// Creates a clone of this logger with new parameters.
        /// The returned object MUST be a unique immutable logger instance, it cannot simply "return this;"
        /// </summary>
        /// <param name="newComponentName">The new component ID to use for the clone (such as the classname that uses it). If null, the clone will receive the parent's component name</param>
        /// <param name="allowedLogLevels">The maximum log level that the clone will support.</param>
        /// <param name="defaultPrivacyClass">The default privacy class to use for messages logged by the clone</param>
        /// <param name="allowedPrivacyClasses">A bit field that specifies the privacy classes which can be written by the clone</param>
        /// <returns>A new logger instance</returns>
        ILogger Clone(
            string newComponentName = null,
            LogLevel? allowedLogLevels = null,
            DataPrivacyClassification? defaultPrivacyClass = null,
            DataPrivacyClassification? allowedPrivacyClasses = null);

        /// <summary>
        /// Logs an object (upon which the default ToString() will be invoked)
        /// </summary>
        /// <param name="value">The object to log</param>
        /// <param name="level">The log level of this message</param>
        /// <param name="traceId">The trace ID. If null, the logger's current default trace ID will be used</param>
        /// <param name="privacyClass">The privacy classification of the logged message</param>
        /// <param name="timestamp">The override timestamp to set on the event, if you want it to be something other than the current time (for example, if you are recreating historical log data)</param>
        void Log(
            object value,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null);

        /// <summary>
        /// Logs a string message
        /// </summary>
        /// <param name="message">The string to log</param>
        /// <param name="level">The importance level</param>
        /// <param name="traceId">The trace ID. If null, the logger's current default trace ID will be used</param>
        /// <param name="privacyClass">The privacy classification of the logged message</param>
        /// <param name="timestamp">The override timestamp to set on the event, if you want it to be something other than the current time (for example, if you are recreating historical log data)</param>
        void Log(
            string message,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null);

        /// <summary>
        /// Logs a string message contained within a StringBuilder
        /// </summary>
        /// <param name="messageBuilder">The StringBuilder containing the string to log</param>
        /// <param name="level">The importance level</param>
        /// <param name="traceId">The trace ID. If null, the logger's current default trace ID will be used</param>
        /// <param name="privacyClass">The privacy classification of the logged message</param>
        /// <param name="timestamp">The override timestamp to set on the event, if you want it to be something other than the current time (for example, if you are recreating historical log data)</param>
        void Log(
            StringBuilder messageBuilder,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null);

        /// <summary>
        /// Logs an exception
        /// </summary>
        /// <param name="exception">The exception to log</param>
        /// <param name="level">The level to log it at - default is "Error"</param>
        /// <param name="traceId">The trace ID. If null, the logger's current default trace ID will be used</param>
        /// <param name="privacyClass">The privacy classification of the logged message</param>
        /// <param name="timestamp">The override timestamp to set on the event, if you want it to be something other than the current time (for example, if you are recreating historical log data)</param>
        void Log(
            Exception exception,
            LogLevel level = LogLevel.Err,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null);

        /// <summary>
        /// Logs the result of a conditional function which is run asynchronously (useful for logging large messages without incurring much overhead).
        /// Note that the timestamp of the produced message will be the time of the Log() statement, not the time that the actual value is produced.
        /// </summary>
        /// <param name="producer">A function that produces a string</param>
        /// <param name="level">The log level of this message</param>
        /// <param name="traceId">The trace ID. If null, the logger's current default trace ID will be used</param>
        /// <param name="privacyClass">The privacy classification of the logged message</param>
        /// <param name="timestamp">The override timestamp to set on the event, if you want it to be something other than the current time (for example, if you are recreating historical log data)</param>
        void Log(
            Func<string> producer,
            LogLevel level = LogLevel.Std,
            Guid? traceId = null,
            DataPrivacyClassification privacyClass = DataPrivacyClassification.Unknown,
            DateTimeOffset? timestamp = null);

        /// <summary>
        /// Logs a message using a format string and args. This is typically the most efficient way of logging as it can
        /// potentially format directly to a pooled buffer, or conditionally skip the formatting based on log level or other filters.
        /// </summary>
        /// <param name="level">The log level of the message</param>
        /// <param name="privacyClass">The privacy class of the message</param>
        /// <param name="formatString">The format string to use</param>
        /// <param name="arg0">The first argument</param>
        void LogFormat<T0>(LogLevel level, DataPrivacyClassification privacyClass, string formatString, T0 arg0);

        /// <summary>
        /// Logs a message using a format string and args. This is typically the most efficient way of logging as it can
        /// potentially format directly to a pooled buffer, or conditionally skip the formatting based on log level or other filters.
        /// </summary>
        /// <param name="level">The log level of the message</param>
        /// <param name="privacyClass">The privacy class of the message</param>
        /// <param name="formatString">The format string to use</param>
        /// <param name="arg0">The first argument</param>
        /// <param name="arg1">The second argument</param>
        void LogFormat<T0, T1>(LogLevel level, DataPrivacyClassification privacyClass, string formatString, T0 arg0, T1 arg1);

        /// <summary>
        /// Logs a message using a format string and args. This is typically the most efficient way of logging as it can
        /// potentially format directly to a pooled buffer, or conditionally skip the formatting based on log level or other filters.
        /// </summary>
        /// <param name="level">The log level of the message</param>
        /// <param name="privacyClass">The privacy class of the message</param>
        /// <param name="formatString">The format string to use</param>
        /// <param name="arg0">The first argument</param>
        /// <param name="arg1">The second argument</param>
        /// <param name="arg2">The third argument</param>
        void LogFormat<T0, T1, T2>(LogLevel level, DataPrivacyClassification privacyClass, string formatString, T0 arg0, T1 arg1, T2 arg2);

        /// <summary>
        /// Logs a message using a format string and args. This is typically the most efficient way of logging as it can
        /// potentially format directly to a pooled buffer, or conditionally skip the formatting based on log level or other filters.
        /// </summary>
        /// <param name="level">The log level of the message</param>
        /// <param name="privacyClass">The privacy class of the message</param>
        /// <param name="formatString">The format string to use</param>
        /// <param name="args">The list of arguments to pass to the formatter</param>
        void LogFormat(LogLevel level, DataPrivacyClassification privacyClass, string formatString, params object[] args);

        /// <summary>
        /// Advanced logging function which enqueues a work item on the logger's async thread pool which can then log one or more messages asynchronously.
        /// Callers of this method should pass in a closure method that will eventually receive a logger to which messages can be written out of the hot path.
        /// This closure should just do Log() synchronously; there's no need for it to do further delegated work. The second argument to the delegate
        /// is the timestamp at which this method was originally called - this can be used as the timestamp for future log events to help merged logs line up better.
        /// </summary>
        /// <param name="processorDelegate">A delegate method that accepts this logger asynchronously, to which messages can be written asynchronously.</param>
        void DispatchAsync(Action<ILogger, DateTimeOffset> processorDelegate);

        /// <summary>
        /// Logs a low-level event
        /// </summary>
        /// <param name="value">The event to write.</param>
        void Log(LogEvent value);

        /// <summary>
        /// Logs a low-level pooled event
        /// </summary>
        /// <param name="value">The event to write.</param>
        void Log(PooledLogEvent value);

        /// <summary>
        /// Returns the component name which owns this instance of the logger
        /// </summary>
        /// <returns></returns>
        string ComponentName { get; }

        /// <summary>
        /// Gets the current TraceId
        /// </summary>
        /// <returns></returns>
        Guid? TraceId { get; }
        
        /// <summary>
        /// Specifies the bitfield of log levels that this logger will process. All levels that do not match the bitfield will be ignored.
        /// </summary>
        LogLevel ValidLogLevels { get; }

        /// <summary>
        /// The default privacy class for messages that have class of Unknown
        /// </summary>
        DataPrivacyClassification DefaultPrivacyClass { get; }

        DataPrivacyClassification ValidPrivacyClasses { get; }

        /// <summary>
        /// Attempts to flush all output from this logger
        /// </summary>
        Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking = false);
    }
}