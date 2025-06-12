using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Collections;
using Durandal.Common.Ontology;
using Durandal.Common.LG;
using Durandal.Common.File;
using Durandal.Common.LG.Template;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Config;
using Durandal.Common.NLP;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Speech.SR;
using Durandal.Common.Net.Http;
using Durandal.Common.Cache;
using Durandal.Common.Time;
using System.IO;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Dialog.Runtime
{
    /// <summary>
    /// Abstract superclass for plugin providers that run on the local machine and load files directly from the filesystem.
    /// Whether the plugin itself is loaded in the local CLR or isolated to an appdomain is up to the subclass to decide.
    /// </summary>
    public class MachineLocalPluginProvider : IDurandalPluginProvider
    {
        private readonly FastConcurrentDictionary<PluginStrongName, PluginServices> _allPluginServices;
        private readonly ILogger _serviceLogger;
        private readonly IFileSystem _globalFileSystem;
        private readonly INLPToolsCollection _pluginNlpTools;
        private readonly ISpeechSynth _speechSynth;
        private readonly ISpeechRecognizerFactory _speechReco;
        private readonly IOAuthManager _oauthManager;
        private readonly IEntityResolver _entityResolver;
        private readonly IHttpClientFactory _pluginHttpClientFactory;
        private readonly VirtualPath _lgDataDirectory;
        private readonly VirtualPath _pluginConfigDirectory;
        private readonly ILGScriptCompiler _lgScriptCompiler;
        private readonly IDurandalPluginLoader _loader;
        private int _disposed = 0;

        public MachineLocalPluginProvider(
            ILogger serviceLogger,
            IDurandalPluginLoader loader,
            IFileSystem globalFileSystem,
            INLPToolsCollection pluginNlpTools,
            IEntityResolver entityResolver,
            ISpeechSynth speechSynthesizer,
            ISpeechRecognizerFactory speechRecognizer,
            IOAuthManager oauthManager,
            IHttpClientFactory pluginHttpClientFactory,
            ILGScriptCompiler lgScriptCompiler)
        {
            _loader = loader;
            _serviceLogger = serviceLogger;
            _allPluginServices = new FastConcurrentDictionary<PluginStrongName, PluginServices>();
            _globalFileSystem = globalFileSystem;
            _pluginNlpTools = pluginNlpTools;
            _speechSynth = speechSynthesizer;
            _speechReco = speechRecognizer;
            _entityResolver = entityResolver;
            _oauthManager = oauthManager;
            _pluginHttpClientFactory = pluginHttpClientFactory;
            _lgScriptCompiler = lgScriptCompiler;
            _lgDataDirectory = new VirtualPath(RuntimeDirectoryName.LG_DIR);
            _pluginConfigDirectory = new VirtualPath(RuntimeDirectoryName.PLUGINCONFIG_DIR);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~MachineLocalPluginProvider()
        {
            Dispose(false);
        }
#endif

        public Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime)
        {
            return _loader.GetAllAvailablePlugins(realTime);
        }

        public Task<DialogProcessingResponse> LaunchPlugin(
            PluginStrongName pluginId,
            string entryPoint,
            bool isRetry,
            QueryWithContext query,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime)
        {
            PluginServices fakePluginServices = RetrievePluginServices(
                pluginId,
                queryLogger,
                localSessionStore,
                userProfiles,
                entityContext,
                contextualEntities);

            return _loader.LaunchPlugin(pluginId, entryPoint, isRetry, query, fakePluginServices, queryLogger, realTime);
        }

        public Task<TriggerProcessingResponse> TriggerPlugin(
            PluginStrongName pluginId,
            QueryWithContext query,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            UserProfileCollection userProfiles,
            //Action<string, object> realTimeCallback,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime)
        {
            PluginServices fakePluginServices = RetrievePluginServices(
                pluginId,
                queryLogger,
                localSessionStore,
                userProfiles,
                //realTimeCallback,
                entityContext,
                contextualEntities);

            return _loader.TriggerPlugin(pluginId, query, fakePluginServices, queryLogger, realTime);
        }

        public Task<CrossDomainRequestData> CrossDomainRequest(
            PluginStrongName pluginId,
            string targetIntent,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            return _loader.CrossDomainRequest(pluginId, targetIntent, queryLogger, realTime);
        }

        public Task<CrossDomainResponseResponse> CrossDomainResponse(
            PluginStrongName pluginId,
            CrossDomainContext context,
            ILogger queryLogger,
            InMemoryDataStore sessionStore,
            InMemoryDataStore globalUserProfile,
            KnowledgeContext entityContext,
            IRealTimeProvider realTime)
        {
            PluginServices pluginServices = RetrievePluginServices(
                pluginId,
                queryLogger,
                sessionStore,
                new UserProfileCollection(new InMemoryDataStore(), globalUserProfile, new InMemoryEntityHistory()),
                entityContext,
                new List<ContextualEntity>());

            return _loader.CrossDomainResponse(pluginId, context, pluginServices, queryLogger, realTime);
        }
        
        public async Task<LoadedPluginInformation> LoadPlugin(
            PluginStrongName pluginId,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            PluginServices pluginServices = await BuildPluginServices(pluginId, queryLogger, realTime).ConfigureAwait(false);
            _allPluginServices[pluginId] = pluginServices;
            LoadedPluginInformation returnVal = await _loader.LoadPlugin(pluginId, pluginServices, queryLogger, realTime).ConfigureAwait(false);
            return returnVal;
        }

        public async Task<bool> UnloadPlugin(
            PluginStrongName pluginId,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            PluginServices fakePluginServices = RetrievePluginServices(
                pluginId,
                queryLogger,
                new InMemoryDataStore(),
                new UserProfileCollection(new InMemoryDataStore(), new InMemoryDataStore(), new InMemoryEntityHistory()),
                new KnowledgeContext(),
                new List<ContextualEntity>());

            bool returnVal = await _loader.UnloadPlugin(pluginId, fakePluginServices, queryLogger, realTime).ConfigureAwait(false);
            _allPluginServices[pluginId] = null;
            return returnVal;
        }

        public Task<CachedWebData> FetchPluginViewData(
            PluginStrongName plugin,
            string path,
            DateTimeOffset? ifModifiedSince,
            ILogger traceLogger,
            IRealTimeProvider realTime)
        {
            return _loader.FetchPluginViewData(plugin, path, ifModifiedSince, traceLogger, realTime);
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
                
            }
        }

        private HybridFileSystem BuildPluginFileSystem(PluginStrongName pluginStrongName)
        {
            HybridFileSystem pluginVfs = new HybridFileSystem(NullFileSystem.Singleton);
            pluginVfs.AddRoute(PluginServices.VIRTUAL_PLUGINDATA_DIRECTORY_PATH, _globalFileSystem, new VirtualPath(RuntimeDirectoryName.PLUGINDATA_DIR + "\\" + pluginStrongName.PluginId + " " + pluginStrongName.MajorVersion + "." + pluginStrongName.MinorVersion));
            return pluginVfs;
        }

        private PluginServices RetrievePluginServices(
            PluginStrongName pluginId,
            ILogger queryLogger,
            InMemoryDataStore localObjectStore,
            UserProfileCollection UserProfiles,
            //Action<string, object> realtimeCallback,
            KnowledgeContext inputEntityContext,
            IList<ContextualEntity> contextualEntities)
        {
            if (_allPluginServices.ContainsKey(pluginId))
            {
                return _allPluginServices[pluginId].Clone(
                    queryLogger.TraceId,
                    queryLogger,
                    localObjectStore,
                    UserProfiles,
                    //realtimeCallback,
                    inputEntityContext,
                    contextualEntities,
                    new InMemoryDialogActionCache(),
                    new InMemoryWebDataCache());
            }

            return null;
        }

        private async Task<PluginServices> BuildPluginServices(PluginStrongName pluginStrongName, ILogger serviceLogger, IRealTimeProvider realTime)
        {
            ILGEngine lgEngine = await LGHelpers.BuildLGEngineForPlugin(_globalFileSystem, _lgDataDirectory, pluginStrongName, serviceLogger, _lgScriptCompiler, _pluginNlpTools).ConfigureAwait(false);
            IniFileConfiguration pluginConfiguration = await IniFileConfiguration.Create(
                serviceLogger.Clone("Plugin-" + pluginStrongName.PluginId + "-Config"),
                _pluginConfigDirectory.Combine("pluginconfig_" + pluginStrongName.PluginId + " " + pluginStrongName.MajorVersion + "." + pluginStrongName.MinorVersion + ".ini"),
                _globalFileSystem,
                realTime,
                warnIfNotFound: false).ConfigureAwait(false);

            return new PluginServices(
                pluginStrongName,
                pluginConfiguration,
                serviceLogger,
                _globalFileSystem,
                lgEngine,
                _entityResolver,
                _speechSynth,
                _speechReco,
                _oauthManager,
                _pluginHttpClientFactory);
        }
    }
}
