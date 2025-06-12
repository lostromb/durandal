using Durandal.API;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog.Runtime
{
    public interface IDurandalPluginLoader
    {
        /// <summary>
        /// Launches a specific entry point into a dialog plugin and returns the execution results
        /// </summary>
        /// <param name="pluginId"></param>
        /// <param name="entryPoint"></param>
        /// <param name="isRetry"></param>
        /// <param name="query"></param>
        /// <param name="services"></param>
        /// <param name="queryLogger"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        Task<DialogProcessingResponse> LaunchPlugin(PluginStrongName pluginId, string entryPoint, bool isRetry, QueryWithContext query, IPluginServicesInternal services, ILogger queryLogger, IRealTimeProvider realTime);

        Task<TriggerProcessingResponse> TriggerPlugin(PluginStrongName pluginId, QueryWithContext query, IPluginServicesInternal services, ILogger queryLogger, IRealTimeProvider realTime);

        Task<CrossDomainRequestData> CrossDomainRequest(PluginStrongName pluginId, string targetIntent, ILogger queryLogger, IRealTimeProvider realTime);

        Task<CrossDomainResponseResponse> CrossDomainResponse(PluginStrongName pluginId, CrossDomainContext context, IPluginServicesInternal services, ILogger queryLogger, IRealTimeProvider realTime);

        /// <summary>
        /// Runs the actual Load() method on the plugin itself
        /// </summary>
        /// <param name="pluginId"></param>
        /// <param name="localServices"></param>
        /// <param name="queryLogger"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        Task<LoadedPluginInformation> LoadPlugin(PluginStrongName pluginId, IPluginServicesInternal localServices, ILogger queryLogger, IRealTimeProvider realTime);

        /// <summary>
        /// Runs the actual Unload() method on the plugin itself
        /// </summary>
        /// <param name="pluginId"></param>
        /// <param name="localServices"></param>
        /// <param name="queryLogger"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        Task<bool> UnloadPlugin(PluginStrongName pluginId, IPluginServicesInternal localServices, ILogger queryLogger, IRealTimeProvider realTime);

        Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime);

        Task<CachedWebData> FetchPluginViewData(PluginStrongName plugin, string path, DateTimeOffset? ifModifiedSince, ILogger traceLogger, IRealTimeProvider realTime);
    }
}
