using Durandal;
using Durandal.API;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.LU;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP.Alignment;
using Durandal.Common.NLP.Language;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Packages;
using Durandal.Common.Security;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Speech.SR;
using Durandal.Common.Cache;
using Durandal.Common.File;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.NLP;
using Durandal.Common.Security.Server;
using System.Threading;
using Durandal.Common.Ontology;
using Durandal.Common.Config;
using Durandal.Common.Instrumentation;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Remoting;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Utils;
using Durandal.Common.Audio;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Audio.Codecs.Opus;

namespace Durandal.Common.Test
{
    /// <summary>
    /// This class creates and manages a virtual LU and Dialog runtime environment, which loads a set of
    /// plugin package files, trains LU and LG, then provides an IDialogClient which tests can use to drive
    /// dialog interactions with one or more plugins.
    /// </summary>
    public class InqueTestDriver : IDisposable
    {
        /// Common Infrastructure (virtual)
        private InMemoryConfiguration _configBase;
        private ILogger _serviceLogger;
        private BasicPluginLoader _mockPluginLoader;
        private IDurandalPluginProvider _mockPluginProvider;
        private IFileSystem _mockFileSystem;
        private InMemoryCache<DialogAction> _mockDialogActionCache;
        private InMemoryCache<CachedWebData> _mockWebDataCache;
        private InMemoryConversationStateCache _mockConversationStateCache;
        private InMemoryCache<ClientContext> _mockClientContextCache;
        private InMemoryPublicKeyStore _publicKeyStore;
        private InMemoryProfileStorage _userProfileStorage;
        private FakeOAuthSecretStore _oauthSecretStore;
        private IRealTimeProvider _realTime;
        private MetricCollector _metrics;

        // LU
        private LanguageUnderstandingEngine _luEngine;
        private ILUTransportProtocol _luTransportProtocol;
        private LUHttpServer _luHttpServer;

        // Dialog
        private DialogConfiguration _dialogConfig;
        private DialogEngineParameters _dialogParams;
        private DialogWebParameters _dialogWebParams;
        private DialogProcessingEngine _dialogCore;
        private DialogWebConfiguration _dialogWebConfig;
        private DialogWebService _dialogWebService;
        private RemotingConfiguration _remotingConfig;
        private NullHttpServer _dialogHttpServer;
        private DirectHttpClient _dialogHttpClient;
        private FakeSpeechRecognizerFactory _mockSpeechReco;
        private FakeSpeechSynth _mockSpeechSynth;
        private IDialogClient _defaultClient;
        private OAuthManager _authManager;
        private ILGScriptCompiler _lgScriptCompiler;
        private IDialogTransportProtocol _dialogTransportProtocol;
        private NullHttpServer _nullServer;

        // Internal test driver stuff
        private IFileSystem _cacheFileSystem;
        private InqueTestParameters _testConfig;
        private static readonly VirtualPath CHECKSUM_FILE_PATH = new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\de_checksums.ini");
        private int _disposed = 0;

        public delegate Task<IDurandalPluginProvider> BuildPluginProviderDelegate(
            ILogger logger,
            IDurandalPluginLoader loader,
            IFileSystem fileSystem,
            INLPToolsCollection nlTools,
            IEntityResolver entityResolver,
            ISpeechSynth speechSynth,
            ISpeechRecognizerFactory speechReco,
            IOAuthManager oauthManager,
            IHttpClientFactory httpClientFactory,
            ILGScriptCompiler lgScriptCompiler,
            IRealTimeProvider realTime,
            RemotingConfiguration remotingConfig);

        public InqueTestDriver(InqueTestParameters testConfig)
        {
            _testConfig = testConfig;
            _serviceLogger = _testConfig.Logger;
            _realTime = _testConfig.TimeProvider;
            _configBase = new InMemoryConfiguration(_serviceLogger.Clone("Config"));
            _nullServer = new NullHttpServer();
            _mockFileSystem = new InMemoryFileSystem();
            _mockDialogActionCache = new InMemoryCache<DialogAction>();
            _mockWebDataCache = new InMemoryCache<CachedWebData>();
            _mockConversationStateCache = new InMemoryConversationStateCache();
            _mockClientContextCache = new InMemoryCache<ClientContext>();
            _publicKeyStore = new InMemoryPublicKeyStore(false);
            _mockSpeechReco = new FakeSpeechRecognizerFactory(AudioSampleFormat.Mono(16000), false);
            _mockSpeechSynth = new FakeSpeechSynth(LanguageCode.EN_US);
            _userProfileStorage = new InMemoryProfileStorage();
            _metrics = new MetricCollector(_serviceLogger, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), _testConfig.TimeProvider);
            _oauthSecretStore = new FakeOAuthSecretStore();
            _authManager = new OAuthManager(
                "https://localhost",
                _oauthSecretStore,
                new WeakPointer<IMetricCollector>(_metrics),
                DimensionSet.Empty,
                new DirectHttpClientFactory(_nullServer));
            _lgScriptCompiler = _testConfig.LGScriptCompiler;
            _cacheFileSystem = _testConfig.CacheFileSystem;
            _dialogTransportProtocol = _testConfig.DialogTransportProtocol;
            _luTransportProtocol = _testConfig.LUTransportProtocol; 
            _mockPluginLoader = new BasicPluginLoader(new BasicDialogExecutor(true), _mockFileSystem);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public static Task<IDurandalPluginProvider> BuildLocallyRemotedPluginProvider(
            ILogger logger,
            IDurandalPluginLoader loader,
            IFileSystem fileSystem,
            INLPToolsCollection nlTools,
            IEntityResolver entityResolver,
            ISpeechSynth speechSynth,
            ISpeechRecognizerFactory speechReco,
            IOAuthManager oauthManager,
            IHttpClientFactory httpClientFactory,
            ILGScriptCompiler lgScriptCompiler,
            IRealTimeProvider realTime,
            RemotingConfiguration remotingConfig)
        {
            return Task.FromResult<IDurandalPluginProvider>(
                new LocallyRemotedPluginProvider(
                    logger,
                    loader,
                    new JsonRemoteDialogProtocol(),
                    remotingConfig,
                    new WeakPointer<IThreadPool>(new TaskThreadPool()),
                    speechSynth,
                    speechReco,
                    oauthManager,
                    httpClientFactory,
                    fileSystem,
                    lgScriptCompiler,
                    nlTools,
                    entityResolver,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    serverSocketFactory: null,
                    clientSocketFactory: null,
                    realTime: realTime,
                    useDebugTimeouts: true));
        }

        public static Task<IDurandalPluginProvider> BuildDefaultPluginProvider(
            ILogger logger,
            IDurandalPluginLoader loader,
            IFileSystem fileSystem,
            INLPToolsCollection nlTools,
            IEntityResolver entityResolver,
            ISpeechSynth speechSynth,
            ISpeechRecognizerFactory speechReco,
            IOAuthManager oauthManager,
            IHttpClientFactory httpClientFactory,
            ILGScriptCompiler lgScriptCompiler,
            IRealTimeProvider realTime,
            RemotingConfiguration remotingConfig)
        {
            return Task.FromResult<IDurandalPluginProvider>(new MachineLocalPluginProvider(
                logger,
                loader,
                fileSystem,
                nlTools,
                entityResolver,
                speechSynth,
                speechReco,
                oauthManager,
                httpClientFactory,
                lgScriptCompiler));
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~InqueTestDriver()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Interface from client to dialog engine
        /// </summary>
        public IDialogClient Client
        {
            get
            {
                return _defaultClient;
            }
        }

        /// <summary>
        /// HTTP interface to dialog web service
        /// </summary>
        public IHttpClient HttpClient
        {
            get
            {
                return _dialogHttpClient;
            }
        }

        /// <summary>
        /// The local storage for client RSA keys
        /// </summary>
        public InMemoryPublicKeyStore PublicKeyStorage
        {
            get
            {
                return _publicKeyStore;
            }
        }

        /// <summary>
        /// The local storage for global and local user profiles
        /// </summary>
        public InMemoryProfileStorage UserProfileStorage
        {
            get
            {
                return _userProfileStorage;
            }
        }

        /// <summary>
        /// The collector where LU + dialog metrics are being reported.
        /// </summary>
        public MetricCollector MetricCollector
        {
            get
            {
                return _metrics;
            }
        }

        /// <summary>
        /// The oauth token that will be used by dialog engine
        /// </summary>
        public OAuthToken MockOAuthToken
        {
            set
            {
                _oauthSecretStore.SetMockToken(value);
            }
        }

        public async Task InjectEntityIntoGlobalContext(Entity e)
        {
            RetrieveResult<UserProfileCollection> profilesResult = await _userProfileStorage.GetProfiles(
                UserProfileType.EntityHistoryGlobal, DialogTestHelpers.TEST_USER_ID, null, _serviceLogger).ConfigureAwait(false);
            UserProfileCollection profiles = profilesResult.Result;
            if (profiles.EntityHistory == null)
            {
                profiles.EntityHistory = new InMemoryEntityHistory();
            }

            profiles.EntityHistory.AddOrUpdateEntity(e);
            await _userProfileStorage.UpdateProfiles(
                UserProfileType.EntityHistoryGlobal, profiles, DialogTestHelpers.TEST_USER_ID, null, _serviceLogger).ConfigureAwait(false);
        }

        public async Task Initialize()
        {
            try
            {
                ISet<PluginStrongName> pluginIds = new HashSet<PluginStrongName>();
                foreach (DurandalPlugin plugin in _testConfig.Plugins)
                {
                    _mockPluginLoader.RegisterPluginType(plugin);
                    pluginIds.Add(plugin.GetStrongName());
                }

                IPackageLoader packageLoader = new PortableZipPackageFileLoader(_serviceLogger);

                // Load all .dupkg files into the virtual filesystem so that plugins have access to their LG and plugindata files
                // newChecksums will contain the data checksum of each dialog domain
                using (ChecksumFile newChecksums = await ChecksumFile.Create(_mockFileSystem, CHECKSUM_FILE_PATH, _realTime).ConfigureAwait(false))
                {
                    // fakeChecksums is just so the package installer has somewhere to put its overall package-level sums, which we ignore
                    using (ChecksumFile fakeChecksums = await ChecksumFile.Create(_mockFileSystem, new VirtualPath("fake_checksums.ini"), _realTime).ConfigureAwait(false))
                    {
                        if (_testConfig.PackageFiles != null)
                        {
                            foreach (VirtualPath packageFile in _testConfig.PackageFiles)
                            {
                                if (!_testConfig.PackageFileSystem.Exists(packageFile))
                                {
                                    _serviceLogger.Log("Package file " + packageFile.FullName + " does not exist!", LogLevel.Wrn);
                                    continue;
                                }

                                PackageFile package = await PackageFile.Load(
                                    _testConfig.PackageFileSystem,
                                    packageFile,
                                    packageLoader,
                                    _serviceLogger.Clone("PackageInstaller")).ConfigureAwait(false);

                                // Install the package file into an in-memory filesystem
                                await package.InstallDialog(_mockFileSystem, _serviceLogger.Clone("PackageInstaller"), fakeChecksums).ConfigureAwait(false);

                                // Also copy the package file itself - containerized runtimes will attempt to install the package on their own since 
                                // they rely on the files being physically present on the hard drive somewhere
                                FileHelpers.CopyFile(_testConfig.PackageFileSystem, packageFile, _mockFileSystem, new VirtualPath(RuntimeDirectoryName.PACKAGE_DIR).Combine(packageFile.Name), _serviceLogger);
                                PackageManifest manifest = package.GetManifest();
                                foreach (DEManifestEntry manifestEntry in manifest.DialogComponents)
                                {
                                    newChecksums.SetValue(manifestEntry.PluginIdString, manifestEntry.DataChecksum);
                                }

                                _serviceLogger.Log("Successfully installed package " + packageFile.Name);
                            }
                        }
                    }

                    // Load precached values from previous tests if applicable
                    await LoadCacheFilesBeforeInitialize(_mockFileSystem, _cacheFileSystem, newChecksums).ConfigureAwait(false);
                }

                NLPTools englishNLP = new NLPTools()
                {
                    LGFeatureExtractor = new EnglishLGFeatureExtractor(),
                    EditDistance = EditDistanceDoubleMetaphone.Calculate,
                    FeaturizationWordBreaker = new EnglishWordBreaker(),
                    WordBreaker = new EnglishWholeWordBreaker(),
                    CultureInfoFactory = new BasicCultureInfoFactory(_serviceLogger.Clone("CultureInfoFactory"))
                };

                NLPToolsCollection nlTools = new NLPToolsCollection();
                nlTools.Add(LanguageCode.EN_US, englishNLP);
                GenericEntityResolver genericEntityResolver = new GenericEntityResolver(nlTools, new InMemoryCache<byte[]>());
                DefaultEntityResolver entityResolver = new DefaultEntityResolver(genericEntityResolver);

                _remotingConfig = DialogTestHelpers.GetTestRemotingConfiguration(new WeakPointer<IConfiguration>(_configBase));

                _mockPluginProvider = await _testConfig.PluginProviderFactory(
                    _serviceLogger.Clone("PluginProvider"),
                    _mockPluginLoader,
                    _mockFileSystem,
                    nlTools,
                    entityResolver,
                    _mockSpeechSynth,
                    _mockSpeechReco,
                    _authManager,
                    new NullHttpClientFactory(),
                    _lgScriptCompiler,
                    _realTime,
                    _remotingConfig).ConfigureAwait(false);

                _dialogConfig = DialogTestHelpers.GetTestDialogConfiguration(new WeakPointer<IConfiguration>(_configBase));
                _dialogConfig.IgnoreSideSpeech = string.IsNullOrEmpty(_testConfig.SideSpeechDomain) ||
                    string.Equals(_testConfig.SideSpeechDomain, DialogConstants.SIDE_SPEECH_DOMAIN);
                _dialogConfig.AssumePluginResponsesArePII = false;
                _dialogParams = new DialogEngineParameters(_dialogConfig, new WeakPointer<IDurandalPluginProvider>(_mockPluginProvider))
                {
                    Logger = _serviceLogger.Clone("DialogEngine"),
                    ConversationStateCache = new WeakPointer<IConversationStateCache>(_mockConversationStateCache),
                    UserProfileStorage = _userProfileStorage,
                    CommonDomainName = DialogConstants.COMMON_DOMAIN,
                    SideSpeechDomainName = _testConfig.SideSpeechDomain,
                    DialogActionCache = new WeakPointer<ICache<DialogAction>>(_mockDialogActionCache),
                    WebDataCache = new WeakPointer<ICache<CachedWebData>>(_mockWebDataCache),
                    RealTime = _realTime
                };

                _dialogCore = new DialogProcessingEngine(_dialogParams);

                await _dialogCore.LoadPlugins(pluginIds, _realTime).ConfigureAwait(false);

                _dialogHttpServer = new NullHttpServer();
                _dialogHttpClient = new DirectHttpClient(_dialogHttpServer);

                List<IDialogTransportProtocol> dialogServerTransport = new List<IDialogTransportProtocol>()
                {
                    _dialogTransportProtocol
                };

                _dialogWebConfig = DialogTestHelpers.GetTestDialogWebConfiguration(new WeakPointer<IConfiguration>(_configBase));
                _defaultClient = new DialogHttpClient(_dialogHttpClient, _serviceLogger.Clone("DialogHttp"), _dialogTransportProtocol);

                _luEngine = await DialogTestHelpers.BuildLUEngine(_serviceLogger.Clone("LU"), _testConfig.FakeLUModels, _realTime).ConfigureAwait(false);

                List<ILUTransportProtocol> luServerTransport = new List<ILUTransportProtocol>()
                {
                    _luTransportProtocol
                };

                InMemoryConfiguration luServerConfig = new InMemoryConfiguration(_serviceLogger.Clone("LuHttpConfig"));
                _luHttpServer = new LUHttpServer(
                    _luEngine,
                    luServerConfig,
                    new NullHttpServer(),
                    _serviceLogger.Clone("LuHttpServer"),
                    luServerTransport,
                    NullFileSystem.Singleton,
                    new InMemoryPackageFileLoader(),
                    _metrics,
                    DimensionSet.Empty,
                    "InqueTest");

                await _luHttpServer.StartServer("LUHttp", CancellationToken.None, _realTime).ConfigureAwait(false);
                ILUClient luHttpClient = new LUHttpClient(new DirectHttpClient(_luHttpServer), _serviceLogger.Clone("LUHttpClient"), _luTransportProtocol);

                _dialogWebParams = new DialogWebParameters(_dialogWebConfig, new WeakPointer<DialogProcessingEngine>(_dialogCore))
                {
                    Logger = _serviceLogger.Clone("DialogWebService"),
                    FileSystem = _mockFileSystem,
                    LuConnection = luHttpClient,
                    DialogActionStore = new WeakPointer<ICache<DialogAction>>(_mockDialogActionCache),
                    ConversationStateCache = _mockConversationStateCache,
                    WebDataCache = new WeakPointer<ICache<CachedWebData>>(_mockWebDataCache),
                    ClientContextCache = new WeakPointer<ICache<ClientContext>>(_mockClientContextCache),
                    HttpServer = _dialogHttpServer,
                    PublicKeyStorage = _publicKeyStore,
                    ProcessingThreadPool = new WeakPointer<IThreadPool>(
                        new TaskThreadPool(new WeakPointer<IMetricCollector>(_metrics), DimensionSet.Empty, "DialogWeb")),
                    Metrics = new WeakPointer<IMetricCollector>(_metrics),
                    MetricDimensions = DimensionSet.Empty,
                    CodecFactory = new AggregateCodecFactory(new RawPcmCodecFactory(), new OpusRawCodecFactory(_serviceLogger.Clone("OpusCodec"))),
                    SpeechReco = _mockSpeechReco,
                    SpeechSynth = _mockSpeechSynth,
                    RealTimeProvider = _realTime,
                    TransportProtocols = dialogServerTransport,
                    MachineHostName = "InqueTest"
                };

                _dialogWebService = await DialogWebService.Create(_dialogWebParams, CancellationToken.None).ConfigureAwait(false);

                // Copy cache files from the virtual filesystem to the external build directory to speed up subsequent tests
                await PersistCacheFilesAfterInitialize(_mockFileSystem, _cacheFileSystem).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _serviceLogger.Log(e, LogLevel.Err);
                throw;
            }
        }

        public void ResetState()
        {
            _serviceLogger.Log("=== Inque test driver is resetting state ===");
            _defaultClient.ResetConversationState(DialogTestHelpers.TEST_USER_ID, DialogTestHelpers.TEST_CLIENT_ID, _serviceLogger, CancellationToken.None, _realTime);
            _mockDialogActionCache.Clear();
            _mockWebDataCache.Clear();
            _mockConversationStateCache.ClearAllConversationStates();
            _mockClientContextCache.Clear();
            _publicKeyStore.ClearAllClients();
            _userProfileStorage.ClearAllProfiles();
            _mockSpeechReco.ClearRecoResults();
            _metrics.Reset();

            _oauthSecretStore.SetMockToken(new OAuthToken()
            {
                Token = "mock_oauth_token",
                ExpiresAt = _realTime.Time.AddDays(100),
                IssuedAt = DateTimeOffset.MinValue,
                RefreshToken = "mock_refresh_token",
                TokenType = "bearer"
            });
        }

        /// <summary>
        /// Instructs dialog engine to trust the test client with Client scope
        /// </summary>
        /// <param name="clientId"></param>
        public void SetClientAsTrusted(string clientId)
        {
            _publicKeyStore.PromoteClient(new ClientKeyIdentifier(ClientAuthenticationScope.Client, clientId: clientId));
        }

        /// <summary>
        /// Instructs dialog engine to trust the test client with User scope
        /// </summary>
        /// <param name="userId"></param>
        public void SetUserAsTrusted(string userId)
        {
            _publicKeyStore.PromoteClient(new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: userId));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _serviceLogger.Log("=== Inque test driver is shutting down ===");
                _dialogWebService?.Dispose();
                _dialogCore?.Dispose();
                _luEngine?.Dispose();
                _dialogHttpClient?.Dispose();
                _dialogHttpServer?.Dispose();
                _luHttpServer?.Dispose();
                _mockSpeechReco?.Dispose();
                _mockSpeechSynth?.Dispose();
                _nullServer?.Dispose();
                _metrics?.Dispose();
                _oauthSecretStore?.Dispose();
                _defaultClient?.Dispose();
                _mockConversationStateCache?.Dispose();
                _mockWebDataCache?.Dispose();
                _mockDialogActionCache?.Dispose();
                _mockClientContextCache?.Dispose();
                _configBase?.Dispose();
            }
        }

        private async Task LoadCacheFilesBeforeInitialize(IFileSystem virtualFilesystem, IFileSystem realFilesystem, ChecksumFile updatedChecksums)
        {
            // Read the checksum file first
            bool checksumsChanged = false;
            if (realFilesystem.Exists(CHECKSUM_FILE_PATH))
            {
                using (ChecksumFile previousChecksums = await ChecksumFile.Create(realFilesystem, CHECKSUM_FILE_PATH, _realTime).ConfigureAwait(false))
                {
                    foreach (string key in updatedChecksums.Keys)
                    {
                        int? oldSum = previousChecksums.GetValue(key);
                        int? newSum = updatedChecksums.GetValue(key);
                        if (!newSum.HasValue || (oldSum.HasValue && oldSum.Value != newSum.Value))
                        {
                            checksumsChanged = true;
                        }
                    }
                }
            }

            // If any plugin data checksums have changed, do not load the cache files into virtual memory
            // This is mainly to target cached LG models which take the longest time to load the first time in dialog
            if (checksumsChanged)
            {
                return;
            }

            VirtualPath sourceDir = new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\en-US");
            if (realFilesystem.Exists(sourceDir))
            {
                foreach (VirtualPath cacheFile in realFilesystem.ListFiles(sourceDir))
                {
                    using (Stream readStream = realFilesystem.OpenStream(cacheFile, FileOpenMode.Open, FileAccessMode.Read))
                    {
                        using (Stream writeStream = await virtualFilesystem.OpenStreamAsync(cacheFile, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false))
                        {
                            await readStream.CopyToAsync(writeStream).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        private async Task PersistCacheFilesAfterInitialize(IFileSystem virtualFilesystem, IFileSystem realFilesystem)
        {
            VirtualPath cacheDir = new VirtualPath(RuntimeDirectoryName.CACHE_DIR + "\\en-US");

            // Clear the target dir first
            if (realFilesystem.Exists(cacheDir))
            {
                foreach (VirtualPath cacheFile in realFilesystem.ListFiles(cacheDir))
                {
                    await realFilesystem.DeleteAsync(cacheFile).ConfigureAwait(false);
                }
            }

            // Copy virtual cache to disk cache
            if (await virtualFilesystem.ExistsAsync(cacheDir).ConfigureAwait(false))
            {
                foreach (VirtualPath cacheFile in virtualFilesystem.ListFiles(cacheDir))
                {
                    using (Stream readStream = virtualFilesystem.OpenStream(cacheFile, FileOpenMode.Open, FileAccessMode.Read))
                    {
                        using (Stream writeStream = await realFilesystem.OpenStreamAsync(cacheFile, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false))
                        {
                            await readStream.CopyToAsync(writeStream).ConfigureAwait(false);
                        }
                    }
                }
            }

            if (virtualFilesystem.Exists(CHECKSUM_FILE_PATH))
            {
                using (Stream readStream = virtualFilesystem.OpenStream(CHECKSUM_FILE_PATH, FileOpenMode.Open, FileAccessMode.Read))
                {
                    using (Stream writeStream = await realFilesystem.OpenStreamAsync(CHECKSUM_FILE_PATH, FileOpenMode.Create, FileAccessMode.Write).ConfigureAwait(false))
                    {
                        await readStream.CopyToAsync(writeStream).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
