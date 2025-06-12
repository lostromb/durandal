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
    using Durandal.Common.Events;
    using System.Text;
    using Durandal.Common.Utils;

    public class PiiEncryptingLogger : LoggerBase
    {
        public PiiEncryptingLogger(
            ILogger downstreamLogger,
            IStringEncrypterPii piiEncrypter,
            LogLevel maxLogLevels = LogLevel.All,
            DataPrivacyClassification maxPrivacyClasses = DataPrivacyClassification.All,
            IThreadPool backgroundLogThreadPool = null)
            : base(new PiiEncryptingLoggerCore(piiEncrypter, downstreamLogger), new LoggerContext()
            {
                BackgroundLoggingThreadPool = backgroundLogThreadPool,
                ComponentName = downstreamLogger.ComponentName,
                DefaultPrivacyClass = downstreamLogger.DefaultPrivacyClass,
                TraceId = downstreamLogger.TraceId,
                ValidLogLevels = downstreamLogger.ValidLogLevels,
                ValidPrivacyClasses = downstreamLogger.ValidPrivacyClasses,
                MaxLogLevels = maxLogLevels,
                MaxPrivacyClasses = maxPrivacyClasses,
            })
        {
        }

        /// <summary>
        /// Private constructor for creating inherited logger objects
        /// </summary>
        /// <param name="core"></param>
        /// <param name="context"></param>
        private PiiEncryptingLogger(PiiEncryptingLoggerCore core, LoggerContext context)
            : base(core, context)
        {
        }

        protected override ILogger CloneImplementation(ILoggerCore core, LoggerContext clonedContext)
        {
            return new PiiEncryptingLogger((PiiEncryptingLoggerCore)core, clonedContext);
        }

        private class PiiEncryptingLoggerCore : ILoggerCore
        {
            private readonly IStringEncrypterPii _stringEncrypter;
            private readonly ILogger _downstreamLogger;

            public PiiEncryptingLoggerCore(IStringEncrypterPii encrypter, ILogger downstreamLogger)
            {
                _stringEncrypter = encrypter;
                _downstreamLogger = downstreamLogger;
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking)
            {
                return _downstreamLogger.Flush(cancellizer, realTime, blocking);
            }

            public void LoggerImplementation(PooledLogEvent value)
            {
                // Encrypt PII if needed
                if ((DataPrivacyClassification.PrivateContent & value.PrivacyClassification) != 0 &&
                    !CommonInstrumentation.IsEncrypted(value.MessageBuffer.Builder))
                {
                    PooledStringBuilder newMessageBuffer = StringBuilderPool.Rent();
                    _stringEncrypter.EncryptString(value.MessageBuffer.Builder, newMessageBuffer.Builder);
                    PooledLogEvent maskedLogEvent = PooledLogEvent.Create(
                        value.Component,
                        newMessageBuffer,
                        value.Level,
                        value.Timestamp,
                        value.TraceId,
                        value.PrivacyClassification);

                    value.Dispose(); // The old plaintext message and buffer is now obsolete and replaced with the newly created buffer
                    value = maskedLogEvent;
                }

                _downstreamLogger.Log(value);
            }
        }
    }
}
