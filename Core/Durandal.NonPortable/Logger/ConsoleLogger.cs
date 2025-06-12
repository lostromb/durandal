namespace Durandal.Common.Logger
{
    using Durandal.API;
    using Durandal.Common.Instrumentation;
    using System;
    using System.Text;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using Durandal.Common.IO;
    using Durandal.Common.Time;
    using Durandal.Common.MathExt;
    using System.Diagnostics;

    /// <summary>
    /// Logger which writes to System.Console
    /// </summary>
    public class ConsoleLogger : StdOutLoggerBase
    {
        // this dictates the maximum rate console messages can be written when throttled
        private const long BACKOFF_TIME_TICKS = 10 /*ms*/ * TimeSpan.TicksPerMillisecond;
        private readonly MovingAverage _averageMsSpentLogging = new MovingAverage(1000, 0.0);
        private readonly MovingAverage _averageMsBetweenLogInvocations = new MovingAverage(1000, 1000.0);
        private readonly double _maxThreadUse;
        private long _lastLogTime = HighPrecisionTimer.GetCurrentTicks();
        private long _backoffTime = 0; // If backing off, don't log anything until the time reaches this tick count

        /// <summary>
        /// Constructs a new <see cref="ConsoleLogger"/>.
        /// </summary>
        /// <param name="componentName">The root component name of this logger.</param>
        /// <param name="maxLevels">The maximum allowable log levels to output.</param>
        /// <param name="backgroundLogThreadPool">A thread pool for background dispatch tasks.</param>
        /// <param name="maxPrivacyClasses">The maximum allowable privacy classes to output.</param>
        /// <param name="defaultPrivacyClass">The default privacy class for messages that are untagged.</param>
        /// <param name="maxThreadUse">A ratio representing the maximum amount of a single CPU's thread you want to devote to console updates.
        /// By default, the console will block as all messages are written. Lowering this ratio to between 0.0 and 1.0 will cause logs to be dropped
        /// if they come in at too high of a rate, which helps prevent slowdown in your application</param>
        public ConsoleLogger(
            string componentName = "Main",
            LogLevel maxLevels = DEFAULT_LOG_LEVELS,
            IThreadPool backgroundLogThreadPool = null,
            DataPrivacyClassification maxPrivacyClasses = DataPrivacyClassification.All,
            DataPrivacyClassification defaultPrivacyClass = DataPrivacyClassification.SystemMetadata,
            double maxThreadUse = 1.5)
            : base(
                  backgroundLogThreadPool,
                  componentName,
                  validLogLevels: maxLevels,
                  maxLogLevels: maxLevels,
                  maxPrivacyClasses: maxPrivacyClasses,
                  defaultPrivacyClass: defaultPrivacyClass)
        {
            _maxThreadUse = maxThreadUse.AssertNonNegative(nameof(maxThreadUse));
            Console.WriteLine("Console logger initialized");
        }

        protected override void LogToOutputImpl(PooledLogEvent value)
        {
            long logStartTime = HighPrecisionTimer.GetCurrentTicks();
            if (logStartTime < _backoffTime)
            {
                // we're backing off now...
                value.Dispose();
                return;
            }

            ValueStopwatch stopwatch = ValueStopwatch.StartNew();
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            using (PooledBuffer<char> scratch = BufferPool<char>.Rent())
            {
                StringBuilder buffer = pooledSb.Builder;
                if (value.TraceId.HasValue)
                {
                    buffer.Append('[');
                    CommonInstrumentation.GetFirst3DigitsOfTraceId(value.TraceId.Value, buffer);
                    buffer.Append("] ");
                }

                buffer.Append('[');
                StringUtils.FormatTime_ISO8601(value.Timestamp.ToLocalTime(), buffer);
                buffer.Append("] [");

                buffer.Append(value.Level.ToChar());
                buffer.Append(':');
                buffer.Append(value.Component);
                buffer.Append("] ");

                lock (Console.Out)
                {
                    if ((value.PrivacyClassification & (DataPrivacyClassification.EndUserIdentifiableInformation | DataPrivacyClassification.PrivateContent)) != 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                    }
                    else if ((value.Level & LogLevel.Err) != 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    else if ((value.Level & LogLevel.Wrn) != 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    else if ((value.Level & LogLevel.Ins) != 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                    }
                    else if ((value.Level & LogLevel.Crt) != 0)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                    }

                    StringUtils.CopyStringBuilderToTextWriter(buffer, Console.Out, scratch.Buffer);
                    StringUtils.CopyStringBuilderToTextWriter(value.MessageBuffer.Builder, Console.Out, scratch.Buffer);

                    Console.WriteLine();
                    Console.ResetColor();
                    stopwatch.Stop();

                    // Decide if we need to back off
                    _averageMsSpentLogging.Add(stopwatch.ElapsedMillisecondsPrecise());
                    _averageMsBetweenLogInvocations.Add((logStartTime - _lastLogTime) / (double)TimeSpan.TicksPerMillisecond);
                    if (_averageMsSpentLogging.Average / _averageMsBetweenLogInvocations.Average > _maxThreadUse)
                    {
                        _backoffTime = logStartTime + BACKOFF_TIME_TICKS;
                    }

                    _lastLogTime = logStartTime;
                }

                value.Dispose();
            }
        }
    }
}
