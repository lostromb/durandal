namespace Durandal.Common.Logger
{
    using Durandal.API;
    using System;
    using System.Diagnostics;
    using Durandal.Common.Tasks;
    using System.Text;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Utils;
    using Durandal.Common.MathExt;
    using Durandal.Common.IO;

    /// <summary>
    /// Logger which writes to System.Diagnostics.Trace
    /// </summary>
    public class TraceLogger : StdOutLoggerBase
    {
        public TraceLogger(
            string componentName = "Main",
            LogLevel maxLevel = DEFAULT_LOG_LEVELS,
            IThreadPool backgroundLogThreadPool = null,
            DataPrivacyClassification maxPrivacyClasses = DataPrivacyClassification.All,
            DataPrivacyClassification defaultPrivacyClass = DataPrivacyClassification.SystemMetadata)
            : base(
                  backgroundLogThreadPool,
                  componentName,
                  validLogLevels: maxLevel,
                  maxLogLevels: maxLevel,
                  maxPrivacyClasses: maxPrivacyClasses,
                  defaultPrivacyClass: defaultPrivacyClass)
        {
            Trace.TraceInformation("Trace logger initialized");
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
                StringUtils.FormatTime_ISO8601(value.Timestamp.ToLocalTime(), buffer);
                buffer.Append("] [");

                buffer.Append(value.Level.ToChar());
                buffer.Append(':');
                buffer.Append(value.Component);
                buffer.Append("] ");

                int inIdx = 0;
                while (inIdx < value.MessageBuffer.Builder.Length)
                {
                    int canCopy = FastMath.Min(scratch.Buffer.Length, value.MessageBuffer.Builder.Length - inIdx);
                    value.MessageBuffer.Builder.CopyTo(inIdx, scratch.Buffer, 0, canCopy);
                    inIdx += canCopy;
                    buffer.Append(scratch.Buffer, 0, canCopy);
                }

                // Wish we didn't have to use StringBuilder.ToString() here. Oh well, we tried our best.
                if (value.Level == LogLevel.Err || value.Level == LogLevel.Crt)
                {
                    Trace.TraceError(buffer.ToString());
                }
                else if (value.Level == LogLevel.Wrn)
                {
                    Trace.TraceWarning(buffer.ToString());
                }
                else
                {
                    Trace.TraceInformation(buffer.ToString());
                }

                value.Dispose();
            }
        }
    }
}
