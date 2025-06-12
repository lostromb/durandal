using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Durandal.API;
using Durandal.API.Data;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Alignment;
using Durandal.Common.Packages;
using Durandal.Common.Security;
using Durandal.Common.Utils;
using Durandal.Common.Utils.Cache;
using Durandal.Common.File;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.Utils.Tasks;
using Durandal.Common.Speech;
using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.TTS;
using System.Threading.Tasks;
using Durandal.Common.NLP.Feature;
using Durandal.Common.Speech.SR.Cortana;
using Durandal.Common.Speech.TTS.Bing;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Net.Http;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Audio;
using Durandal.Common.NLP.Language;
using Durandal.Common.LU;
using Durandal.BondProtocol;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Security.Server;
using Durandal.Common.Utils.IO;
using Durandal;
using Durandal.Common.NLP.Annotation;

namespace LanguageUnderstandingAspCore
{
    public static class LUEngine
    {
        public static async Task Create(IHttpServer hostServer)
        {
            string defaultComponentName = "LUMain";

            ILogger bootstrapLogger = new ConsoleLogger(defaultComponentName, LogLevel.All, false);

            IFileSystem configFileSystem = new WindowsFileSystem(bootstrapLogger, @"C:\Users\lostromb\Documents\Visual Studio 2015\Projects\Durandal\target");
            bootstrapLogger.Log(string.Format("Durandal LanguageUnderstanding Engine v{0} built on {1}", SVNVersionInfo.VersionString, SVNVersionInfo.BuildDate));

            // Unpack bundle data if present
            UnpackBundleFile(bootstrapLogger, "lu.bundle");

            bootstrapLogger.Log("Bootstrapping config...");
            Configuration luConfig = await IniFileConfiguration.Create(bootstrapLogger.Clone("PrimaryConfig"), new VirtualPath("LanguageUnderstanding_Config"), configFileSystem, true);

            // Now see what loggers should actually be used, based on the config
            ILogger coreLogger = CreateAggregateLogger(defaultComponentName, luConfig, bootstrapLogger);

            if (coreLogger == null)
            {
                bootstrapLogger.Log("It looks like all loggers are turned off! I'll just be quiet then...");
                coreLogger = NullLogger.Singleton;
            }

            IFileSystem resourceManager = new WindowsFileSystem(coreLogger, @"C:\Users\lostromb\Documents\Visual Studio 2015\Projects\Durandal\target");

            coreLogger.Log("Configuring runtime environment...");
            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;

            coreLogger.Log("Starting language understanding service...");
            IThreadPool luThreadPool = new CustomThreadPool(coreLogger.Clone("LUCoreThreadPool"), "LUCore");
            IHttpClientFactory annotatorHttpClientFactory = new PortableHttpClientFactory();
            IAnnotatorProvider annotatorProvider = new HardcodedAnnotatorProvider(
                annotatorHttpClientFactory,
                resourceManager,
                bingMapsApiKey: luConfig.GetString("bingMapsApiKey"),
                bingLocalApiKey: luConfig.GetString("bingLocalApiKey"),
                bingSpellerApiKey: luConfig.GetString("bingSpellerApiKey"));
            LanguageUnderstandingEngine core = new LanguageUnderstandingEngine(new LUConfiguration(luConfig), coreLogger.Clone("LUCore"), resourceManager, annotatorProvider, luThreadPool);

            if (!luConfig.ContainsKey("answerDomains"))
            {
                coreLogger.Log("No answer domains are specified in the config file! Nothing will be loaded.");
            }

            IPackageLoader packageLoader = new PortableZipPackageFileLoader(coreLogger.Clone("PackageLoader"));
            await PackageInstaller.InstallNewOrUpdatedPackages(coreLogger, resourceManager, packageLoader, PackageComponent.LU);

            ICultureInfoFactory cultureInfoFactory = new WindowsCultureInfoFactory();
            IList<string> enabledLocales = luConfig.GetVectorString("answerLocales");
            core.Initialize(enabledLocales, cultureInfoFactory);
            
            List<ILUTransportProtocol> transportProtocols = new List<ILUTransportProtocol>()
            {
                new LUJsonTransportProtocol(),
                new LUBondTransportProtocol()
            };

            LUHttpServer serverTransport = new LUHttpServer(
                core,
                luConfig,
                hostServer,
                coreLogger.Clone("LUHttpServer"),
                transportProtocols,
                resourceManager,
                packageLoader);
            serverTransport.StartServer("LUHTTP");

            foreach (string locale in enabledLocales)
            {
                ISet<string> domains = new HashSet<string>();

                // If there is no locale-specific domain config this will fall back to the default
                foreach (string enabledDomain in luConfig.GetVectorString("answerDomains", "locale", locale))
                {
                    if (!domains.Contains(enabledDomain))
                    {
                        domains.Add(enabledDomain);
                    }
                }

                // Check if the enabled domain list is empty or "*".
                // If so, tell the model to load everything
                if (domains.Count == 0 || domains.Count == 1 && domains.First().Equals("*"))
                {
                    core.LoadModels(locale, domains);
                }
                else
                {
                    core.LoadModels(locale, domains);
                }
            }
        }

        /// <summary>
        /// From a list of config strings, builds a set of loggers to be used for the main service.
        /// </summary>
        /// <param name="defaultComponentName">The component name to use as the base for all loggers</param>
        /// <param name="config">The program's configuration</param>
        /// <param name="bootstrapLogger">The logger that is used at bootstrap time (the "metalogger")</param>
        /// <returns></returns>
        private static ILogger CreateAggregateLogger(string defaultComponentName, Configuration config, ILogger bootstrapLogger)
        {
            IList<ILogger> allLoggers = new List<ILogger>();
            ISet<string> loggerNames = new HashSet<string>();
            foreach (string l in config.GetVectorString("loggers"))
            {
                if (!loggerNames.Contains(l.ToLowerInvariant()))
                {
                    loggerNames.Add(l.ToLowerInvariant());
                }
            }

            if (loggerNames.Contains("console"))
            {
                ILogger consoleLogger = new DebugLogger(defaultComponentName, LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Vrb, false);
#if DEBUG
                consoleLogger.ValidLevels = LogLevel.Std | LogLevel.Wrn | LogLevel.Err | LogLevel.Vrb;
#else
                consoleLogger.ValidLevels = LogLevel.Std | LogLevel.Wrn | LogLevel.Err;
#endif
                allLoggers.Add(consoleLogger);
                bootstrapLogger.Log("Console logger enabled");
            }

            if (loggerNames.Contains("file"))
            {
                ILogger fileLogger = new FileLogger(defaultComponentName, defaultComponentName, false, 10485760);
                allLoggers.Add(fileLogger);
                bootstrapLogger.Log("File logger enabled");
            }

            if (loggerNames.Contains("http"))
            {
                if (config.ContainsKey("remoteLoggingEndpoint") && config.ContainsKey("remoteLoggingPort") && config.ContainsKey("remoteLoggingStream"))
                {
                    ILogger remoteInstrumentation = new RemoteInstrumentationLogger(
                        new HttpSocketClient(new TcpClientSocketFactory(NullLogger.Singleton), config.GetString("remoteLoggingEndpoint"), config.GetInt("remoteLoggingPort"), false, NullLogger.Singleton),
                        new InstrumentationBlobSerializer(),
                        config.GetString("remoteLoggingStream"),
                        true,
                        defaultComponentName);
                    allLoggers.Add(remoteInstrumentation);
                    bootstrapLogger.Log("Remote instrumentation logger enabled");
                }
                else
                {
                    bootstrapLogger.Log("The http logger was specified, but one of the necessary configuration keys (remoteLoggingEndpoint, remoteLoggingPort, remoteLoggingStream) are missing!", LogLevel.Err);
                }
            }

            // Add an EventOnlyLogger onto the stack to be used for insta-tracing (keeping a transient log history)
            allLoggers.Add(new EventOnlyLogger(defaultComponentName));

            if (allLoggers.Count == 0)
            {
                return null;
            }
            else if (allLoggers.Count == 1)
            {
                return allLoggers[0];
            }
            else
            {
                return new AggregateLogger(defaultComponentName, allLoggers.ToArray());
            }
        }

        private static void UnpackBundleFile(ILogger logger, string fileName)
        {
            FileInfo bundleFile = new FileInfo(fileName);
            if (bundleFile.Exists)
            {
                logger.Log("Unpacking bundle file " + bundleFile.FullName);
                WindowsFileSystem localDirectory = new WindowsFileSystem(logger);
                using (FileStream bundleStream = new FileStream(bundleFile.FullName, FileMode.Open, FileAccess.Read))
                {
                    InMemoryFileSystem bundleData = InMemoryFileSystem.Deserialize(bundleStream, false);
                    FileHelpers.CopyAllFiles(bundleData, localDirectory, VirtualPath.Root, logger, true);
                }
            }
            else
            {
                logger.Log("Bundle file " + bundleFile.FullName + " was not found! Required runtime files may be missing", LogLevel.Wrn);
            }
        }
    }
}
