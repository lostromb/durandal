namespace Durandal.Service
{
    using Durandal.Common.Security;
    using Durandal.Common.MathExt;
    using Durandal.API;
    using Durandal.Extensions.BondProtocol;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.Compression;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
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
    using Durandal.Common.Security.OAuth;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Utils;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using Durandal.Extensions.Azure.AppInsights;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using System.Threading;
    using Durandal.Common.Audio;
    using Durandal.Common.Cache;
    using Durandal.Common.IO;
    using Durandal.Common.Client;
    using Durandal.Common.Instrumentation.Profiling;
    using Durandal.Common.Remoting.Protocol;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Net.Http2;
    using Durandal.Extensions.NativeAudio;

    public class DialogServiceApplication : IDisposable
    {
        private static ILogger APP_DOMAIN_LOGGER = NullLogger.Singleton;

        private IConfiguration mainConfig = null;
        private WindowsPerfCounterReporter windowsPerfCounters = null;
        private GarbageCollectionObserver gcObserver = null;
        private SystemThreadPoolObserver threadPoolObserver = null;
        private IThreadPool asyncLoggingFixedThreadPool = null;
        private IThreadPool asyncLoggingThreadPool = null;
        private CryptographicRandom cryptoRandom = null;
        private ISocketFactory socketFactory = null;
        private SystemAESDelegates aesImpl = null;
        private FileMetricOutput fileMetricOutput = null;
        private AppInsightsMetricOutput appInsightsMetricOutput = null;
        private IHttpClient luClient = null;
        private IThreadPool workerThreadPool = null;
        private IThreadPool httpThreadPool = null;
        private RawTcpSocketServer socketServer = null;
        private IDurandalPluginProvider pluginProvider = null;
        private DialogServiceCollection allServices = null;
        private DialogWebService dialogWebService = null;
        private IHttpClient dialogHttpClient = null;
        private IDialogClient dialogClient = null;
        private DialogProcessingEngine engine = null;
        private ILogger coreLogger = null;
        private IHttpClientFactory httpClientFactory = null;
        private IHttpServer dialogHttpServer = null;

        private DialogServiceApplication()
        {
        }

        public static async Task<DialogServiceApplication> Create(string[] programArgs)
        {
            DialogServiceApplication returnVal = new DialogServiceApplication();
            await returnVal.Initialize(programArgs);
            return returnVal;
        }

        private async Task Initialize(string[] programArgs)
        {
            AppDomain.CurrentDomain.UnhandledException += PrintUnhandledException;
            string defaultComponentName = "DialogService";
            string machineHostName = System.Net.Dns.GetHostName();
            IRealTimeProvider realTimeDefinition = DefaultRealTimeProvider.Singleton;
            DimensionSet coreMetricDimensions = new DimensionSet(new MetricDimension[]
                {
                        new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceName, "Durandal.DialogEngine"),
                        new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceVersion, SVNVersionInfo.AssemblyVersion),
                        new MetricDimension(CommonInstrumentation.Key_Dimension_HostName, machineHostName)
                });

            ILogger bootstrapLogger = new ConsoleLogger(defaultComponentName, LogLevel.All, null);
            APP_DOMAIN_LOGGER = bootstrapLogger;
            MetricCollector metrics = new MetricCollector(bootstrapLogger, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10));
#if NETFRAMEWORK
                windowsPerfCounters = new WindowsPerfCounterReporter(
                    bootstrapLogger,
                    coreMetricDimensions,
                    WindowsPerfCounterSet.BasicLocalMachine |
                    WindowsPerfCounterSet.BasicCurrentProcess |
                    WindowsPerfCounterSet.DotNetClrCurrentProcess);
                metrics.AddMetricSource(windowsPerfCounters);
#elif NETCOREAPP
            windowsPerfCounters = new WindowsPerfCounterReporter(
                bootstrapLogger,
                coreMetricDimensions,
                WindowsPerfCounterSet.BasicLocalMachine |
                WindowsPerfCounterSet.BasicCurrentProcess);
            metrics.AddMetricSource(windowsPerfCounters);
            metrics.AddMetricSource(new NetCorePerfCounterReporter(coreMetricDimensions));
#endif
            gcObserver = new GarbageCollectionObserver(metrics, coreMetricDimensions);
            threadPoolObserver = new SystemThreadPoolObserver(metrics, coreMetricDimensions, bootstrapLogger.Clone("ThreadPoolObserver"));
            //metrics.AddMetricOutput(new ConsoleMetricOutput());

            // Enable detailed buffer pool metrics in debug mode only because they can be costly
#if DEBUG
            BufferPool<byte>.Metrics = metrics;
            BufferPool<byte>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "byte"));
            BufferPool<char>.Metrics = metrics;
            BufferPool<char>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "char"));
            BufferPool<float>.Metrics = metrics;
            BufferPool<float>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "float"));
            BufferPool<string>.Metrics = metrics;
            BufferPool<string>.MetricDimensions = coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_BufferPoolName, "string"));
#endif

            string rootRuntimeDirectory = ServiceCommon.GetDurandalEnvironmentDirectory(programArgs, bootstrapLogger);

            // Unpack bundle data if present
            ServiceCommon.UnpackBundleFile(bootstrapLogger, "bundle_dialog.zip", rootRuntimeDirectory);

            // Read configuration
            IFileSystem configFileSystem = new RealFileSystem(bootstrapLogger, rootRuntimeDirectory);
            mainConfig = await IniFileConfiguration.Create(bootstrapLogger.Clone("DialogConfig"), new VirtualPath("Durandal.DialogEngine_config.ini"), configFileSystem, realTimeDefinition, warnIfNotFound: true, reloadOnExternalChanges: true);

            // Now see what loggers should actually be used, based on the config
            asyncLoggingThreadPool = new TaskThreadPool(new WeakPointer<IMetricCollector>(metrics), coreMetricDimensions, "AsyncLogThreadPool");
            //new CustomThreadPool(bootstrapLogger, metrics, coreMetricDimensions, ThreadPriority.Normal, "AsyncLogThreadPool", 8);
            asyncLoggingFixedThreadPool = new FixedCapacityThreadPool(
                asyncLoggingThreadPool,
                bootstrapLogger,
                metrics,
                coreMetricDimensions,
                "AsyncLogThreadPool",
                8,
                ThreadPoolOverschedulingBehavior.BlockUntilThreadsAvailable);

            cryptoRandom = new CryptographicRandom();
            aesImpl = new SystemAESDelegates();
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

            coreLogger = ServiceCommon.CreateAggregateLogger(
                defaultComponentName,
                mainConfig,
                bootstrapLogger,
                asyncLoggingThreadPool,
                metrics,
                coreMetricDimensions,
                piiEncrypter,
                realTimeDefinition,
                rootRuntimeDirectory);

            if (coreLogger == null)
            {
                bootstrapLogger.Log("It looks like all loggers are turned off! I'll just be quiet then...");
                coreLogger = NullLogger.Singleton;
            }
            else
            {
                APP_DOMAIN_LOGGER = coreLogger;
            }

            //MicroProfiler.Initialize(await FileMicroProfilerClient.CreateAsync("Durandal.DialogEngine"), coreLogger.Clone("Microprofiler"));

            IFileSystem fileSystem = new RealFileSystem(coreLogger, rootRuntimeDirectory);

            coreLogger.Log("Configuring runtime environment...");
            System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
            System.Net.ServicePointManager.Expect100Continue = false;
            DefaultRealTimeProvider.HighPrecisionWaitProvider = new Win32HighPrecisionWaitProvider();

            fileMetricOutput = new FileMetricOutput(coreLogger, Process.GetCurrentProcess().ProcessName, Path.Combine(rootRuntimeDirectory, "logs"), 10485760);
            metrics.AddMetricOutput(fileMetricOutput);
            if (!string.IsNullOrEmpty(mainConfig.GetString("appInsightsConnectionString")))
            {
                coreLogger.Log("Enabling AppInsights metrics upload");
                appInsightsMetricOutput = new AppInsightsMetricOutput(coreLogger, mainConfig.GetString("appInsightsConnectionString"));
                metrics.AddMetricOutput(appInsightsMetricOutput);
            }

            socketFactory = new PooledTcpClientSocketFactory(coreLogger.Clone("TcpSocketFactory"), metrics, coreMetricDimensions);
            httpClientFactory = new SocketHttpClientFactory(
                new WeakPointer<ISocketFactory>(socketFactory),
                new WeakPointer<IMetricCollector>(metrics),
                coreMetricDimensions,
                Http2SessionManager.Default);

            IPackageLoader packageLoader = new PortableZipPackageFileLoader(coreLogger.Clone("PackageLoader"));

            DialogConfiguration config = new DialogConfiguration(new WeakPointer<IConfiguration>(mainConfig));
            DialogWebConfiguration webConfig = new DialogWebConfiguration(new WeakPointer<IConfiguration>(mainConfig));
            RemotingConfiguration remotingConfig = new RemotingConfiguration(new WeakPointer<IConfiguration>(mainConfig));

            string luEndpoint = mainConfig.GetString("luServerHost", "localhost");
            int luPort = mainConfig.GetInt32("luServerPort", 62291);
            ILogger luHttpLogger = coreLogger.Clone("LUHttpClient");
            luClient = httpClientFactory.CreateHttpClient(luEndpoint, luPort, false, luHttpLogger);
            luClient.SetReadTimeout(TimeSpan.FromMilliseconds(mainConfig.GetInt32("luTimeout", 2000)));
            luClient.InitialProtocolVersion = HttpVersion.HTTP_1_1;

            ILUTransportProtocol luProtocol = new LUBondTransportProtocol();

            LUHttpClient luInterface = new LUHttpClient(luClient, luHttpLogger, luProtocol);
            coreLogger.Log("LU connection is configured for " + luClient.ServerAddress);

            // TODO: Move this into a common builder/helper class
            IPronouncer pronouncer = await EnglishPronouncer.Create(
                new VirtualPath(RuntimeDirectoryName.MISCDATA_DIR + "\\en-US\\cmu-pronounce-ipa.dict"),
                new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\en-US\\pronouncer.dat"),
                coreLogger.Clone("Pronouncer"),
                fileSystem);

            IWordBreaker wordBreaker = new EnglishWordBreaker();
            IWordBreaker wholeWordBreaker = new EnglishWholeWordBreaker();
            NLPToolsCollection nlpTools = new NLPToolsCollection();
            EditDistancePronunciation pronouncerEditDistance = new EditDistancePronunciation(pronouncer, wholeWordBreaker, LanguageCode.EN_US);
            ILGFeatureExtractor lgFeaturizer = new EnglishLGFeatureExtractor();
            nlpTools.Add(LanguageCode.EN_US, new NLPTools()
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
            workerThreadPool = new TaskThreadPool(new WeakPointer<IMetricCollector>(metrics), coreMetricDimensions, "DialogPool");
            // new CustomThreadPool(coreLogger.Clone("DialogThreadPool"), metrics, coreMetricDimensions, "DialogPool", workerThreadPoolSize, !webConfig.FailFastPlugins);
            // HttpThreadPool is used by the server to handle HTTP requests to dialog server
            httpThreadPool = new TaskThreadPool(new WeakPointer<IMetricCollector>(metrics), coreMetricDimensions, "HttpPool");
            //new CustomThreadPool(coreLogger.Clone("HttpThreadPool"), metrics, coreMetricDimensions, "HttpPool", workerThreadPoolSize, true);

            // Build all services
            allServices = await DialogServicesFactory.BuildServices(mainConfig, coreLogger, workerThreadPool, metrics, coreMetricDimensions, fileSystem);

            GenericEntityResolver genericEntityResolver = new GenericEntityResolver(nlpTools);
            DefaultEntityResolver entityResolver = new DefaultEntityResolver(genericEntityResolver);

            // Build dialog executor wrapper
            IDialogExecutor executor;
#if NETFRAMEWORK
                if (webConfig.SandboxPlugins)
                {
                    executor = new SandboxedDialogExecutor(webConfig.MaxPluginExecutionTime, webConfig.FailFastPlugins);
                }
                else
                {
                    executor = new BasicDialogExecutor(webConfig.FailFastPlugins);
                }
#else
            executor = new BasicDialogExecutor(webConfig.FailFastPlugins);
#endif

            // Build dialog http server
            ILogger httpLogger = coreLogger.Clone("DialogHttpServerBase");
            IList<ServerBindingInfo> parsedEndpoints = ServerBindingInfo.ParseBindingList(webConfig.DialogServerEndpoints);
            if (string.Equals(webConfig.HttpImplementation, "listener", StringComparison.OrdinalIgnoreCase))
            {
#if NETFRAMEWORK
                    coreLogger.Log("Initializing HTTP listener server");
                    dialogHttpServer = new ListenerHttpServer(parsedEndpoints, httpLogger, new WeakPointer<IThreadPool>(httpThreadPool));
#else
                throw new PlatformNotSupportedException(".Net Core service does not support HTTP listener server");
#endif
            }
            else if (string.Equals(webConfig.HttpImplementation, "kestrel", StringComparison.OrdinalIgnoreCase))
            {
#if NETCOREAPP
                coreLogger.Log("Initializing HTTP Kestrel server");
                dialogHttpServer = new KestrelHttpServer(parsedEndpoints, httpLogger);
#else
                    throw new PlatformNotSupportedException(".Net Framework service does not support HTTP kestrel server");
#endif
            }
            else
            {
                coreLogger.Log("Initializing HTTP socket server");
                socketServer = new RawTcpSocketServer(parsedEndpoints, httpLogger, realTimeDefinition, new WeakPointer<IMetricCollector>(metrics), coreMetricDimensions, new WeakPointer<IThreadPool>(httpThreadPool));
                dialogHttpServer = new SocketHttpServer(socketServer, httpLogger, new CryptographicRandom(), new WeakPointer<IMetricCollector>(metrics), coreMetricDimensions);
            }

            IAudioCodecFactory codecs = DialogServicesFactory.CreateCodecCollection(coreLogger);

            // Set up SR, TTS, and audio codecs
            string ttsProvider = webConfig.TTSProvider;
            string srProvider = webConfig.SRProvider;
            int speechPoolSize = Math.Max(1, webConfig.SpeechPoolSize);

            ISpeechSynth speechSynth = DialogServicesFactory.TryGetSpeechSynth(
                ttsProvider,
                coreLogger.Clone("DialogTTS"),
                nlpTools,
                webConfig.TTSApiKey,
                new WeakPointer<IMetricCollector>(metrics),
                coreMetricDimensions,
                new WeakPointer<IThreadPool>(workerThreadPool),
                speechPoolSize);
            ISpeechRecognizerFactory speechReco = DialogServicesFactory.TryGetSpeechRecognizer(
                srProvider,
                coreLogger.Clone("DialogSR"),
                webConfig.SRApiKey,
                false,
                new WeakPointer<IMetricCollector>(metrics),
                coreMetricDimensions,
                realTimeDefinition);

            OAuthManager oauthManager = new OAuthManager(
                config.OAuthCallbackUri,
                allServices.OAuthSecretStore,
                new WeakPointer<IMetricCollector>(metrics),
                coreMetricDimensions);

            List<IDialogTransportProtocol> dialogWebProtocols = new List<IDialogTransportProtocol>()
                {
                    new DialogJsonTransportProtocol(),
                    new DialogBondTransportProtocol(),
                    new DialogLZ4JsonTransportProtocol(),
                    new DialogLZ4BondTransportProtocol()
                };

#if NETFRAMEWORK
            ILGScriptCompiler lgScriptCompiler = new CodeDomLGScriptCompiler();
#elif NETCOREAPP
            ILGScriptCompiler lgScriptCompiler = new RoslynLGScriptCompiler();
#endif

            IRemoteDialogProtocol remotingProtocol = null;
            if ("json".Equals(remotingConfig.IpcProtocol, StringComparison.OrdinalIgnoreCase))
            {
                remotingProtocol = new JsonRemoteDialogProtocol();
            }
            else
            {
                remotingProtocol = new BondRemoteDialogProtocol();
            }

            string pluginLoaderType = remotingConfig.PluginLoader;
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
                    new WeakPointer<IThreadPool>(workerThreadPool),
                    speechSynth,
                    speechReco,
                    oauthManager,
                    httpClientFactory,
                    fileSystem,
                    lgScriptCompiler,
                    nlpTools,
                    entityResolver,
                    new WeakPointer<IMetricCollector>(metrics),
                    coreMetricDimensions,
                    serverSocketFactory: null,
                    clientSocketFactory: null,
                    realTime: realTimeDefinition,
                    useDebugTimeouts: Debugger.IsAttached);
            }
#if NETFRAMEWORK
                else if ("containerized".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase) ||
                    "appdomain_isolated".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase))
                {
                    pluginProvider = await AppDomainIsolatedPluginProvider.Create(
                        coreLogger.Clone("ContainerizedPluginProvider"),
                        fileSystem,
                        new DirectoryInfo(rootRuntimeDirectory),
                        httpClientFactory,
                        new DirectoryInfo(Path.Combine(rootRuntimeDirectory, "temp_containers")),
                        entityResolver,
                        speechSynth,
                        speechReco,
                        oauthManager,
                        realTimeDefinition,
                        remotingProtocol,
                        metrics,
                        coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_IPCMethod, remotingConfig.RemotingPipeImplementation)),
                        allServices.ServerSocketFactory,
                        remotingConfig,
                        Debugger.IsAttached);
                }
                else if ("loadcontext_isolated".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase))
                {
                    throw new PlatformNotSupportedException(".Net Framework runtime does not support load context isolation");
                }
#elif NETCOREAPP
            else if ("containerized".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase) ||
                "loadcontext_isolated".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase))
            {
                IServerSocketFactory serverSocketFactory = DialogServicesFactory.CreateServerSocketFactory(remotingConfig.RemotingPipeImplementation, coreLogger);
                pluginProvider = await LoadContextIsolatedPluginProvider.Create(
                    coreLogger.Clone("ContainerizedPluginProvider"),
                    fileSystem,
                    new DirectoryInfo(rootRuntimeDirectory),
                    httpClientFactory,
                    new DirectoryInfo(Path.Combine(rootRuntimeDirectory, "temp_containers")),
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
            else if ("appdomain_isolated".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase))
            {
                throw new PlatformNotSupportedException(".Net Core runtime does not support app domain isolation");
            }
#endif
            else if ("process_isolated".Equals(pluginLoaderType, StringComparison.OrdinalIgnoreCase))
            {
                pluginProvider = await ProcessIsolatedPluginProvider.Create(
                    coreLogger.Clone("ContainerizedPluginProvider"),
                    fileSystem,
                    new DirectoryInfo(rootRuntimeDirectory),
                    httpClientFactory,
                    new DirectoryInfo(Path.Combine(rootRuntimeDirectory, "temp_containers")),
                    entityResolver,
                    speechSynth,
                    speechReco,
                    oauthManager,
                    realTimeDefinition,
                    remotingProtocol,
                    metrics,
                    coreMetricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_IPCMethod, remotingConfig.RemotingPipeImplementation)),
                    allServices.ServerSocketFactory,
                    remotingConfig,
                    DialogRuntimeFramework.RUNTIME_NETFRAMEWORK,
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
                ProcessingThreadPool = new WeakPointer<IThreadPool>(workerThreadPool),
                HttpServer = dialogHttpServer,
                SpeechReco = speechReco,
                SpeechSynth = speechSynth,
                CodecFactory = codecs,
                StreamingAudioCache = allServices.StreamingAudioCache,
                TransportProtocols = dialogWebProtocols,
                Metrics = new WeakPointer<IMetricCollector>(metrics),
                MetricDimensions = coreMetricDimensions,
                MachineHostName = machineHostName,
            };

            dialogWebService = await DialogWebService.Create(dialogWebParams, CancellationToken.None);

            ISet<string> domains = new HashSet<string>();
            foreach (string enabledDomain in webConfig.PluginIdsToLoad)
            {
                domains.Add(enabledDomain);
            }

            await engine.LoadPlugins(domains, realTimeDefinition);

            coreLogger.Log("The dialog server is loaded and ready to process queries on " + string.Join("|", dialogHttpServer.Endpoints));

            Jitter.JitAssembly(coreLogger.Clone("JIT"), Assembly.GetEntryAssembly(), TimeSpan.FromSeconds(10)).Forget(coreLogger);
        }

        public void Dispose()
        {
            engine?.Dispose();
            windowsPerfCounters?.Dispose();
            gcObserver?.Dispose();
            threadPoolObserver?.Dispose();
            mainConfig?.Dispose();
            asyncLoggingFixedThreadPool?.Dispose();
            asyncLoggingThreadPool?.Dispose();
            cryptoRandom?.Dispose();
            aesImpl?.Dispose();
            fileMetricOutput?.Dispose();
            appInsightsMetricOutput?.Dispose();
            luClient?.Dispose();
            workerThreadPool?.Dispose();
            httpThreadPool?.Dispose();
            socketServer?.Dispose();
            pluginProvider?.Dispose();
            dialogWebService?.Dispose();
            dialogHttpClient?.Dispose();
            dialogHttpServer?.Dispose();
            dialogClient?.Dispose();
            socketFactory?.Dispose();
            allServices?.Dispose();
        }

        public async Task Run()
        {
            // Run interactive console, which is the main bulk of the service.
            // If console is disabled, this Run() method will still sit in a loop in case the config changes later.
            using (DialogInteractiveConsole console = new DialogInteractiveConsole(
                coreLogger,
                new WeakPointer<DialogProcessingEngine>(engine),
                httpClientFactory,
                dialogHttpServer.LocalAccessUri,
                mainConfig))
            {
                await console.Run().ConfigureAwait(false);
            }
        }

        private void PrintUnhandledException(object source, UnhandledExceptionEventArgs args)
        {
            try
            {
                ILogger appDomainLogger = APP_DOMAIN_LOGGER;
                if (appDomainLogger != null)
                {
                    object exceptionObject = args.ExceptionObject;
                    if (exceptionObject is Exception)
                    {
                        Exception ex = exceptionObject as Exception;
                        appDomainLogger.Log(ex, LogLevel.Crt);
                    }
                    else
                    {
                        appDomainLogger.Log("A potentially fatal unhandled exception was raised in the AppDomain", LogLevel.Crt);
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
