namespace Durandal.Common.Dialog.Services
{
    using Durandal.API;
        using Durandal.Common.Utils;
    using Durandal.Common.Audio;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.File;
    using Durandal.Common.LG;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.NLP.ApproxString;
    using Durandal.Common.Collections.Indexing;
    using Durandal.Common.Ontology;
    using Durandal.Common.Security.OAuth;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Durandal.Common.Time;
    using System.Threading;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// A collection of helper objects and functions that are provided by the runtime to each individual plugin.
    /// These include a logger and a place for persistent session storage, among others.
    /// </summary>
    public class PluginServices : IPluginServicesInternal
    {
        internal static readonly VirtualPath VIRTUAL_VIEW_DIRECTORY_PATH = new VirtualPath("\\virtual\\views");
        internal static readonly VirtualPath VIRTUAL_PLUGINDATA_DIRECTORY_PATH = new VirtualPath("\\virtual\\plugindata");

        //// These fields can change between queries, and are cloned every time ////

        /// <summary>
        /// The query specific logger that is exposed to the plugin
        /// </summary>
        private readonly ILogger _immutableLogger;

        /// <summary>
        /// The internal query-specific logger
        /// </summary>
        private readonly ILogger _mutableLogger;

        /// <summary>
        /// This contains the transient context of the current conversation. It's typically localized only to the current domain
        /// </summary>
        private readonly InMemoryDataStore _sessionStore = null;

        /// <summary>
        /// This is the profile attached to the local plugin only. It contains persistent data that is sepcific to this user and this plugin.
        /// </summary>
        private readonly InMemoryDataStore _localUserProfile = null;

        /// <summary>
        /// This is the profile that is globally shared by the user, which is readonly to all domains except reflection
        /// </summary>
        private readonly InMemoryDataStore _globalUserProfile = null;
        
        private readonly InMemoryEntityHistory _globalUserHistory = null;

        private readonly IList<ContextualEntity> _contextualEntities = null;

        private readonly KnowledgeContext _entityContext = null;

        private readonly InMemoryDialogActionCache _dialogActionCache = null;

        /// <inheritdoc />
        public InMemoryDialogActionCache DialogActionCache
        {
            get
            {
                return _dialogActionCache;
            }
        }

        private readonly InMemoryWebDataCache _webDataCache = null;

        /// <inheritdoc />
        public InMemoryWebDataCache WebDataCache
        {
            get
            {
                return _webDataCache;
            }
        }

        /// <summary>
        /// Creates http clients that are tied to the current logger instance
        /// </summary>
        private readonly IHttpClientFactory _localHttpClientFactory;

        /// <summary>
        /// This is for constants and persists between clones
        /// </summary>
        private readonly PluginServicesInternal _internalConstants = null;

        /// <summary>
        /// Creates the "global" answer services, using a fixed set of internal constants
        /// </summary>
        public PluginServices(
            PluginStrongName pluginId,
            IConfiguration localConfig,
            ILogger logger,
            IFileSystem globalFileSystem,
            ILGEngine lgTemplate,
            IEntityResolver entityResolver,
            ISpeechSynth tts,
            ISpeechRecognizerFactory sr,
            IOAuthManager authManager,
            IHttpClientFactory httpClientFactory)
        {
            // Create a new filesystem that is a view of the current domain's local files only
            HybridFileSystem pluginVfs = new HybridFileSystem(NullFileSystem.Singleton);
            pluginVfs.AddRoute(VIRTUAL_VIEW_DIRECTORY_PATH, globalFileSystem, new VirtualPath("\\" + RuntimeDirectoryName.VIEW_DIR + "\\" + pluginId.PluginId + " " + pluginId.MajorVersion + "." + pluginId.MinorVersion));
            pluginVfs.AddRoute(VIRTUAL_PLUGINDATA_DIRECTORY_PATH, globalFileSystem, new VirtualPath("\\" + RuntimeDirectoryName.PLUGINDATA_DIR + "\\" + pluginId.PluginId + " " + pluginId.MajorVersion + "." + pluginId.MinorVersion));
            
            _internalConstants = new PluginServicesInternal(
                pluginId,
                localConfig,
                pluginVfs,
                lgTemplate,
                entityResolver,
                tts,
                sr,
                authManager,
                httpClientFactory);
            _mutableLogger = logger.Clone("Plugin-" + pluginId);
            _immutableLogger = _mutableLogger;
            _localHttpClientFactory = new FixedLoggerHttpClientFactory(httpClientFactory, _immutableLogger);
        }

        /// <summary>
        /// Creates a per-query answer services (called by the clone() method)
        /// </summary>
        /// <param name="sharedData"></param>
        /// <param name="mutableLogger"></param>
        /// <param name="sessionStore"></param>
        /// <param name="userProfiles"></param>
        /// <param name="entityContext"></param>
        /// <param name="contextualEntities"></param>
        /// <param name="dialogActionCache"></param>
        /// <param name="webDataCache"></param>
        private PluginServices(
            PluginServicesInternal sharedData,
            ILogger mutableLogger,
            InMemoryDataStore sessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            InMemoryDialogActionCache dialogActionCache,
            InMemoryWebDataCache webDataCache)
        {
            _internalConstants = sharedData;
            _mutableLogger = mutableLogger;
            _immutableLogger = new ImmutableLogger(mutableLogger);
            _sessionStore = sessionStore;
            if (userProfiles != null)
            {
                _localUserProfile = userProfiles.LocalProfile;
                _globalUserProfile = userProfiles.GlobalProfile;
                _globalUserHistory = userProfiles.EntityHistory;
            }
            else
            {
                _localUserProfile = null;
                _globalUserProfile = null;
                _globalUserHistory = null;
            }

            _contextualEntities = contextualEntities;
            _localHttpClientFactory = new FixedLoggerHttpClientFactory(sharedData._globalHttpClientFactory, _immutableLogger);
            _entityContext = entityContext;
            _dialogActionCache = dialogActionCache;
            _webDataCache = webDataCache;
        }

        /// <summary>
        /// Returns a clone of this IPluginServices object with new query-specific parameters
        /// </summary>
        /// <param name="traceId"></param>
        /// <param name="queryLogger"></param>
        /// <param name="sessionStore"></param>
        /// <param name="userProfiles"></param>
        /// <param name="entityContext"></param>
        /// <param name="contextualEntities"></param>
        /// <param name="dialogActionCache"></param>
        /// <param name="webDataCache"></param>
        /// <returns></returns>
        public PluginServices Clone(
            Guid? traceId,
            ILogger queryLogger,
            InMemoryDataStore sessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            InMemoryDialogActionCache dialogActionCache,
            InMemoryWebDataCache webDataCache)
        {
            ILogger mutableLogger = queryLogger.Clone("Plugin-" + _internalConstants._pluginId.PluginId).CreateTraceLogger(traceId);
            
            return new PluginServices(_internalConstants,
                mutableLogger,
                sessionStore,
                userProfiles,
                //realtimeCallback,
                entityContext,
                contextualEntities,
                dialogActionCache,
                webDataCache);
        }

        /// <inheritdoc />
        public ILogger Logger
        {
            get
            {
                return _immutableLogger;
            }
        }

        /// <inheritdoc />
        public ISpeechSynth TTSEngine
        {
            get
            {
                return _internalConstants._speechSynth.Value;
            }
        }

        /// <inheritdoc />
        public ISpeechRecognizerFactory SpeechRecoEngine
        {
            get
            {
                return _internalConstants._speechReco.Value;
            }
        }

        /// <inheritdoc />
        public IEntityResolver EntityResolver
        {
            get
            {
                return _internalConstants._entityResolver;
            }
        }

        /// <inheritdoc />
        public InMemoryDataStore SessionStore
        {
            get { return _sessionStore; }
        }

        /// <inheritdoc />
        public InMemoryDataStore LocalUserProfile
        {
            get { return _localUserProfile; }
        }

        /// <inheritdoc />
        public InMemoryDataStore GlobalUserProfile
        {
            get { return _globalUserProfile; }
        }

        /// <inheritdoc />
        public IEntityHistory EntityHistory
        {
            get { return _globalUserHistory; }
        }

        /// <inheritdoc />
        public IList<ContextualEntity> ContextualEntities
        {
            get { return _contextualEntities; }
        }

        /// <inheritdoc />
        public KnowledgeContext EntityContext
        {
            get { return _entityContext; }
        }

        /// <inheritdoc />
        public IFileSystem FileSystem
        {
            get { return _internalConstants._fileSystem; }
        }

        /// <inheritdoc />
        public ILGEngine LanguageGenerator
        {
            get { return _internalConstants._lgEngine; }
        }

        /// <inheritdoc />
        public Task<Uri> CreateOAuthUri(OAuthConfig authConfig, string userId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _internalConstants._oauthManager.CreateAuthUri(userId, _internalConstants._pluginId, authConfig, _immutableLogger, cancelToken, realTime);
        }

        /// <inheritdoc />
        public Task<OAuthToken> TryGetOAuthToken(OAuthConfig oauthConfig, string userId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _internalConstants._oauthManager.GetToken(userId, _internalConstants._pluginId, oauthConfig, _immutableLogger, cancelToken, realTime);
        }

        /// <inheritdoc />
        public Task DeleteOAuthToken(OAuthConfig oauthConfig, string userId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _internalConstants._oauthManager.DeleteToken(userId, _internalConstants._pluginId, oauthConfig, _immutableLogger, cancelToken, realTime);
        }

        /// <inheritdoc />
        public string RegisterDialogAction(DialogAction action, TimeSpan? lifeTime = null)
        {
            return _dialogActionCache.Store(action, lifeTime);
        }

        /// <inheritdoc />
        public string RegisterDialogActionUrl(DialogAction action, string clientId, TimeSpan? lifeTime = null)
        {
            string cachedActionKey = RegisterDialogAction(action, lifeTime);
            return string.Format("/action?key={0}&client={1}", cachedActionKey, clientId);
        }

        /// <inheritdoc />
        public string CreateTemporaryWebResource(ArraySegment<byte> data, string mimeType, TimeSpan? lifetime = null)
        {
            if (lifetime.HasValue && lifetime < TimeSpan.FromSeconds(10))
            {
                // Enforce minimum of 10 seconds in cache
                lifetime = TimeSpan.FromSeconds(10);
            }
            else if (!lifetime.HasValue)
            {
                // Fill in default value of 60 seconds if not supplied
                lifetime = TimeSpan.FromSeconds(60);
            }

            CachedWebData cacheItem = new CachedWebData(data, mimeType, TraceId);
            cacheItem.LifetimeSeconds = (int)lifetime.Value.TotalSeconds;
            if (TraceId.HasValue)
            {
                return string.Format("/cache?data={0}&trace={1}", _webDataCache.Store(cacheItem, lifetime), CommonInstrumentation.FormatTraceId(TraceId));
            }
            else
            {
                return string.Format("/cache?data={0}", _webDataCache.Store(cacheItem, lifetime));
            }
        }

        /// <inheritdoc />
        public IHttpClientFactory HttpClientFactory
        {
            get
            {
                return _localHttpClientFactory;
            }
        }

        /// <inheritdoc />
        public IConfiguration PluginConfiguration
        {
            get { return _internalConstants._localConfig.Value; }
        }

        /// <inheritdoc />
        public VirtualPath PluginViewDirectory
        {
            get
            {
                return VIRTUAL_VIEW_DIRECTORY_PATH;
            }
        }

        /// <inheritdoc />
        public VirtualPath PluginDataDirectory
        {
            get
            {
                return VIRTUAL_PLUGINDATA_DIRECTORY_PATH;
            }
        }

        /// <inheritdoc />
        public Guid? TraceId
        {
            get
            {
                if (_immutableLogger != null)
                {
                    return _immutableLogger.TraceId;
                }

                return null;
            }
        }

        //// Always constant for all queries to a particular answer ////
        private class PluginServicesInternal
        {
            public readonly WeakPointer<IConfiguration> _localConfig;
            public readonly PluginStrongName _pluginId;
            public readonly IFileSystem _fileSystem;
            public readonly ILGEngine _lgEngine;
            public readonly IEntityResolver _entityResolver;
            public readonly WeakPointer<ISpeechSynth> _speechSynth;
            public readonly WeakPointer<ISpeechRecognizerFactory> _speechReco;
            public readonly IOAuthManager _oauthManager;
            public readonly IHttpClientFactory _globalHttpClientFactory;

            public PluginServicesInternal(PluginStrongName pluginId,
                IConfiguration localConfig,
                IFileSystem globalFileSystem,
                ILGEngine lgTemplate,
                IEntityResolver entityResolver,
                ISpeechSynth tts,
                ISpeechRecognizerFactory sr,
                IOAuthManager authManager,
                IHttpClientFactory globalHttpClientFactory)
            {
                _pluginId = pluginId;
                _localConfig = new WeakPointer<IConfiguration>(localConfig);
                _fileSystem = globalFileSystem;
                _lgEngine = lgTemplate;
                _entityResolver = entityResolver;
                _speechSynth = new WeakPointer<ISpeechSynth>(tts);
                _speechReco = new WeakPointer<ISpeechRecognizerFactory>(sr);
                _oauthManager = authManager;
                _globalHttpClientFactory = globalHttpClientFactory;
            }
        }
    }
}
