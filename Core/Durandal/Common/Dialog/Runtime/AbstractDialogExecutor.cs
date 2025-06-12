using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Logger;
using System.Reflection;
using Durandal.Common.Tasks;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Collections;

namespace Durandal.Common.Dialog.Runtime
{
    /// <summary>
    /// Dialog executor which contains logic to resolve method names into actual delegates and maintain them in a cache
    /// </summary>
    public abstract class AbstractDialogExecutor : IDialogExecutor
    {
        private readonly FastConcurrentDictionary<PluginStrongName, FastConcurrentDictionary<string, PluginContinuation>> _pluginMethodNameMap = new FastConcurrentDictionary<PluginStrongName, FastConcurrentDictionary<string, PluginContinuation>>();

        public abstract Task<DialogProcessingResponse> LaunchPlugin(DurandalPlugin plugin, string entryPoint, bool isRetry, QueryWithContext argument, IPluginServicesInternal services, ILogger queryLogger);

        public abstract Task<TriggerProcessingResponse> TriggerPlugin(DurandalPlugin plugin, QueryWithContext query, IPluginServicesInternal services, ILogger queryLogger);
        
        public abstract Task<CrossDomainRequestData> CrossDomainRequest(DurandalPlugin plugin, string targetIntent);

        public abstract Task<CrossDomainResponseData> CrossDomainResponse(DurandalPlugin plugin, CrossDomainContext context, IPluginServicesInternal services);
        
        /// <summary>
        /// Subclasses need to call this method in their override so that the cache gets properly created
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="localServices"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public virtual Task<bool> LoadPlugin(DurandalPlugin plugin, IPluginServicesInternal localServices, ILogger logger)
        {
            PluginStrongName pluginId = plugin.GetStrongName();
            FastConcurrentDictionary<string, PluginContinuation> d;
            _pluginMethodNameMap.TryGetValueOrSet(pluginId, out d, CreateNewContinuationDictionary);
            return Task.FromResult(true);
        }

        private static FastConcurrentDictionary<string, PluginContinuation> CreateNewContinuationDictionary()
        {
            return new FastConcurrentDictionary<string, PluginContinuation>();
        }

        /// <summary>
        /// Subclasses need to call this method in their override so that the cache gets properly cleaned up
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="localServices"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public virtual async Task<bool> UnloadPlugin(DurandalPlugin plugin, IPluginServicesInternal localServices, ILogger logger)
        {
            PluginStrongName pluginId = plugin.GetStrongName();
            if (!_pluginMethodNameMap.Remove(pluginId))
            {
                throw new DialogException("Attempted to unload the plugin \"" + pluginId.ToString() + "\" while it was never loaded");
            }

            await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Internal method - attempts to resolve the given method name in a given plugin and cache it. If method is not found, return null.
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="entryPointName"></param>
        /// <param name="queryLogger"></param>
        /// <returns></returns>
        protected PluginContinuation GetCachedContinuationMethod(DurandalPlugin plugin, string entryPointName, ILogger queryLogger)
        {
            PluginStrongName pluginId = plugin.GetStrongName();
            PluginContinuation returnVal;
            FastConcurrentDictionary<string, PluginContinuation> dict;

            if (!_pluginMethodNameMap.TryGetValue(pluginId, out dict))
            {
                return null;
            }

            // This lambda closure costs some performance & allocations but it's probably not worth optimizing now
            dict.TryGetValueOrSet(entryPointName, out returnVal, () => TryResolveContinuationMethodName(plugin, entryPointName, queryLogger));
            return returnVal;
        }

        /// <summary>
        /// Given a method name such as "MyPlugin.Namespace.CreateResponse", resolve that to a specific PluginContinuation delegate within the actual runtime assembly.
        /// </summary>
        /// <param name="plugin"></param>
        /// <param name="fullyQualifiedMethodName"></param>
        /// <param name="queryLogger"></param>
        /// <returns></returns>
        public static PluginContinuation TryResolveContinuationMethodName(DurandalPlugin plugin, string fullyQualifiedMethodName, ILogger queryLogger)
        {
            PluginContinuation returnVal = null;

            try
            {
                int lastPeriod = fullyQualifiedMethodName.LastIndexOf('.');
                if (lastPeriod < 0)
                {
                    queryLogger.Log("An error occurred while recreating an invokable within the domain \"" + plugin.PluginId + "\". The continuation function has no declaring type. Type name is \"" + fullyQualifiedMethodName + "\"", LogLevel.Err);
                }
                else
                {
                    // Look up the continuation function via its fully qualified name.
                    // Note that this code assumes the answer function is declared within the same assembly as the answer,
                    // and not in some kind of separate helper library (which is unlikely, but possible)
                    string methodDeclaringType = fullyQualifiedMethodName.Substring(0, lastPeriod);
                    Type declaringType = plugin.GetType().GetTypeInfo().Assembly.GetType(methodDeclaringType);
                    if (declaringType == null)
                    {
                        queryLogger.Log("An error occurred while recreating an invokable continuation within the domain \"" + plugin.PluginId + "\". The continuation name \"" + fullyQualifiedMethodName + "\" is invalid.", LogLevel.Err);
                        queryLogger.Log("Explicit continuations must refer to public named functions within the same assembly as the answer plugin they originated from.", LogLevel.Err);
                    }
                    else
                    {
                        string methodName = fullyQualifiedMethodName.Substring(lastPeriod + 1);
                        MethodInfo method = declaringType.GetRuntimeMethod(methodName, new Type[] { typeof(QueryWithContext), typeof(IPluginServices) });

                        if (method == null)
                        {
                            queryLogger.Log("The explicit continuation method \"" + methodName + "\" could not be found", LogLevel.Err);
                            return null;
                        }

                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters.Length != 2 ||
                            parameters[0].ParameterType != typeof(QueryWithContext) ||
                            parameters[1].ParameterType != typeof(IPluginServices) ||
                            method.ReturnType != typeof(Task<PluginResult>) ||
                            method.IsPrivate)
                        {
                            queryLogger.Log("The explicit continuation method \"" + methodName + "\" does not match the expected function signature", LogLevel.Err);
                            return null;
                        }

                        if (!method.IsStatic)
                            returnVal = method.CreateDelegate(typeof(PluginContinuation), plugin) as PluginContinuation;
                        else
                            returnVal = method.CreateDelegate(typeof(PluginContinuation)) as PluginContinuation;
                    }
                }
            }
            catch (Exception e)
            {
                queryLogger.Log(e, LogLevel.Err);
            }

            return returnVal;
        }

        public static void ValidateContinuationMethod(PluginContinuation method)
        {
            MethodInfo info = method.GetMethodInfo();
            if (info.IsPrivate)
            {
                throw new ArgumentException("Dialog method \"" + GetNameOfPluginContinuation(method) + "\" must have public scope");
            }
        }

        /// <summary>
        /// Converts a plugin continuation method into a fully qualified method name that can be used to store on the conversation state stack, serialized, and then executed later
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetNameOfPluginContinuation(PluginContinuation method)
        {
            MethodInfo methodInfo = method.GetMethodInfo();
            return methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
        }
    }
}
