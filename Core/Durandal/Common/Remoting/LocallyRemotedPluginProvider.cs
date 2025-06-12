using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using Durandal.API;
using Durandal.Common.Ontology;
using System.Threading.Tasks;
using System.Threading;
using Durandal.Common.Config;
using Durandal.Common.File;
using Durandal.Common.NLP;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Speech.SR;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Net.Http;
using Durandal.Common.Speech.TTS;
using Durandal.Common.LG;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Instrumentation;
using Durandal.Common.Utils;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting
{
    /// <summary>
    /// Implementation of <see cref="IDurandalPluginProvider"/> which is functionally identical to <see cref="BasicPluginProvider"/>, except that all messages pass over a serialization socket before reaching the plugin.
    /// This is mostly used to diagnose issues in the remoting protocol itself, rather than actually being useful in production.
    /// </summary>
    public class LocallyRemotedPluginProvider : IDurandalPluginProvider, IDisposable
    {
        private readonly ILogger _logger;

        private readonly IDurandalPluginLoader _loader;
        private readonly RemoteDialogExecutorClient _client;
        private readonly RemoteDialogExecutorServer _server;
        private readonly PostOffice _clientPostOffice;
        private readonly PostOffice _serverPostOffice;
        private readonly ISpeechSynth _speechSynth;
        private readonly ISpeechRecognizerFactory _speechReco;
        private readonly IOAuthManager _oauthManager;
        private readonly IEntityResolver _entityResolver;
        private readonly ContainerKeepaliveManager _keepAliveManager;
        private readonly RemotingConfiguration _remotingConfig;
        private int _disposed = 0;

        public delegate ISocket CreateClientSocketDelegate(
            string socketConnectionString,
            ILogger logger,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions);

        public LocallyRemotedPluginProvider(
            ILogger logger,
            IDurandalPluginLoader pluginLoader,
            IRemoteDialogProtocol remotingProtocol,
            RemotingConfiguration remotingConfig,
            WeakPointer<IThreadPool> serverThreadPool,
            ISpeechSynth speechSynth,
            ISpeechRecognizerFactory speechReco,
            IOAuthManager oauthManager,
            IHttpClientFactory httpClientFactory,
            IFileSystem pluginFileSystem,
            ILGScriptCompiler lgScriptCompiler,
            INLPToolsCollection pluginNlpTools,
            IEntityResolver entityResolver,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            IServerSocketFactory serverSocketFactory,
            CreateClientSocketDelegate clientSocketFactory,
            IRealTimeProvider realTime = null,
            bool useDebugTimeouts = false)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _loader = pluginLoader.AssertNonNull(nameof(pluginLoader));
            _speechSynth = speechSynth.AssertNonNull(nameof(speechSynth));
            _speechReco = speechReco.AssertNonNull(nameof(speechReco));
            _oauthManager = oauthManager.AssertNonNull(nameof(oauthManager));
            _entityResolver = entityResolver.AssertNonNull(nameof(entityResolver));
            _remotingConfig = remotingConfig.AssertNonNull(nameof(remotingConfig));
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            ISocket serverSocket;
            ISocket clientSocket;
            if (serverSocketFactory == null || clientSocketFactory == null)
            {
                DirectSocketPair socketPair = DirectSocket.CreateSocketPair();
                serverSocket = socketPair.ServerSocket;
                clientSocket = socketPair.ClientSocket;
            }
            else
            {
                serverSocket = serverSocketFactory.CreateServerSocket(_logger.Clone("ServerSocketFactory"), metrics, metricDimensions);
                clientSocket = clientSocketFactory(serverSocket.RemoteEndpointString, _logger, metrics, metricDimensions);
            }
            
            TimeSpan postOfficeTimeout = useDebugTimeouts ? TimeSpan.FromMinutes(10) : TimeSpan.FromSeconds(30);
            _clientPostOffice = new PostOffice(
                clientSocket,
                logger,
                postOfficeTimeout,
                isServer: false,
                realTime: realTime,
                metrics: metrics,
                metricDimensions: metricDimensions,
                useDedicatedThread: _remotingConfig.UseDedicatedIpcThreads);
            _serverPostOffice = new PostOffice(
                serverSocket,
                logger,
                postOfficeTimeout,
                isServer: true,
                realTime: realTime,
                metrics: metrics,
                metricDimensions: metricDimensions,
                useDedicatedThread: _remotingConfig.UseDedicatedIpcThreads);
            _server = new RemoteDialogExecutorServer(
                _logger.Clone("LocalRemotingServer"),
                new WeakPointer<PostOffice>(_serverPostOffice),
                _loader,
                new IRemoteDialogProtocol[] { remotingProtocol },
                serverThreadPool,
                pluginFileSystem,
                httpClientFactory,
                lgScriptCompiler,
                pluginNlpTools,
                metrics,
                metricDimensions);

            _client = new RemoteDialogExecutorClient(remotingProtocol, new WeakPointer<PostOffice>(_clientPostOffice), logger.Clone("LocalRemotingClient"), metrics, metricDimensions, useDebugTimeouts);
            _server.Start(realTime);

            string serviceName = "UNKNOWN_SERVICE";
            _keepAliveManager = new ContainerKeepaliveManager(
                _clientPostOffice,
                metrics,
                metricDimensions,
                _logger.Clone("KeepAliveManager-" + serviceName),
                remotingProtocol,
                _remotingConfig);

            _keepAliveManager.Start(DefaultRealTimeProvider.Singleton).Await();

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~LocallyRemotedPluginProvider()
        {
            Dispose(false);
        }
#endif

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
                _server.Stop();
                _clientPostOffice?.Dispose();
                _serverPostOffice?.Dispose();
                _server?.Dispose();
            }
        }

        public Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime)
        {
            return _client.GetAllAvailablePlugins(realTime);
        }

        public Task<DialogProcessingResponse> LaunchPlugin(
            PluginStrongName plugin,
            string entryPoint,
            bool isRetrying,
            QueryWithContext query,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime)
        {
            PluginServices services = BuildPluginServices(plugin, queryLogger, localSessionStore, userProfiles, entityContext, contextualEntities);
            return _client.LaunchPlugin(plugin, entryPoint, isRetrying, query, services, queryLogger, realTime);
        }

        public Task<TriggerProcessingResponse> TriggerPlugin(
            PluginStrongName pluginId,
            QueryWithContext query,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime)
        {
            PluginServices services = BuildPluginServices(pluginId, queryLogger, localSessionStore, userProfiles, entityContext, contextualEntities);
            return _client.TriggerPlugin(pluginId, query, services, queryLogger, realTime);
        }

        public Task<CrossDomainRequestData> CrossDomainRequest(
            PluginStrongName targetPlugin,
            string targetIntent,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            return _client.CrossDomainRequest(targetPlugin, targetIntent, queryLogger, realTime);
        }

        public Task<CrossDomainResponseResponse> CrossDomainResponse(
            PluginStrongName targetPlugin,
            CrossDomainContext context,
            ILogger queryLogger,
            InMemoryDataStore sessionStore,
            InMemoryDataStore globalUserProfile,
            KnowledgeContext entityContext,
            IRealTimeProvider realTime)
        {
            PluginServices services = BuildPluginServices(
                targetPlugin,
                queryLogger,
                sessionStore,
                new UserProfileCollection(new InMemoryDataStore(), globalUserProfile, new InMemoryEntityHistory()),
                entityContext,
                new List<ContextualEntity>());

            return _client.CrossDomainResponse(targetPlugin, context, services, queryLogger, realTime);
        }

        public Task<LoadedPluginInformation> LoadPlugin(
            PluginStrongName plugin,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            PluginServices baseServices = new PluginServices(plugin,
                new InMemoryConfiguration(logger),
                logger,
                NullFileSystem.Singleton,
                new NullLGEngine(),
                null,
                _speechSynth,
                _speechReco,
                _oauthManager,
                new NullHttpClientFactory());

            return _client.LoadPlugin(plugin, baseServices, logger, realTime);
        }

        public Task<bool> UnloadPlugin(
            PluginStrongName plugin,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            PluginServices baseServices = new PluginServices(plugin,
                new InMemoryConfiguration(logger),
                logger,
                NullFileSystem.Singleton,
                new NullLGEngine(),
                null,
                _speechSynth,
                _speechReco,
                _oauthManager,
                new NullHttpClientFactory());

            return _client.UnloadPlugin(plugin, baseServices, logger, realTime);
        }

        public Task<CachedWebData> FetchPluginViewData(
            PluginStrongName plugin,
            string path,
            DateTimeOffset? ifModifiedSince,
            ILogger traceLogger,
            IRealTimeProvider realTime)
        {
            return _client.FetchPluginViewData(plugin, path, ifModifiedSince, traceLogger, realTime);
        }

        private PluginServices BuildPluginServices(
            PluginStrongName pluginId,
            ILogger queryLogger,
            InMemoryDataStore sessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities)
        {
            PluginServices baseServices = new PluginServices(
                pluginId,
                new InMemoryConfiguration(queryLogger),
                queryLogger,
                NullFileSystem.Singleton,
                new NullLGEngine(),
                _entityResolver,
                _speechSynth,
                _speechReco,
                _oauthManager,
                new NullHttpClientFactory());

            return baseServices.Clone(
                queryLogger.TraceId,
                queryLogger,
                sessionStore,
                userProfiles,
                entityContext,
                contextualEntities,
                new InMemoryDialogActionCache(),
                new InMemoryWebDataCache());
        }
    }
}
