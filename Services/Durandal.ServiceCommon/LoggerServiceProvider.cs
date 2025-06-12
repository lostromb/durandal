using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Microsoft.ApplicationInsights;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Service
{
    public class LoggerServiceProvider : ConfigBasedServiceResolver<ILogger>
    {
        private readonly ILogger _bootstrapLogger;
        private readonly string _defaultComponentName;
        private readonly string _durandalEnvironmentDirectory;

        public LoggerServiceProvider(
            WeakPointer<IConfiguration> config,
            IRealTimeProvider realTime,
            ILogger bootstrapLogger,
            string defaultComponentName,
            string durandalEnvironmentDirectory)
                : base(config, bootstrapLogger, realTime)
        {
            _bootstrapLogger = bootstrapLogger.AssertNonNull(nameof(bootstrapLogger));
            _defaultComponentName = defaultComponentName.AssertNonNullOrEmpty(nameof(defaultComponentName));
            _durandalEnvironmentDirectory = durandalEnvironmentDirectory.AssertNonNullOrEmpty(nameof(durandalEnvironmentDirectory));
        }

        protected override Task<RetrieveResult<ILogger>> CreateNewImpl(IConfiguration config, IRealTimeProvider realTime)
        {
            IList<ILogger> allLoggers = new List<ILogger>();
            ISet<string> loggerNames = new HashSet<string>();
            foreach (string l in config.GetStringList("loggers"))
            {
                if (!loggerNames.Contains(l.ToLowerInvariant()))
                {
                    loggerNames.Add(l.ToLowerInvariant());
                }
            }

            if (loggerNames.Contains("console"))
            {
#if DEBUG
                LogLevel maxConsoleLevels = LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Crt | LogLevel.Vrb | LogLevel.Ins;
#else
                LogLevel maxConsoleLevels = LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Crt;
#endif
                ILogger consoleLogger = new ConsoleLogger(
                    _defaultComponentName,
                    maxLevels: maxConsoleLevels,
                    maxThreadUse: 0.2); // arbitrary limiter ratio to keep console throttling down

                allLoggers.Add(consoleLogger);
                _bootstrapLogger.Log("Console logger enabled");
            }

            if (Debugger.IsAttached)
            {
                LogLevel maxDebugLevels = LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Crt | LogLevel.Vrb | LogLevel.Ins;
                ILogger debugLogger = new DebugLogger(
                    _defaultComponentName,
                    maxLevel: maxDebugLevels);

                allLoggers.Add(debugLogger);
                _bootstrapLogger.Log("Debug logger enabled");
            }

            //if (loggerNames.Contains("file"))
            //{
            //    ILogger fileLogger = new FileLogger(
            //        _defaultComponentName,
            //        backgroundLogThreadPool,
            //        _bootstrapLogger,
            //        metrics,
            //        dimensions,
            //        10485760,
            //        Path.Combine(_durandalEnvironmentDirectory, "logs"));
            //    allLoggers.Add(fileLogger);
            //    _bootstrapLogger.Log("File logger enabled");
            //}

            //if (loggerNames.Contains("http"))
            //{
            //    if (config.ContainsKey("remoteLoggingEndpoint") && config.ContainsKey("remoteLoggingPort") && config.ContainsKey("remoteLoggingStream"))
            //    {
            //        ILogger remoteInstrumentation = new RemoteInstrumentationLogger(
            //            new PortableHttpClient(
            //                config.GetString("remoteLoggingEndpoint"),
            //                config.GetInt32("remoteLoggingPort"),
            //        false,
            //                NullLogger.Singleton,
            //                new WeakPointer<IMetricCollector>(metrics),
            //                dimensions),
            //            new InstrumentationBlobSerializer(),
            //            DefaultRealTimeProvider.Singleton,
            //            config.GetString("remoteLoggingStream"),
            //        tracesOnly: true,
            //            bootstrapLogger: _bootstrapLogger,
            //            metrics: metrics,
            //            dimensions: dimensions,
            //            componentName: _defaultComponentName,
            //            //validLogLevels: LogLevel.All,
            //            //maxLogLevels: LogLevel.All,
            //            //maxPrivacyClasses: DataPrivacyClassification.All,
            //            //defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
            //            backgroundLogThreadPool: backgroundLogThreadPool);
            //        allLoggers.Add(remoteInstrumentation);
            //        _bootstrapLogger.Log("Remote instrumentation logger enabled");
            //    }
            //    else
            //    {
            //        _bootstrapLogger.Log("The http logger was specified, but one of the necessary configuration keys (remoteLoggingEndpoint, remoteLoggingPort, remoteLoggingStream) are missing!", LogLevel.Err);
            //    }
            //}

            //// Add an EventOnlyLogger onto the stack to be used for insta-tracing (keeping a transient log history)
            //allLoggers.Add(new EventOnlyLogger(
            //    _defaultComponentName,
            //    validLogLevels: LogLevel.All,
            //    backgroundLogThreadPool: backgroundLogThreadPool));

            ILogger returnVal = NullLogger.Singleton;

            //if (allLoggers.Count == 1)
            //{
            //    returnVal = allLoggers[0];
            //}
            //else if (allLoggers.Count > 1)
            //{
            //    returnVal = new AggregateLogger(_defaultComponentName, backgroundLogThreadPool, allLoggers.ToArray());
            //}

            //// Enable pii encryption at the top level if necessary
            //if (piiEncrypter != null)
            //{
            //    returnVal = new PiiEncryptingLogger(returnVal, piiEncrypter, LogLevel.All, DataPrivacyClassification.All, backgroundLogThreadPool);
            //}

            //if (returnVal == null)
            //{
            //    _bootstrapLogger.Log("It looks like all loggers are turned off! I'll just be quiet then...");
            //    returnVal = NullLogger.Singleton;
            //}

            return Task.FromResult<RetrieveResult<ILogger>>(new RetrieveResult<ILogger>(returnVal));
        }
    }
}
