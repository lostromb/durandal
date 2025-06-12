namespace Durandal.Common.Remoting.Proxies
{
    using Durandal.API;
    using Durandal.Common.Config;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.LG;
    using Durandal.Common.Logger;
    using Durandal.Common.Net.Http;
    using Durandal.Common.Ontology;
    using Durandal.Common.ServiceMgmt;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Test;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Implementation of IPluginServices specifically designed for remoted scenarios.
    /// In this context, many of the service providers are transient remoted objects which we need to dispose of.
    /// </summary>
    public class RemotedPluginServices : IPluginServicesInternal, IDisposable
    {
        private int _disposed = 0;

        public RemotedPluginServices(
            PluginStrongName pluginId,
            IFileSystem fileSystem,
            ILGEngine languageGenerator,
            IConfiguration pluginConfiguration,
            IOAuthManager oauthManager,
            InMemoryDataStore sessionStore,
            InMemoryDataStore localUserProfile,
            InMemoryDataStore globalUserProfile,
            IHttpClientFactory httpClientFactory,
            ILogger logger,
            Guid? traceId,
            ISpeechRecognizerFactory speechRecoEngine,
            ISpeechSynth ttsEngine,
            IList<ContextualEntity> contextualEntities,
            KnowledgeContext entityContext,
            IEntityHistory entityHistory,
            IEntityResolver entityResolver,
            FakeOAuthSecretStore fakeOauthStore)
        {
            PluginId = pluginId;
            Logger = new ImmutableLogger(logger.Clone("Plugin-" + pluginId));

            HybridFileSystem pluginVfs = new HybridFileSystem(NullFileSystem.Singleton);
            pluginVfs.AddRoute(PluginServices.VIRTUAL_VIEW_DIRECTORY_PATH, fileSystem, new VirtualPath("\\" + RuntimeDirectoryName.VIEW_DIR + "\\" + pluginId.PluginId + " " + pluginId.MajorVersion + "." + pluginId.MinorVersion));
            pluginVfs.AddRoute(PluginServices.VIRTUAL_PLUGINDATA_DIRECTORY_PATH, fileSystem, new VirtualPath("\\" + RuntimeDirectoryName.PLUGINDATA_DIR + "\\" + pluginId.PluginId + " " + pluginId.MajorVersion + "." + pluginId.MinorVersion));
            FileSystem = pluginVfs;

            LanguageGenerator = languageGenerator;
            PluginConfiguration = pluginConfiguration;
            OAuthManager = oauthManager;
            DialogActionCache = new InMemoryDialogActionCache();
            WebDataCache = new InMemoryWebDataCache();
            SessionStore = sessionStore;
            LocalUserProfile = localUserProfile;
            GlobalUserProfile = globalUserProfile;
            HttpClientFactory = new FixedLoggerHttpClientFactory(httpClientFactory, Logger);
            TraceId = traceId;
            SpeechRecoEngine = speechRecoEngine;
            TTSEngine = ttsEngine;
            ContextualEntities = contextualEntities;
            EntityContext = entityContext;
            EntityHistory = entityHistory;
            EntityResolver = entityResolver;
            _oauthSecretStore = fakeOauthStore;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RemotedPluginServices()
        {
            Dispose(false);
        }
#endif

        // Constant
        public PluginStrongName PluginId { get; private set; }

        // Constant
        public IFileSystem FileSystem { get; private set; }

        // Constant
        public ILGEngine LanguageGenerator { get; private set; }

        // Constant
        public IConfiguration PluginConfiguration { get; private set; }

        // "Internal" constructs not directly exposed to plugins
        public IOAuthManager OAuthManager { get; private set; }
        public InMemoryDialogActionCache DialogActionCache { get; private set; }
        public InMemoryWebDataCache WebDataCache { get; private set; }

        // Extra disposable things that this object needs to take ownership of

        private FakeOAuthSecretStore _oauthSecretStore;

        // Everything below this is instanced per-request
        public InMemoryDataStore SessionStore { get; private set; }

        public InMemoryDataStore LocalUserProfile { get; private set; }

        public InMemoryDataStore GlobalUserProfile { get; private set; }

        public IHttpClientFactory HttpClientFactory { get; private set; }

        public ILogger Logger { get; private set; }

        public VirtualPath PluginDataDirectory => PluginServices.VIRTUAL_PLUGINDATA_DIRECTORY_PATH;

        public VirtualPath PluginViewDirectory => PluginServices.VIRTUAL_VIEW_DIRECTORY_PATH;

        public Guid? TraceId { get; private set; }

        public ISpeechRecognizerFactory SpeechRecoEngine { get; private set; }

        public ISpeechSynth TTSEngine { get; private set; }

        public IList<ContextualEntity> ContextualEntities { get; private set; }

        public KnowledgeContext EntityContext { get; private set; }

        public IEntityHistory EntityHistory { get; private set; }

        public IEntityResolver EntityResolver { get; private set; }

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
                return string.Format("/cache?data={0}&trace={1}", WebDataCache.Store(cacheItem, lifetime), TraceId);
            }
            else
            {
                return string.Format("/cache?data={0}", WebDataCache.Store(cacheItem, lifetime));
            }
        }

        public Task<Uri> CreateOAuthUri(OAuthConfig authConfig, string userId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return OAuthManager.CreateAuthUri(userId, PluginId, authConfig, Logger, cancelToken, realTime);
        }

        public Task DeleteOAuthToken(OAuthConfig oauthConfig, string userId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return OAuthManager.DeleteToken(userId, PluginId, oauthConfig, Logger, cancelToken, realTime);
        }

        public Task<OAuthToken> TryGetOAuthToken(OAuthConfig oauthConfig, string userId, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return OAuthManager.GetToken(userId, PluginId, oauthConfig, Logger, cancelToken, realTime);
        }

        public string RegisterDialogAction(DialogAction action, TimeSpan? lifeTime = null)
        {
            return DialogActionCache.Store(action, lifeTime);
        }

        public string RegisterDialogActionUrl(DialogAction action, string clientId, TimeSpan? lifeTime = null)
        {
            string cachedActionKey = RegisterDialogAction(action, lifeTime);
            return string.Format("/action?key={0}&client={1}", cachedActionKey, clientId);
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
                TTSEngine?.Dispose();
                SpeechRecoEngine?.Dispose();
                _oauthSecretStore?.Dispose();
            }
        }
    }
}
