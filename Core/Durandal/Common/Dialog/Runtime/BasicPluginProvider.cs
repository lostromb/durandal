using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Dialog.Services;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Ontology;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;

namespace Durandal.Common.Dialog.Runtime
{
    /// <summary>
    /// Plugin provider which operates on plugin classes already created in-proc.
    /// Mostly broken; do not use
    /// </summary>
    public class BasicPluginProvider : IDurandalPluginProvider
    {
        private readonly Dictionary<PluginStrongName, DurandalPlugin> _plugins = new Dictionary<PluginStrongName, DurandalPlugin>();
        private int _disposed = 0;

        public BasicPluginProvider()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~BasicPluginProvider()
        {
            Dispose(false);
        }
#endif

        public Task<CrossDomainRequestData> CrossDomainRequest(
            PluginStrongName targetPlugin,
            string targetIntent,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            DurandalPlugin plugin = GetPlugin(targetPlugin);
            PluginServices services = GetPluginServices(targetPlugin);
            return plugin.CrossDomainRequest(targetIntent);
        }

        public void AddPlugin(DurandalPlugin toAdd)
        {
            _plugins[toAdd.GetStrongName()] = toAdd;
        }

        public void RemovePlugin(PluginStrongName toRemove)
        {
            if (_plugins.ContainsKey(toRemove))
            {
                _plugins.Remove(toRemove);
            }
        }

        public async Task<CrossDomainResponseResponse> CrossDomainResponse(
            PluginStrongName targetPlugin,
            CrossDomainContext context,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            InMemoryDataStore globalUserProfile,
            KnowledgeContext entityContext,
            IRealTimeProvider realTime)
        {
            DurandalPlugin plugin = GetPlugin(targetPlugin);
            PluginServices services = GetPluginServices(targetPlugin);
            CrossDomainResponseData response = await plugin.CrossDomainResponse(context, services).ConfigureAwait(false);
            return new CrossDomainResponseResponse()
            {
                PluginResponse = response,
                OutEntityContext = entityContext
            };
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

        public Task<CachedWebData> FetchPluginViewData(
            PluginStrongName pluginName,
            string path,
            DateTimeOffset? ifModifiedSince,
            ILogger traceLogger,
            IRealTimeProvider realTime)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime)
        {
            return Task.FromResult<IEnumerable<PluginStrongName>>(_plugins.Keys);
        }

        public async Task<DialogProcessingResponse> LaunchPlugin(
            PluginStrongName pluginName,
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
            // FIXME this doesn't resolve continuations or anything
            DurandalPlugin plugin = GetPlugin(pluginName);
            PluginServices services = GetPluginServices(pluginName);
            PluginResult pluginResult = await plugin.Execute(query, services).ConfigureAwait(false);
            return new DialogProcessingResponse(pluginResult, isRetrying);
        }

        public async Task<LoadedPluginInformation> LoadPlugin(
            PluginStrongName pluginName,
            ILogger logger,
            IRealTimeProvider realTime)
        {

            DurandalPlugin plugin = GetPlugin(pluginName);
            PluginServices services = GetPluginServices(pluginName);
            await plugin.OnLoad(services).ConfigureAwait(false);
            return new LoadedPluginInformation()
            {
                ConversationTree = plugin.GetConversationTreeSingleton(NullFileSystem.Singleton, VirtualPath.Root),
                LUDomain = plugin.LUDomain,
                PluginId = plugin.PluginId,
                PluginInfo = plugin.GetPluginInformationSingleton(NullFileSystem.Singleton, VirtualPath.Root),
                PluginStrongName = plugin.GetStrongName(),
                // FIXME do we need to return serialized conversation tree?
            };
        }

        public async Task<TriggerProcessingResponse> TriggerPlugin(
            PluginStrongName pluginName,
            QueryWithContext query,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime)
        {

            DurandalPlugin plugin = GetPlugin(pluginName);
            PluginServices services = GetPluginServices(pluginName);
            TriggerResult pluginResult = await plugin.Trigger(query, services).ConfigureAwait(false);
            return new TriggerProcessingResponse(pluginResult, localSessionStore);
        }

        public async Task<bool> UnloadPlugin(PluginStrongName pluginName, ILogger logger, IRealTimeProvider realTime)
        {
            DurandalPlugin plugin = GetPlugin(pluginName);
            PluginServices services = GetPluginServices(pluginName);
            await plugin.OnUnload(services).ConfigureAwait(false);
            return true;
        }

        private DurandalPlugin GetPlugin(PluginStrongName strongName)
        {
            if (_plugins.ContainsKey(strongName))
            {
                return _plugins[strongName];
            }

            throw new DialogException("The plugin \"" + strongName.ToString() + "\" is not loaded");
        }

        private PluginServices GetPluginServices(PluginStrongName strongName)
        {
            // FIXME broken
            PluginServices services = null;
            return services;
        }
    }
}
