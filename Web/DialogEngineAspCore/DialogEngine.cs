namespace DialogEngineAspCore
{
    using Durandal;
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.Cache;
    using Durandal.Common.Compression;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.IO;
    using Durandal.Common.LG.Statistical;
    using Durandal.Common.Logger;
    using Durandal.Common.LU;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.Alignment;
    using Durandal.Common.NLP.Feature;
    using Durandal.Common.NLP.Language;
    using Durandal.Common.NLP.Language.English;
    using Durandal.Common.Packages;
    using Durandal.Common.Remoting;
    using Durandal.Common.Remoting.Protocol;
    using Durandal.Common.Security;
    using Durandal.Common.Security.OAuth;
    using Durandal.Common.Security.Server;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Speech.TTS.Bing;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.Extensions.Azure.AppInsights;
    using Durandal.Extensions.BondProtocol;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    public class DialogEngine
    {
        private static ILogger APP_DOMAIN_LOGGER = NullLogger.Singleton;

        private DialogWebService _webService;

        private DialogEngine(DialogWebService service)
        {
            _webService = service;
        }

        public static async Task<DialogEngine> Create(IHttpServer hostingServer)
        {
            // Declare all of the disposable things we might use throughout the program
            IConfiguration mainConfig = null;
            GarbageCollectionObserver gcObserver = null;
            IThreadPool asyncLoggingFixedThreadPool = null;
            IThreadPool asyncLoggingThreadPool = null;
            CryptographicRandom cryptoRandom = null;
            ISocketFactory socketFactory = null;
            Win32AESDelegates aesImpl = null;
            FileMetricOutput fileMetricOutput = null;
            AppInsightsMetricOutput appInsightsMetricOutput = null;
            IHttpClient luClient = null;
            IThreadPool workerThreadPool = null;
            IThreadPool httpThreadPool = null;
            IDurandalPluginProvider pluginProvider = null;
            DialogWebService dialogWebService = null;
            DialogProcessingEngine engine = null;

            string defaultComponentName = "DialogService";
            string machineHostName = Dns.GetHostName();
            IRealTimeProvider realTimeDefinition = DefaultRealTimeProvider.Singleton;
            DimensionSet coreMetricDimensions = new DimensionSet(new MetricDimension[]
                {
                    new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceName, "Durandal.DialogEngine"),
                    new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceVersion, SVNVersionInfo.AssemblyVersion),
                    new MetricDimension(CommonInstrumentation.Key_Dimension_HostName, machineHostName)
                });

            AppDomain.CurrentDomain.UnhandledException += PrintUnhandledException;

            ILogger bootstrapLogger = new ConsoleLogger(defaultComponentName, LogLevel.All, null);
            APP_DOMAIN_LOGGER = bootstrapLogger;
            MetricCollector metrics = new MetricCollector(bootstrapLogger, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
            //windowsPerfCounters = new WindowsPerfCounterReporter(bootstrapLogger, coreMetricDimensions);
            //metrics.AddMetricSource(windowsPerfCounters);
            gcObserver = new GarbageCollectionObserver(metrics, coreMetricDimensions);
            //metrics.AddMetricOutput(new ConsoleMetricOutput());

            BufferPool<byte>.Metrics = metrics;
            BufferPool<byte>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "byte"));
            BufferPool<float>.Metrics = metrics;
            BufferPool<float>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "float"));

            string runtimeDirectory = ServiceCommon.GetDurandalEnvironmentDirectory(Array.Empty<string>(), bootstrapLogger);

            // Unpack bundle data if present
            ServiceCommon.UnpackBundleFile(bootstrapLogger, "bundle_dialog.zip", runtimeDirectory);

            // Read configuration
            IFileSystem configFileSystem = new WindowsFileSystem(bootstrapLogger, runtimeDirectory);
            mainConfig = await IniFileConfiguration.Create(bootstrapLogger.Clone("DialogConfig"), new VirtualPath("Durandal.DialogEngine_config.ini"), configFileSystem, realTimeDefinition, true, true);

            // Now see what loggers should actually be used, based on the config
            asyncLoggingThreadPool = new CustomThreadPool(bootstrapLogger, metrics, coreMetricDimensions, ThreadPriority.BelowNormal, "AsyncLogThreadPool", 8);
            asyncLoggingFixedThreadPool = new FixedCapacityThreadPool(
                asyncLoggingThreadPool,
                bootstrapLogger,
                metrics,
                coreMetricDimensions,
                "AsyncLogThreadPool",
                8,
                ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable);

            cryptoRandom = new CryptographicRandom();
            aesImpl = new Win32AESDelegates();
            IRSADelegates rsaImpl = new StandardRSADelegates(cryptoRandom);
            IStringEncrypterPii piiEncrypter = new NullStringEncrypter();

            if (mainConfig.ContainsKey("piiEncryptionKey"))
            {
                PublicKey rsaKey = PublicKey.ReadFromXml(mainConfig.GetString("piiEncryptionKey"));
                DataPrivacyClassification privacyClassesToEncrypt = (DataPrivacyClassification)mainConfig.GetInt32(
                    "privacyClassesToEncrypt",
                    (int)(DataPrivacyClassification.EndUserIdentifiableInformation | DataPrivacyClassification.PrivateContent | DataPrivacyClassification.PublicPersonalData));

                if (privacyClassesToEncrypt != DataPrivacyClassification.Unknown)
                {
                    bootstrapLogger.Log("RSA PII encrypter is enabled");
                    bootstrapLogger.Log("Privacy classes to encrypt: " + privacyClassesToEncrypt.ToString());
                    piiEncrypter = new RsaStringEncrypterPii(
                        rsaImpl,
                        aesImpl,
                        cryptoRandom,
                        realTimeDefinition,
                        privacyClassesToEncrypt,
                        rsaKey);
                }
            }
            else
            {
                bootstrapLogger.Log("PII encryption is disabled (no RSA public key specified)");
            }

            ILogger coreLogger = ServiceCommon.CreateAggregateLogger(
                defaultComponentName,
                mainConfig,
                bootstrapLogger,
                asyncLoggingThreadPool,
                metrics,
                coreMetricDimensions,
                piiEncrypter,
                realTimeDefinition,
                runtimeDirectory);

            if (coreLogger == null)
            {
                bootstrapLogger.Log("It looks like all loggers are turned off! I'll just be quiet then...");
                coreLogger = NullLogger.Singleton;
            }
            else
            {
                APP_DOMAIN_LOGGER = coreLogger;
            }

            IFileSystem fileSystem = new WindowsFileSystem(coreLogger, runtimeDirectory);

            coreLogger.Log("Configuring runtime environment...");
            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
            ServicePointManager.Expect100Continue = false;
            //DefaultRealTimeProvider.HighPrecisionWaitProvider = new Win32HighPrecisionWaitProvider();

            fileMetricOutput = new FileMetricOutput(coreLogger, "DialogEngineAspCore", ".\\logs", 10485760);
            metrics.AddMetricOutput(fileMetricOutput);
            if (!string.IsNullOrEmpty(mainConfig.GetString("appInsightsKey")))
            {
                coreLogger.Log("Enabling AppInsights metrics upload");
                appInsightsMetricOutput = new AppInsightsMetricOutput(coreLogger, mainConfig.GetString("appInsightsKey"));
                metrics.AddMetricOutput(appInsightsMetricOutput);
            }

            socketFactory = new PooledTcpClientSocketFactory(coreLogger.Clone("TcpSocketFactory"), metrics, coreMetricDimensions);
            IHttpClientFactory httpClientFactory = new SocketHttpClientFactory(socketFactory, DefaultRealTimeProvider.Singleton);

            IPackageLoader packageLoader = new PortableZipPackageFileLoader(coreLogger.Clone("PackageLoader"));

            DialogConfiguration config = new DialogConfiguration(new WeakPointer<IConfiguration>(mainConfig));
            DialogWebConfiguration webConfig = new DialogWebConfiguration(new WeakPointer<IConfiguration>(mainConfig));
            RemotingConfiguration remotingConfig = new RemotingConfiguration(new WeakPointer<IConfiguration>(mainConfig));

            string luEndpoint = mainConfig.GetString("luServerHost", "localhost");
            int luPort = mainConfig.GetInt32("luServerPort", 62291);
            ILogger luHttpLogger = coreLogger.Clone("LUHttpClient");
            luClient = httpClientFactory.CreateHttpClient(luEndpoint, luPort, false, luHttpLogger);
            luClient.SetReadTimeout(TimeSpan.FromMilliseconds(mainConfig.GetInt32("luTimeout", 2000)));

            ILUTransportProtocol luProtocol = new LUBondTransportProtocol();

            LUHttpClient luInterface = new LUHttpClient(luClient, luHttpLogger, luProtocol);
            coreLogger.Log("LU connection is configured for " + luClient.ServerAddress);

            // TODO: Move this into a common builder/helper class
            IPronouncer pronouncer = await EnglishPronouncer.Create(
                new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\en-us\\cmu-pronounce-ipa.dict"),
                new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\en-us\\pronouncer.dat"),
                coreLogger.Clone("Pronouncer"),
                fileSystem);

            IWordBreaker wordBreaker = new EnglishWordBreaker();
            IWordBreaker wholeWordBreaker = new EnglishWholeWordBreaker();
            IDictionary<string, NLPTools> nlpTools = new Dictionary<string, NLPTools>();
            EditDistancePronunciation pronouncerEditDistance = new EditDistancePronunciation(pronouncer, wholeWordBreaker, "en-us");
            ILGFeatureExtractor lgFeaturizer = new EnglishLGFeatureExtractor();
            nlpTools.Add("en-us", new NLPTools()
            {
                Pronouncer = pronouncer,
                WordBreaker = wholeWordBreaker,
                FeaturizationWordBreaker = wordBreaker,
                EditDistance = pronouncerEditDistance.Calculate,
                LGFeatureExtractor = lgFeaturizer,
                CultureInfoFactory = new WindowsCultureInfoFactory(),
                SpeechTimingEstimator = new EnglishSpeechTimingEstimator()
            });

            int workerThreadPoolSize = Math.Max(Environment.ProcessorCount, 9);

            // Build thread pools.
            // WorkerThreadPool is used by dialog for things like asynchronous audio
            workerThreadPool = new TaskThreadPool(metrics, coreMetricDimensions, "DialogPool");
            // new CustomThreadPool(coreLogger.Clone("DialogThreadPool"), metrics, coreMetricDimensions, "DialogPool", workerThreadPoolSize, !webConfig.FailFastPlugins);
            // HttpThreadPool is used by the server to handle HTTP requests to dialog server
            httpThreadPool = new TaskThreadPool(metrics, coreMetricDimensions, "HttpPool");
            //new CustomThreadPool(coreLogger.Clone("HttpThreadPool"), metrics, coreMetricDimensions, "HttpPool", workerThreadPoolSize, true);

            // Build all services
            DialogServiceCollection allServices = await ServiceFactory.BuildServices(mainConfig, coreLogger, workerThreadPool, metrics, coreMetricDimensions, fileSystem);

            GenericEntityResolver genericEntityResolver = new GenericEntityResolver(nlpTools);
            DefaultEntityResolver entityResolver = new DefaultEntityResolver(genericEntityResolver);

            // Build dialog executor wrapper
            IDialogExecutor executor;
            if (webConfig.SandboxPlugins)
            {
                executor = new SandboxedDialogExecutor(webConfig.MaxPluginExecutionTime, webConfig.FailFastPlugins);
            }
            else
            {
                executor = new BasicDialogExecutor(webConfig.FailFastPlugins);
            }

            IAudioCodecFactory codecs = ServiceFactory.CreateCodecCollection(coreLogger);

            // Set up SR, TTS, and audio codecs
            string ttsProvider = webConfig.TTSProvider;
            string srProvider = webConfig.SRProvider;
            int speechPoolSize = Math.Max(1, webConfig.SpeechPoolSize);

            ISpeechSynth speechSynth = ServiceFactory.TryGetSpeechSynth(ttsProvider, coreLogger.Clone("DialogTTS"), nlpTools, webConfig.TTSApiKey, metrics, coreMetricDimensions, workerThreadPool, speechPoolSize);
            ISpeechRecognizerFactory speechReco = ServiceFactory.TryGetSpeechRecognizer(srProvider, coreLogger.Clone("DialogSR"), webConfig.SRApiKey, false, realTimeDefinition, speechPoolSize);

            OAuthManager oauthManager = new OAuthManager(config.OAuthCallbackUri, allServices.OAuthSecretStore);

            List<IDialogTransportProtocol> dialogProtocols = new List<IDialogTransportProtocol>()
            {
                new DialogJsonTransportProtocol(),
                new DialogBondTransportProtocol(),
                new DialogLZ4JsonTransportProtocol(),
                new DialogLZ4BondTransportProtocol()
            };

            ILGScriptCompiler lgScriptCompiler = new RoslynLGScriptCompiler();

            IRemoteDialogProtocol remotingProtocol;
            if ("json".Equals(remotingConfig.IpcProtocol))
            {
                remotingProtocol = new JsonRemoteDialogProtocol();
            }
            else
            {
                remotingProtocol = new BondRemoteDialogProtocol();
            }

            string pluginLoaderType = mainConfig.GetString("pluginLoader", "basic");
            if ("locally_remoted".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase))
            {
                IDurandalPluginLoader loader = new ResidentDllPluginLoader(
                    executor,
                    coreLogger.Clone("DllPluginLoader"),
                    fileSystem,
                    new VirtualPath(RuntimeDirectoryName.PLUGIN_DIR),
                    fileSystem,
                    PluginFrameworkLevel.NetFull);

                pluginProvider = new LocallyRemotedPluginProvider(
                    coreLogger.Clone("PluginProvider"),
                    loader,
                    remotingProtocol,
                    remotingConfig,
                    workerThreadPool,
                    speechSynth,
                    speechReco,
                    oauthManager,
                    httpClientFactory,
                    fileSystem,
                    lgScriptCompiler,
                    nlpTools,
                    entityResolver,
                    metrics,
                    coreMetricDimensions,
                    serverSocketFactory: null,
                    clientSocketFactory: null,
                    realTime: realTimeDefinition,
                    useDebugTimeouts: Debugger.IsAttached);
            }
            else if ("appdomain_isolated".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase))
            {
                throw new PlatformNotSupportedException("Appdomain isolation is not supported in .Net Core");
            }
            else if ("containerized".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase) ||
                "loadcontext_isolated".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase))
            {
                IServerSocketFactory serverSocketFactory = ServiceFactory.CreateServerSocketFactory(remotingConfig.RemotingPipeImplementation, coreLogger);
                pluginProvider = await LoadContextIsolatedPluginProvider.Create(
                    coreLogger.Clone("ContainerizedPluginProvider"),
                    fileSystem,
                    new DirectoryInfo(runtimeDirectory),
                    httpClientFactory,
                    new DirectoryInfo(Path.Combine(runtimeDirectory, "temp_containers")),
                    entityResolver,
                    speechSynth,
                    speechReco,
                    oauthManager,
                    realTimeDefinition,
                    remotingProtocol,
                    metrics,
                    coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_IPCMethod, remotingConfig.RemotingPipeImplementation)),
                    serverSocketFactory,
                    remotingConfig,
                    Debugger.IsAttached);
            }
            else if ("process_isolated".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase))
            {
                IServerSocketFactory serverSocketFactory = ServiceFactory.CreateServerSocketFactory(remotingConfig.RemotingPipeImplementation, coreLogger);
                pluginProvider = await ProcessIsolatedPluginProvider.Create(
                    coreLogger.Clone("ContainerizedPluginProvider"),
                    fileSystem,
                    new DirectoryInfo(runtimeDirectory),
                    httpClientFactory,
                    new DirectoryInfo(Path.Combine(runtimeDirectory, "temp_containers")),
                    entityResolver,
                    speechSynth,
                    speechReco,
                    oauthManager,
                    realTimeDefinition,
                    remotingProtocol,
                    metrics,
                    coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_IPCMethod, remotingConfig.RemotingPipeImplementation)),
                    serverSocketFactory,
                    remotingConfig,
                    DialogRuntimeFramework.RUNTIME_NETCORE,
                    Debugger.IsAttached);
            }
            else
            {
                if (!"basic".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase))
                {
                    coreLogger.Log("Unknown plugin loader type \"" + pluginLoaderType + "\", reverting to basic implementation", LogLevel.Err);
                }

                IDurandalPluginLoader loader = new ResidentDllPluginLoader(
                    executor,
                    coreLogger.Clone("DllPluginLoader"),
                    fileSystem,
                    new VirtualPath(RuntimeDirectoryName.PLUGIN_DIR),
                    fileSystem,
                    PluginFrameworkLevel.NetFull);

                pluginProvider = new MachineLocalPluginProvider(
                    coreLogger.Clone("PluginProvider"),
                    loader,
                    fileSystem,
                    nlpTools,
                    entityResolver,
                    speechSynth,
                    speechReco,
                    oauthManager,
                    httpClientFactory,
                    lgScriptCompiler);
            }

            DialogEngineParameters dialogParams = new DialogEngineParameters(config, new WeakPointer<IDurandalPluginProvider>(pluginProvider))
            {
                Logger = coreLogger.Clone("DialogProcessingEngine"),
                ConversationStateCache = new WeakPointer<IConversationStateCache>(allServices.ConversationStateCache),
                UserProfileStorage = allServices.UserProfileStore,
                DialogActionCache = new WeakPointer<ICache<DialogAction>>(allServices.DialogActionStore),
                WebDataCache = new WeakPointer<ICache<CachedWebData>>(allServices.WebDataStore)
            };

            engine = new DialogProcessingEngine(dialogParams);

            DialogWebParameters dialogWebParams = new DialogWebParameters(webConfig, new WeakPointer<DialogProcessingEngine>(engine))
            {
                Logger = coreLogger.Clone("DialogWebService"),
                FileSystem = fileSystem,
                LuConnection = luInterface,
                ClientContextCache = new WeakPointer<ICache<ClientContext>>(allServices.ClientContextStore),
                ConversationStateCache = allServices.ConversationStateCache,
                WebDataCache = new WeakPointer<ICache<CachedWebData>>(allServices.WebDataStore),
                DialogActionStore = new WeakPointer<ICache<DialogAction>>(allServices.DialogActionStore),
                PublicKeyStorage = allServices.PublicKeyStore,
                ProcessingThreadPool = workerThreadPool,
                HttpServer = hostingServer,
                SpeechReco = speechReco,
                SpeechSynth = speechSynth,
                CodecFactory = codecs,
                StreamingAudioCache = allServices.StreamingAudioCache,
                TransportProtocols = dialogProtocols,
                Metrics = metrics,
                MetricDimensions = coreMetricDimensions,
                MachineHostName = machineHostName
            };

            dialogWebService = await DialogWebService.Create(dialogWebParams);

            ISet<string> domains = new HashSet<string>();
            foreach (string enabledDomain in webConfig.PluginIdsToLoad)
            {
                domains.Add(enabledDomain);
            }

            await engine.LoadPlugins(domains, realTimeDefinition);

            DialogEngine returnVal = new DialogEngine(dialogWebService);
            return returnVal;
        }

        //private void DisposeOfJunk()
        //{
        //    engine?.Dispose();
        //    windowsPerfCounters?.Dispose();
        //    gcObserver?.Dispose();
        //    mainConfig?.Dispose();
        //    asyncLoggingFixedThreadPool?.Dispose();
        //    asyncLoggingThreadPool?.Dispose();
        //    cryptoRandom?.Dispose();
        //    aesImpl?.Dispose();
        //    fileMetricOutput?.Dispose();
        //    appInsightsMetricOutput?.Dispose();
        //    luClient?.Dispose();
        //    workerThreadPool?.Dispose();
        //    httpThreadPool?.Dispose();
        //    socketServer?.Dispose();
        //    pluginProvider?.Dispose();
        //    dialogWebService?.Dispose();
        //    dialogHttpClient?.Dispose();
        //    dialogClient?.Dispose();
        //    socketFactory?.Dispose();
        //}
        
        private static void PrintUnhandledException(object source, UnhandledExceptionEventArgs args)
        {
            try
            {
                if (APP_DOMAIN_LOGGER != null)
                {
                    object exceptionObject = args.ExceptionObject;
                    if (exceptionObject is Exception)
                    {
                        Exception ex = exceptionObject as Exception;
                        APP_DOMAIN_LOGGER.Log(ex, LogLevel.Crt);
                    }

                    APP_DOMAIN_LOGGER.Log("A potentially fatal unhandled exception was raised in the AppDomain", LogLevel.Crt);
                }
            }
            catch (Exception) { }
        }
    }
}
