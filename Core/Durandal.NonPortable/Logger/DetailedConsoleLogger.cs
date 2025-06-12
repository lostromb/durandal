namespace Durandal.Common.Logger
{
    using Durandal.API;
    using Instrumentation;
    using System;
    using System.Text;
    using System.Threading;
    using Durandal.Common.Tasks;
    using Durandal.Common.Utils;
    using System.IO;
    using Durandal.Common.IO;

    /// <summary>
    /// Logger which writes to System.Console
    /// </summary>
    public class DetailedConsoleLogger : StdOutLoggerBase
    {
        public DetailedConsoleLogger(
            string componentName = "Main",
            IThreadPool backgroundLogThreadPool = null,
            DataPrivacyClassification defaultPrivacyClass = DataPrivacyClassification.SystemMetadata)
            : base(
                  backgroundLogThreadPool,
                  componentName,
                  validLogLevels: LogLevel.All,
                  maxLogLevels: LogLevel.All,
                  maxPrivacyClasses: DataPrivacyClassification.All,
                  defaultPrivacyClass: defaultPrivacyClass)
        {
            Console.WriteLine("Detailed console logger initialized");
        }

        protected override void LogToOutputImpl(PooledLogEvent value)
        {
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
                StringUtils.FormatDateTime_ISO8601WithMicroseconds(value.Timestamp.ToLocalTime(), buffer);
                buffer.Append("] [");
                buffer.Append(value.Level.ToChar());
                buffer.Append(':');
                buffer.Append(value.Component);
                buffer.Append(':');
                if (string.IsNullOrEmpty(Thread.CurrentThread.Name))
                {
                    buffer.Append("Thread ");
                    buffer.Append(Thread.CurrentThread.ManagedThreadId);
                    buffer.Append("] [");
                }
                else
                {
                    buffer.Append(Thread.CurrentThread.Name);
                    buffer.Append('(');
                    buffer.Append(Thread.CurrentThread.ManagedThreadId);
                    buffer.Append(")] [");
                }

                CommonInstrumentation.WritePrivacyClassification(value.PrivacyClassification, buffer);
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
                }

                value.Dispose();
            }
        }
    }
}
