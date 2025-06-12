namespace Durandal.Common.Dialog.Runtime
{
        using Durandal.API;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.Logger;
    using Durandal.Common.Ontology;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Durandal.Common.Time;

    /// <summary>
    /// This interface defines the bridge point between addressing a plugin only by its ID or strong name, and actually
    /// instantiating the plugin implementation and executing it somehow. There is a lot of latitude in how this could
    /// be done - generally (for debugging or functional tests) the plugin is just loaded directly in memory, but more
    /// advanced constructs could communicate over the network to a remotely hosted plugin on another machine or inside
    /// of an isolated AppDomain container that keeps execution and CLR runtime dependencies separate. Theoretically,
    /// the plugin itself could be implemented outside of C# entirely, and the execution could be bridged to a runtime
    /// in some other programming language (IronPython or a micro web service of some kind)
    /// </summary>
    public interface IDurandalPluginProvider : IDisposable
    {
        Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime);

        Task<DialogProcessingResponse> LaunchPlugin(
            PluginStrongName plugin,
            string entryPoint,
            bool isRetrying,
            QueryWithContext query,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime);

        Task<TriggerProcessingResponse> TriggerPlugin(
            PluginStrongName pluginId,
            QueryWithContext query,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime);

        Task<CrossDomainRequestData> CrossDomainRequest(
            PluginStrongName targetPlugin,
            string targetIntent,
            ILogger queryLogger,
            IRealTimeProvider realTime);

        Task<CrossDomainResponseResponse> CrossDomainResponse(
            PluginStrongName targetPlugin,
            CrossDomainContext context,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            InMemoryDataStore globalUserProfile,
            KnowledgeContext entityContext,
            IRealTimeProvider realTime);

        Task<LoadedPluginInformation> LoadPlugin(
            PluginStrongName plugin,
            ILogger logger,
            IRealTimeProvider realTime);

        Task<bool> UnloadPlugin(PluginStrongName plugin,
            ILogger logger,
            IRealTimeProvider realTime);

        /// <summary>
        /// Retrieves a static view file from a single loaded plugin
        /// </summary>
        /// <param name="plugin">The strong name of the plugin to fetch the view file for</param>
        /// <param name="path">The relative path of the file after the /views/plugin_id" prefix. Example "/page.css", "/resources/icon.png"</param>
        /// <param name="ifModifiedSince">If the client is using cache control, this is the datetime value that the client's cache was last updated</param>
        /// <param name="traceLogger">A tracing logger</param>
        /// <param name="realTime"></param>
        /// <returns>Cached web data containing payload, mime type, and cache validity info if found, otherwise null</returns>
        Task<CachedWebData> FetchPluginViewData(
            PluginStrongName plugin,
            string path,
            DateTimeOffset? ifModifiedSince,
            ILogger traceLogger,
            IRealTimeProvider realTime);
    }
}
