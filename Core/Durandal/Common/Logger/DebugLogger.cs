namespace Durandal.Common.Logger
{
    using Durandal.API;
    using System;
    using System.Diagnostics;
    using Durandal.Common.Tasks;
    using Durandal.Common.Instrumentation;
    using System.Text;
    using Durandal.Common.Utils;
    using Durandal.Common.MathExt;
    using Durandal.Common.IO;

    /// <summary>
    /// Logger which writes to System.Diagnostics.Debug
    /// </summary>
    public class DebugLogger : StdOutLoggerBase
    {
        /// <summary>
        /// The default singleton debug logger.
        /// </summary>
        public static readonly DebugLogger Default = new DebugLogger(
            "Main",
#if DEBUG
            LogLevel.All,
#else
            LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Crt,
#endif
            backgroundLogThreadPool: null,
            maxPrivacyClasses: DataPrivacyClassification.All,
            defaultPrivacyClass: DataPrivacyClassification.SystemMetadata);

        public DebugLogger(
            string componentName = "Main",
            LogLevel maxLevel = DEFAULT_LOG_LEVELS,
            IThreadPool backgroundLogThreadPool = null,
            DataPrivacyClassification maxPrivacyClasses = DataPrivacyClassification.All,
            DataPrivacyClassification defaultPrivacyClass = DataPrivacyClassification.SystemMetadata)
            : base(backgroundLogThreadPool, 
                  componentName,
                  validLogLevels: maxLevel,
                  maxLogLevels: maxLevel,
                  maxPrivacyClasses: maxPrivacyClasses,
                  defaultPrivacyClass: defaultPrivacyClass)
        {
            Log("Debug logger initialized");
        }

        protected override void LogToOutputImpl(PooledLogEvent value)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
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

                StringUtils.CopyAcross(value.MessageBuffer.Builder, buffer);

                // Wish we didn't have to use StringBuilder.ToString() here. Oh well, we tried our best.
                Debug.WriteLine(buffer.ToString());

                value.Dispose();
            }
        }
    }
}
