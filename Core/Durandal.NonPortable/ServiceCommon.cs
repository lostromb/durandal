namespace Durandal
{
    using Durandal.API;
    using Durandal.Common.Compression;
    using Durandal.Common.Config;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.Net.Http;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Static helper class for constructing the primary interfaces that are used for dialog engine (caches, codecs, SR, TTS, etc.)
    /// </summary>
    public static class ServiceCommon
    {
        public static string GetDurandalEnvironmentDirectory(string[] commandLineArgs, ILogger logger)
        {
            IDictionary<string, List<string>> parsedArgs = CommandLineParser.ParseArgs(commandLineArgs);
            string rootRuntimeDirectory = Environment.CurrentDirectory;

            try
            {
                if (parsedArgs.ContainsKey("baseDirectory"))
                {
                    rootRuntimeDirectory = parsedArgs["baseDirectory"][0];
                    logger.Log("Found base directory in command line arg: \"" + rootRuntimeDirectory + "\"");
                }
                else
                {
                    string rootEnv = Environment.GetEnvironmentVariable("DURANDAL_ROOT");
                    if (rootEnv != null)
                    {
                        rootRuntimeDirectory = rootEnv;
                        logger.Log("Found base directory in environment variable: \"" + rootRuntimeDirectory + "\"");
                    }
                    else
                    {
                        logger.Log("Using default base directory \"" + rootRuntimeDirectory +
                        "\". This is not recommended; please specify base directory using either \"--baseDirectory\" command line or DURANDAL_ROOT environment variable", LogLevel.Wrn);
                    }
                }

                if (!Directory.Exists(rootRuntimeDirectory))
                {
                    Directory.CreateDirectory(rootRuntimeDirectory);
                }
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Err);
                rootRuntimeDirectory = Environment.CurrentDirectory;
                logger.Log("Reverting base directory to the default \"" + rootRuntimeDirectory + "\"", LogLevel.Wrn);
            }

            return rootRuntimeDirectory;
        }

        public static void UnpackBundleFile(ILogger logger, string bundleFileName, string durandalEnvironmentDirectory)
        {
            FileInfo bundleFile = new FileInfo(bundleFileName);
            if (bundleFile.Exists)
            {
                logger.Log("Unpacking bundle file " + bundleFile.FullName + " to " + durandalEnvironmentDirectory);
                RealFileSystem bundleSource = new RealFileSystem(logger, bundleFile.DirectoryName);
                RealFileSystem bundleTarget = new RealFileSystem(logger, durandalEnvironmentDirectory);
                using (ZipFileFileSystem zipFileSystem = new ZipFileFileSystem(logger.Clone("BundleFileSystem"), bundleSource, new VirtualPath(bundleFile.Name)))
                {
                    FileHelpers.CopyAllFiles(zipFileSystem, VirtualPath.Root, bundleTarget, VirtualPath.Root, logger, true);
                }
            }
            else
            {
                logger.Log("Bundle file " + bundleFile.FullName + " was not found! Required runtime files may be missing", LogLevel.Wrn);
            }
        }

        /// <summary>
        /// From a list of config strings, builds a set of loggers to be used for the main service.
        /// </summary>
        /// <param name="defaultComponentName">The component name to use as the base for all loggers</param>
        /// <param name="config">The program's configuration</param>
        /// <param name="bootstrapLogger">The logger that is used at bootstrap time (the "metalogger")</param>
        /// <param name="backgroundLogThreadPool"></param>
        /// <param name="metrics"></param>
        /// <param name="dimensions"></param>
        /// <param name="piiEncrypter"></param>
        /// <param name="realTime"></param>
        /// <param name="durandalEnvironmentDirectory"></param>
        /// <returns></returns>
        public static ILogger CreateAggregateLogger(
            string defaultComponentName,
            IConfiguration config,
            ILogger bootstrapLogger,
            IThreadPool backgroundLogThreadPool,
            IMetricCollector metrics,
            DimensionSet dimensions,
            IStringEncrypterPii piiEncrypter,
            IRealTimeProvider realTime,
            string durandalEnvironmentDirectory)
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
                    defaultComponentName,
                    maxLevels: maxConsoleLevels,
                    maxThreadUse: 0.2); // arbitrary limiter ratio to keep console throttling down

                allLoggers.Add(consoleLogger);
                bootstrapLogger.Log("Console logger enabled");
            }

            if (Debugger.IsAttached)
            {
                LogLevel maxDebugLevels = LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Crt | LogLevel.Vrb | LogLevel.Ins;
                ILogger debugLogger = new DebugLogger(
                    defaultComponentName,
                    maxLevel: maxDebugLevels);

                allLoggers.Add(debugLogger);
                bootstrapLogger.Log("Debug logger enabled");
            }

            if (loggerNames.Contains("file"))
            {
                ILogger fileLogger = new FileLogger(
                    new RealFileSystem(bootstrapLogger.Clone("FileLogger"), durandalEnvironmentDirectory),
                    defaultComponentName,
                    Process.GetCurrentProcess().ProcessName,
                    backgroundLogThreadPool,
                    bootstrapLogger,
                    50 * 1024 * 1024, // 50Mb
                    new VirtualPath("logs"));
                allLoggers.Add(fileLogger);
                bootstrapLogger.Log("File logger enabled");
            }

            if (loggerNames.Contains("http"))
            {
                if (config.ContainsKey("remoteLoggingEndpoint") && config.ContainsKey("remoteLoggingPort") && config.ContainsKey("remoteLoggingStream"))
                {
                    ILogger remoteInstrumentation = new RemoteInstrumentationLogger(
                        new PortableHttpClient(
                            config.GetString("remoteLoggingEndpoint"),
                            config.GetInt32("remoteLoggingPort"),
                            false,
                            NullLogger.Singleton,
                            new WeakPointer<IMetricCollector>(metrics),
                            dimensions),
                        new InstrumentationBlobSerializer(),
                        realTime,
                        config.GetString("remoteLoggingStream"),
                        tracesOnly: true,
                        bootstrapLogger: bootstrapLogger,
                        metrics: metrics,
                        dimensions: dimensions,
                        componentName: defaultComponentName,
                        //validLogLevels: LogLevel.All,
                        //maxLogLevels: LogLevel.All,
                        //maxPrivacyClasses: DataPrivacyClassification.All,
                        //defaultPrivacyClass: DataPrivacyClassification.SystemMetadata,
                        backgroundLogThreadPool: backgroundLogThreadPool);
                    allLoggers.Add(remoteInstrumentation);
                    bootstrapLogger.Log("Remote instrumentation logger enabled");
                }
                else
                {
                    bootstrapLogger.Log("The http logger was specified, but one of the necessary configuration keys (remoteLoggingEndpoint, remoteLoggingPort, remoteLoggingStream) are missing!", LogLevel.Err);
                }
            }

            // Add an EventOnlyLogger onto the stack to be used for insta-tracing (keeping a transient log history)
            allLoggers.Add(new EventOnlyLogger(
                defaultComponentName,
                validLogLevels: LogLevel.All,
                backgroundLogThreadPool: backgroundLogThreadPool));

            ILogger returnVal = NullLogger.Singleton;

            if (allLoggers.Count == 1)
            {
                returnVal = allLoggers[0];
            }
            else if (allLoggers.Count > 1)
            {
                returnVal = new AggregateLogger(defaultComponentName, backgroundLogThreadPool, allLoggers.ToArray());
            }

            // Enable pii encryption at the top level if necessary
            if (piiEncrypter != null)
            {
                returnVal = new PiiEncryptingLogger(returnVal, piiEncrypter, LogLevel.All, DataPrivacyClassification.All, backgroundLogThreadPool);
            }

            return returnVal;
        }
    }
}
