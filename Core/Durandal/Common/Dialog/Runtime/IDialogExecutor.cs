using Durandal.API;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Dialog.Services;

namespace Durandal.Common.Dialog.Runtime
{
    /// <summary>
    /// Represents an interface which allows you to execute dialog methods with a level of security and isolation that you desire.
    /// This has nothing to do with loading the plugins themselves; the already constructed plugin and plugin services are passed
    /// to the executor methods. Implementations of this class would only implement various levels of security and isolation for the running plugin, or
    /// conversely, decreasing the level of indirection when developers want to F5-step into their plugins while they execute.
    /// </summary>
    public interface IDialogExecutor
    {
        /// <summary>
        /// Launches a specific entry point into a dialog plugin and returns the execution results
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="entryPoint"></param>
        /// <param name="isRetry"></param>
        /// <param name="query"></param>
        /// <param name="services"></param>
        /// <param name="queryLogger"></param>
        /// <returns></returns>
        Task<DialogProcessingResponse> LaunchPlugin(DurandalPlugin plugin, string entryPoint, bool isRetry, QueryWithContext query, IPluginServicesInternal services, ILogger queryLogger);

        Task<TriggerProcessingResponse> TriggerPlugin(DurandalPlugin plugin, QueryWithContext query, IPluginServicesInternal services, ILogger queryLogger);

        Task<CrossDomainRequestData> CrossDomainRequest(DurandalPlugin plugin, string targetIntent);

        Task<CrossDomainResponseData> CrossDomainResponse(DurandalPlugin plugin, CrossDomainContext context, IPluginServicesInternal services);

        /// <summary>
        /// Runs the actual Load() method on the plugin itself
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="localServices"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        Task<bool> LoadPlugin(DurandalPlugin plugin, IPluginServicesInternal localServices, ILogger logger);

        /// <summary>
        /// Runs the actual Unload() method on the plugin itself
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="localServices"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        Task<bool> UnloadPlugin(DurandalPlugin plugin, IPluginServicesInternal localServices, ILogger logger);
    }
}
