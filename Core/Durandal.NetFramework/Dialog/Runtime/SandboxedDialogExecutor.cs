using Durandal.API;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// An executor which runs answer plugin code inside a noncooperative thread environment,
    /// where security controls and a strict execution timeout is enforced.
    /// </summary>
    public class SandboxedDialogExecutor : AbstractDialogExecutor
    {
        private readonly TimeSpan _maxThreadLifetime;
        private readonly bool _failFast;

        public SandboxedDialogExecutor(int maxPluginExecutionTime, bool failFast)
        {
            _maxThreadLifetime = TimeSpan.FromMilliseconds(maxPluginExecutionTime);
            _failFast = failFast;
        }

        // TODO: Revoke the thread control permissions?
        /*[SecurityPermission(SecurityAction.PermitOnly, Flags = 
            SecurityPermissionFlag.Execution | 
            SecurityPermissionFlag.SerializationFormatter |
            SecurityPermissionFlag.SkipVerification |
            SecurityPermissionFlag.ControlThread |
            SecurityPermissionFlag.Infrastructure)]*/
        public override async Task<DialogProcessingResponse> LaunchPlugin(DurandalPlugin plugin, string entryPointName, bool isRetry, QueryWithContext argument, IPluginServicesInternal services, ILogger queryLogger)
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

            // Start an isolated thread to do the processing in
            // We can't use thread pools because we need a way to forcibly terminate a process to enforce SLA, and I can't find
            // a design that uses pooled tasks AND non-cooperative task cancellation.
            Exception pluginException = null;
            Thread remoteExecutionThread = new Thread(() =>
            {
                try
                {
                    pluginResult = entryPoint(argument, services).Await();
                }
                catch (ThreadAbortException)
                {
                    // Ignore this because it just means the plugin violated its timeout SLA
                }
                catch (SecurityException e)
                {
                    pluginResult = new PluginResult(Result.Failure);
                    string errorMessage = "The plugin \"" + pluginName.ToString() + "\" attempted to execute code in an unsecure way and has been terminated.";
                    pluginResult.ErrorMessage = errorMessage;
                    queryLogger.Log(errorMessage, LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                    pluginException = e;
                }
                catch (Exception e)
                {
                    pluginResult = new PluginResult(Result.Failure);
                    string errorMessage = "An unhandled exception occurred within the plugin \"" + pluginName.ToString() + "\": " + e.Message;
                    pluginResult.ErrorMessage = errorMessage;
                    queryLogger.Log(errorMessage, LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                    pluginException = e;
                }
            });

            queryLogger.Log("Starting execution thread...", LogLevel.Vrb);
            remoteExecutionThread.Name = "Sandboxed Execution " + pluginName.PluginId;
            remoteExecutionThread.IsBackground = true;
            remoteExecutionThread.Start();

            Stopwatch executionTimer = Stopwatch.StartNew();
            bool terminateThread = false;
            while (remoteExecutionThread.IsAlive && !terminateThread)
            {
                await Task.Delay(10).ConfigureAwait(false);
                terminateThread = executionTimer.Elapsed > _maxThreadLifetime;
            }

            if (terminateThread && remoteExecutionThread.IsAlive)
            {
                remoteExecutionThread.Abort();
                pluginResult = new PluginResult(Result.Failure);
                string errorMessage = "The plugin \"" + pluginName.ToString() + "\" violated its SLA and was forcibly terminated.";
                pluginResult.ErrorMessage = errorMessage;
                queryLogger.Log(errorMessage, LogLevel.Err);
            }

            if (pluginException != null && _failFast)
            {
                ExceptionDispatchInfo.Capture(pluginException).Throw();
            }

            DialogProcessingResponse returnVal = new DialogProcessingResponse(pluginResult, isRetry);
            returnVal.ApplyPluginServiceSideEffects(services);
            return returnVal;
        }

        public override async Task<TriggerProcessingResponse> TriggerPlugin(DurandalPlugin plugin, QueryWithContext query, IPluginServicesInternal services, ILogger queryLogger)
        {
            TriggerResult pluginResult = null;
            PluginStrongName pluginName = plugin.GetStrongName();

            // Start an isolated thread to do the processing in
            // We can't use thread pools because we need a way to forcibly terminate a process to enforce SLA, and I can't find
            // a design that uses pooled tasks AND non-cooperative task cancellation.
            Exception pluginException = null;
            Thread remoteExecutionThread = new Thread(() =>
            {
                try
                {
                    pluginResult = plugin.Trigger(query, services).Await();
                }
                catch (ThreadAbortException)
                {
                    // Ignore this because it just means the plugin violated its timeout SLA
                }
                catch (SecurityException e)
                {
                    pluginResult = new TriggerResult(BoostingOption.NoChange);
                    string errorMessage = "The plugin \"" + pluginName.ToString() + "\" attempted to execute code in an unsecure way and has been terminated.";
                    queryLogger.Log(errorMessage, LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                    pluginException = e;
                }
                catch (Exception e)
                {
                    pluginResult = new TriggerResult(BoostingOption.NoChange);
                    string errorMessage = "An unhandled exception occurred within the plugin \"" + pluginName.ToString() + "\": " + e.Message;
                    queryLogger.Log(errorMessage, LogLevel.Err);
                    queryLogger.Log(e, LogLevel.Err);
                    pluginException = e;
                }
            });

            queryLogger.Log("Starting trigger thread...", LogLevel.Vrb);
            remoteExecutionThread.Name = "Sandboxed Triggering " + pluginName.PluginId;
            remoteExecutionThread.IsBackground = true;
            remoteExecutionThread.Start();

            Stopwatch executionTimer = Stopwatch.StartNew();
            bool terminateThread = false;
            while (remoteExecutionThread.IsAlive && !terminateThread)
            {
                await Task.Delay(10).ConfigureAwait(false);
                terminateThread = executionTimer.Elapsed > _maxThreadLifetime;
            }

            if (terminateThread && remoteExecutionThread.IsAlive)
            {
                remoteExecutionThread.Abort();
                pluginResult = new TriggerResult(BoostingOption.NoChange);
                string errorMessage = "The plugin \"" + pluginName.ToString() + "\" violated its SLA and was forcibly terminated.";
                queryLogger.Log(errorMessage, LogLevel.Err);
            }

            if (pluginException != null && _failFast)
            {
                ExceptionDispatchInfo.Capture(pluginException).Throw();
            }

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
                logger.Log("The plugin \"" + plugin.GetStrongName().ToString() + "\" threw an exception during OnUnload()", LogLevel.Err);
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
            return plugin.CrossDomainRequest(targetIntent);
        }

        public override Task<CrossDomainResponseData> CrossDomainResponse(DurandalPlugin plugin, CrossDomainContext context, IPluginServicesInternal pluginServices)
        {
            return plugin.CrossDomainResponse(context, pluginServices);
        }
    }
}
