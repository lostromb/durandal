using Durandal.API;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Dialog.Runtime
{
    /// <summary>
    /// A basic dialog executor which runs plugins as inline functions with basic exception handling only
    /// </summary>
    public class BasicDialogExecutor : AbstractDialogExecutor
    {
        private bool _failFast;

        public BasicDialogExecutor(bool failFast)
        {
            _failFast = failFast;
        }

        public override async Task<DialogProcessingResponse> LaunchPlugin(
            DurandalPlugin plugin,
            string entryPointName,
            bool isRetry,
            QueryWithContext argument, 
            IPluginServicesInternal services,
            ILogger queryLogger)
        {
            PluginResult pluginResult = null;
            PluginStrongName pluginName = plugin.GetStrongName();

            // Is there an existing continuation? If so, jump into it.
            // Otherwise, just use Execute
            if (string.IsNullOrEmpty(entryPointName))
            {
                entryPointName = plugin.GetType().FullName + ".Execute";
            }

            queryLogger.Log("Using the entry point " + entryPointName, LogLevel.Vrb);

            PluginContinuation entryPoint = GetCachedContinuationMethod(plugin, entryPointName, queryLogger);
            if (entryPoint == null)
            {
                pluginResult = new PluginResult(Result.Failure);
                string errorMessage = "The plugin \"" + pluginName.ToString() + "\" does not have a runtime method named " + entryPointName;
                pluginResult.ErrorMessage = errorMessage;
                queryLogger.Log(errorMessage, LogLevel.Err);
                return new DialogProcessingResponse(pluginResult, isRetry);
            }

            try
            {
                pluginResult = await entryPoint(argument, services).ConfigureAwait(false);
            }
            catch (SecurityException e)
            {
                pluginResult = new PluginResult(Result.Failure);
                string errorMessage = "The plugin \"" + pluginName.ToString() + "\" attempted to execute code in an unsecure way and has been terminated.";
                pluginResult.ErrorMessage = errorMessage;
                queryLogger.Log(errorMessage, LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
            }
            catch (Exception e)
            {
                pluginResult = new PluginResult(Result.Failure);
                string errorMessage = "An unhandled exception occurred within the plugin \"" + pluginName.ToString() + "\"";
                pluginResult.ErrorMessage = errorMessage;
                queryLogger.Log(errorMessage, LogLevel.Err);
                queryLogger.Log(e, LogLevel.Err);
                
                if (_failFast)
                {
                    ExceptionDispatchInfo.Capture(e).Throw();
                }
            }

            DialogProcessingResponse returnVal = new DialogProcessingResponse(pluginResult, isRetry);
            returnVal.ApplyPluginServiceSideEffects(services);
            return returnVal;
        }
        
        public override async Task<TriggerProcessingResponse> TriggerPlugin(DurandalPlugin plugin, QueryWithContext query, IPluginServicesInternal services, ILogger queryLogger)
        {
            // FIXME handle errors
            TriggerResult pluginResult = await plugin.Trigger(query, services).ConfigureAwait(false);
            return new TriggerProcessingResponse(pluginResult, services.SessionStore);
        }

        public override async Task<bool> LoadPlugin(DurandalPlugin plugin, IPluginServicesInternal localServices, ILogger logger)
        {
            try
            {
                await base.LoadPlugin(plugin, localServices, logger).ConfigureAwait(false);
                await plugin.OnLoad(localServices).ConfigureAwait(false);
                return true;
            }
            catch (Exception e)
            {
                logger.Log("The plugin \"" + plugin.GetStrongName().ToString() + "\" threw an exception during OnLoad()", LogLevel.Err);
                logger.Log(e, LogLevel.Err);

                if (_failFast)
                {
                    ExceptionDispatchInfo.Capture(e).Throw();
                }
            }

            return false;
        }

        public override async Task<bool> UnloadPlugin(DurandalPlugin plugin, IPluginServicesInternal localServices, ILogger logger)
        {
            try
            {
                await base.UnloadPlugin(plugin, localServices, logger).ConfigureAwait(false);
                await plugin.OnUnload(localServices).ConfigureAwait(false);
                return true;
            }
            catch (Exception e)
            {
                logger.Log("The plugin \"" + plugin.GetStrongName() + "\" threw an exception during OnUnload()", LogLevel.Err);
                logger.Log(e, LogLevel.Err);

                if (_failFast)
                {
                    ExceptionDispatchInfo.Capture(e).Throw();
                }
            }

            return false;
        }

        public override Task<CrossDomainRequestData> CrossDomainRequest(DurandalPlugin plugin, string targetIntent)
        {
            // FIXME handle errors
            return plugin.CrossDomainRequest(targetIntent);
        }

        public override Task<CrossDomainResponseData> CrossDomainResponse(DurandalPlugin plugin, CrossDomainContext context, IPluginServicesInternal services)
        {
            // FIXME handle errors
            return plugin.CrossDomainResponse(context, services);
        }
    }
}


