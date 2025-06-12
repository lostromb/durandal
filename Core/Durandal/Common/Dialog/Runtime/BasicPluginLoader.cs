using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Dialog.Web;
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
using Durandal.Common.Tasks;
using Durandal.Common.Dialog.Services;
using System.IO;
using Durandal.Common.Time;
using Durandal.Common.IO;

namespace Durandal.Common.Dialog.Runtime
{
    public class BasicPluginLoader : IDurandalPluginLoader
    {
        private readonly IDictionary<PluginStrongName, DurandalPlugin> _plugins = new Dictionary<PluginStrongName, DurandalPlugin>();
        private IDialogExecutor _executor;
        private IFileSystem _globalFileSystem;

        public BasicPluginLoader(IDialogExecutor executor, IFileSystem globalFileSystem)
        {
            _executor = executor;
            _globalFileSystem = globalFileSystem;
        }

        public void RegisterPluginType(DurandalPlugin toRegister)
        {
            _plugins[toRegister.GetStrongName()] = toRegister;
        }

        public Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime)
        {
            IList<PluginStrongName> returnVal = new List<PluginStrongName>();
            foreach (DurandalPlugin plugin in _plugins.Values)
            {
                returnVal.Add(plugin.GetStrongName());
            }

            return Task.FromResult<IEnumerable<PluginStrongName>>(returnVal);
        }

        public Task<CrossDomainRequestData> CrossDomainRequest(
            PluginStrongName pluginId,
            string targetIntent,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            DurandalPlugin plugin;
            if (!_plugins.TryGetValue(pluginId, out plugin))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " doesn't exist!");
            }

            return _executor.CrossDomainRequest(plugin, targetIntent);
        }

        public async Task<CrossDomainResponseResponse> CrossDomainResponse(
            PluginStrongName pluginId,
            CrossDomainContext context,
            IPluginServicesInternal services,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            DurandalPlugin plugin;
            if (!_plugins.TryGetValue(pluginId, out plugin))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " doesn't exist!");
            }

            CrossDomainResponseData pluginOutput = await _executor.CrossDomainResponse(plugin, context, services).ConfigureAwait(false);
            return new CrossDomainResponseResponse()
            {
                PluginResponse = pluginOutput,
                OutEntityContext = services.EntityContext
            };
        }
        
        public async Task<DialogProcessingResponse> LaunchPlugin(
            PluginStrongName pluginId,
            string entryPoint,
            bool isRetry,
            QueryWithContext query,
            IPluginServicesInternal services,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            DurandalPlugin plugin;
            if (!_plugins.TryGetValue(pluginId, out plugin))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " doesn't exist!");
            }

            return await _executor.LaunchPlugin(plugin, entryPoint, isRetry, query, services, queryLogger).ConfigureAwait(false);
        }

        public async Task<TriggerProcessingResponse> TriggerPlugin(
            PluginStrongName pluginId,
            QueryWithContext query,
            IPluginServicesInternal services,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            DurandalPlugin plugin;
            if (!_plugins.TryGetValue(pluginId, out plugin))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " doesn't exist!");
            }

            return await _executor.TriggerPlugin(plugin, query, services, queryLogger).ConfigureAwait(false);
        }

        public async Task<LoadedPluginInformation> LoadPlugin(
            PluginStrongName pluginId,
            IPluginServicesInternal localServices,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            // See if this plugin exists
            if (!_plugins.ContainsKey(pluginId))
            {
                await DurandalTaskExtensions.NoOpTask.ConfigureAwait(false);
                throw new Exception("Plugin \"" + pluginId + "\" does not exist");
            }

            DurandalPlugin pluginToLoad = _plugins[pluginId];

            // Ensure that this LU domain is not already taken by another plugin ID
            string luDomain = pluginToLoad.LUDomain;
            foreach (var plugin in _plugins.Values)
            {
                if (!string.Equals(plugin.PluginId, pluginId.PluginId) &&
                    string.Equals(plugin.LUDomain, luDomain))
                {
                    throw new ArgumentException("Cannot register more than one unique plugin to the LU domain \"" + luDomain + "\" (conflict between " + plugin.PluginId + " and " + pluginId.PluginId + ")");
                }
            }
            
            IConversationTree conversationTree = pluginToLoad.GetConversationTreeSingleton(localServices.FileSystem, localServices.PluginDataDirectory);
            PluginInformation pluginInfo = pluginToLoad.GetPluginInformationSingleton(localServices.FileSystem, localServices.PluginDataDirectory);
            bool loadSuccess = await _executor.LoadPlugin(pluginToLoad, localServices, logger).ConfigureAwait(false);
            // FIXME handle load failure

            return new LoadedPluginInformation()
            {
                PluginStrongName = pluginId,
                PluginId = pluginId.PluginId,
                ConversationTree = conversationTree,
                LUDomain = pluginToLoad.LUDomain,
                PluginInfo = pluginInfo
            };
        }

        public async Task<bool> UnloadPlugin(
            PluginStrongName pluginId,
            IPluginServicesInternal localServices,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            DurandalPlugin plugin;
            if (!_plugins.TryGetValue(pluginId, out plugin))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " doesn't exist!");
            }

            return await _executor.UnloadPlugin(plugin, localServices, logger).ConfigureAwait(false);
        }

        public async Task<CachedWebData> FetchPluginViewData(
            PluginStrongName plugin,
            string path,
            DateTimeOffset? ifModifiedSince,
            ILogger traceLogger,
            IRealTimeProvider realTime)
        {
            CachedWebData response;
            VirtualPath localFile = new VirtualPath(RuntimeDirectoryName.VIEW_DIR + VirtualPath.PATH_SEPARATOR_STR + plugin.PluginId + " " + plugin.MajorVersion + "." + plugin.MinorVersion + path);

            string mimeType = HttpHelpers.ResolveMimeType(localFile.Name);
            if (_globalFileSystem.Exists(localFile))
            {
                int cacheValiditySeconds = -1;

                // Does the client say they have it cached?
                if (ifModifiedSince.HasValue)
                {
                    FileStat localFileStat = await _globalFileSystem.StatAsync(localFile).ConfigureAwait(false);
                    if (localFileStat != null)
                    {
                        // this number will indicate "for how many seconds has the client cache been valid"
                        cacheValiditySeconds = (int)((ifModifiedSince.Value - localFileStat.LastWriteTime).TotalSeconds);
                    }
                }

                if (cacheValiditySeconds > 0)
                {
                    response = new CachedWebData()
                    {
                        Data = new ArraySegment<byte>(),
                        LifetimeSeconds = cacheValiditySeconds,
                        MimeType = mimeType
                    };
                }
                else
                {
                    // Read the whole file into memory and send it
                    using (RecyclableMemoryStream bucket = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                    {
                        using (Stream fileIn = await _globalFileSystem.OpenStreamAsync(localFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
                        {
                            await fileIn.CopyToAsync(bucket).ConfigureAwait(false);
                        }

                        response = new CachedWebData()
                        {
                            Data = new ArraySegment<byte>(bucket.ToArray()),
                            LifetimeSeconds = cacheValiditySeconds,
                            MimeType = mimeType
                        };
                    }
                }
            }
            else
            {
                traceLogger.Log("Client requested a nonexistent file " + localFile.FullName, LogLevel.Wrn);
                response = null;
            }

            return response;
        }
    }
}
