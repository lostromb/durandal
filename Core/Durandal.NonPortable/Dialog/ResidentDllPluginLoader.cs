namespace Durandal.Common.Dialog
{
    using Durandal.API;
    using Durandal.API.Data;
    using Durandal.Common.Dialog.Web;
    using Durandal.Common.File;
    using Durandal.Common.LG.Statistical;
    using Durandal.Common.Logger;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Utils;
    using Durandal.Common.Utils.IO;
    using Ontology;
    using Runtime;
    using Security.OAuth;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Versioning;
    using System.Threading.Tasks;

    /// <summary>
    /// Plugin provider which inspects .DLL files in the filesystem and loads their types into the local CLR.
    /// This allows for some level of hot-swap and programmatic plugin loading, however it doesn't handle isolation very well
    /// and can leak memory if you reload new plugins many times over the service lifespan. Eventually this should be
    /// replaced with a proper implementation that isolates each runtime into a separate AppDomain so that plugins can
    /// load their own dependencies in isolation and their types don't pollute the local CLR.
    /// </summary>
    public class ResidentDllPluginLoader : IDurandalPluginLoader
    {
        private readonly IDialogExecutor _executor;
        private readonly IDictionary<PluginStrongName, Type> _knownAnswerTypes = new Dictionary<PluginStrongName, Type>();
        private readonly IDictionary<PluginStrongName, DurandalPlugin> _loadedPlugins = new Dictionary<PluginStrongName, DurandalPlugin>();
        private readonly ILogger _logger;
        private readonly VirtualPath _pluginDir;
        private readonly IFileSystem _pluginResourceManager;
        private readonly PluginFrameworkLevel _maxAllowedFramework;
        private readonly IDictionary<string, long> _pluginFileModifyTimes = new Dictionary<string, long>();
        
        public ResidentDllPluginLoader(
            IDialogExecutor executor,
            ILogger logger,
            IFileSystem globalFileSystem,
            VirtualPath pluginDirectory,
            PluginFrameworkLevel maxPluginFrameworkLevel)
        {
            _executor = executor;
            _logger = logger;
            _pluginResourceManager = globalFileSystem;
            _pluginDir = pluginDirectory;
            _knownAnswerTypes = new Dictionary<PluginStrongName, Type>();
            _maxAllowedFramework = maxPluginFrameworkLevel;
            if (!_pluginResourceManager.Exists(_pluginDir))
            {
                _logger.Log("The plugin directory " + _pluginDir.FullName + " does not exist. No plugins will be loaded.", LogLevel.Err);
                return;
            }

            RefreshPluginDirectoryIfNeeded();
        }

        public IEnumerable<PluginStrongName> ResolvePluginId(string pluginId)
        {
            IList<PluginStrongName> returnVal = new List<PluginStrongName>();
            foreach (PluginStrongName key in _knownAnswerTypes.Keys)
            {
                if (string.Equals(key.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
                {
                    returnVal.Add(key);
                }
            }

            return returnVal;
        }

        public IEnumerable<PluginStrongName> GetAllAvailablePlugins()
        {
            return _knownAnswerTypes.Keys;
        }

        public Task<DialogProcessingResponse> LaunchPlugin(
            PluginStrongName pluginId,
            string entryPoint,
            bool isRetry,
            QueryWithContext query,
            IPluginServices services,
            ILogger queryLogger)
        {
            if (!_loadedPlugins.ContainsKey(pluginId))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " isn't loaded!");
            }

            DurandalPlugin plugin = _loadedPlugins[pluginId];
            // Is there an existing continuation? If so, jump into it.
            // Otherwise, just use Execute
            if (entryPoint == null)
            {
                entryPoint = plugin.GetType().FullName + ".Execute";
            }

            queryLogger.Log("Using the entry point " + entryPoint, LogLevel.Vrb);
            
            return _executor.LaunchPlugin(plugin, entryPoint, isRetry, query, services, queryLogger);
        }

        public Task<TriggerProcessingResponse> TriggerPlugin(
            PluginStrongName pluginId,
            QueryWithContext query,
            IPluginServices services,
            ILogger queryLogger)
        {
            if (!_loadedPlugins.ContainsKey(pluginId))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " isn't loaded!");
            }

            DurandalPlugin plugin = _loadedPlugins[pluginId];
            return _executor.TriggerPlugin(plugin, query, services, queryLogger);
        }

        public Task<CrossDomainRequestData> CrossDomainRequest(PluginStrongName pluginId, string targetIntent)
        {
            if (!_loadedPlugins.ContainsKey(pluginId))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " isn't loaded!");
            }

            DurandalPlugin plugin = _loadedPlugins[pluginId];
            return _executor.CrossDomainRequest(plugin, targetIntent);
        }

        public Task<CrossDomainResponseData> CrossDomainResponse(PluginStrongName pluginId, CrossDomainContext context)
        {
            if (!_loadedPlugins.ContainsKey(pluginId))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " isn't loaded!");
            }

            DurandalPlugin plugin = _loadedPlugins[pluginId];
            return _executor.CrossDomainResponse(plugin, context);
        }

        public async Task<LoadedPluginInformation> LoadPlugin(PluginStrongName pluginId, IPluginServices services, ILogger logger)
        {
            if (_loadedPlugins.ContainsKey(pluginId))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " is already loaded; cannot load it twice!");
            }
            
            DurandalPlugin plugin = CreatePluginInstance(pluginId);
            if (plugin == null)
            {
                throw new Exception("Plugin " + pluginId.ToString() + " doesn't exist!");
            }
            
            await _executor.LoadPlugin(plugin, services, logger);

            IConversationTree conversationTree = plugin.GetConversationTreeSingleton();
            PluginInformation pluginInfo = plugin.GetPluginInformationSingleton(services.FileSystem, services.PluginDataDirectory);
            _loadedPlugins[pluginId] = plugin;

            return new LoadedPluginInformation()
            {
                PluginStrongName = pluginId,
                PluginId = pluginId.PluginId,
                ConversationTree = conversationTree,
                LUDomain = plugin.LUDomain,
                PluginInfo = pluginInfo
            };
        }

        public async Task<bool> UnloadPlugin(PluginStrongName pluginId, IPluginServices services, ILogger logger)
        {
            DurandalPlugin plugin = CreatePluginInstance(pluginId);
            if (plugin == null)
            {
                throw new Exception("Plugin " + pluginId.ToString() + " doesn't exist!");
            }
            
            _loadedPlugins.Remove(pluginId);
            return await _executor.UnloadPlugin(plugin, services, logger);
        }

        private DurandalPlugin CreatePluginInstance(PluginStrongName id)
        {
            // Always keep in sync with what plugins are in the folder, so we can load new ones on-the-fly
            RefreshPluginDirectoryIfNeeded();
            if (!_knownAnswerTypes.ContainsKey(id))
            {
                _logger.Log("No answer class could be found with ID \"" + id.ToString() + "\" inside all plugin DLLs (Is your class publically visible?)", LogLevel.Err);
                return null;
            }
            Type t = _knownAnswerTypes[id];
            
            // Create an instance of the answer
            DurandalPlugin o = null;
            try
            {
                o = Activator.CreateInstance(t) as DurandalPlugin;
            }
            catch (TargetInvocationException e)
            {
                _logger.Log("A dll invocation exception occurred while loading answer provider " + t.FullName, LogLevel.Err);
                _logger.Log("The binary may have been built against a different version of the API", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }
            return o;
        }

        /// <summary>
        /// Returns the list of all new or modified dll files within the plugin directory since the last invocation of this method
        /// </summary>
        private IList<VirtualPath> GetTouchedFiles()
        {
            List<VirtualPath> returnVal = new List<VirtualPath>();
            if (_pluginResourceManager.Exists(_pluginDir))
            {
                FindDllFilesRecursive(_pluginDir, returnVal);
            }

            return returnVal;
        }

        private void FindDllFilesRecursive(VirtualPath path, List<VirtualPath> returnVal)
        {
            foreach (VirtualPath file in _pluginResourceManager.ListFiles(path))
            {
                if (!file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FileStat pluginStat = _pluginResourceManager.Stat(file);
                long modTime = pluginStat.LastWriteTime.ToFileTime();
                if (!_pluginFileModifyTimes.ContainsKey(file.FullName))
                {
                    // New file
                    _pluginFileModifyTimes.Add(file.FullName, modTime);
                    returnVal.Add(file);
                    _logger.Log("Plugin file " + file.Name + " is new so I will load it");
                }
                else if (_pluginFileModifyTimes[file.FullName] != modTime)
                {
                    // Touched file
                    _pluginFileModifyTimes.Remove(file.FullName);
                    _pluginFileModifyTimes[file.FullName] = modTime;
                    returnVal.Add(file);
                    _logger.Log("Plugin file " + file.Name + " has been touched so I will reload it");
                }
            }

            foreach (VirtualPath dir in _pluginResourceManager.ListDirectories(path))
            {
                FindDllFilesRecursive(dir, returnVal);
            }
         }

        /// <summary>
        /// Inspects the /plugins/* directory  and all subdirectories for .dll files and attempts to use reflection to load their contained IDurandalPlugin assemblies
        /// </summary>
        private void RefreshPluginDirectoryIfNeeded()
        {
            if (!_pluginResourceManager.Exists(_pluginDir))
            {
                return;
            }
            
            IList<VirtualPath> modifiedDllFiles = GetTouchedFiles();
            
            // Load all answer DLLs dynamically to build a set of supported domains
            foreach (VirtualPath answersDLL in modifiedDllFiles)
            {
                if (!answersDLL.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Type expectedBaseType = typeof(DurandalPlugin);
                _logger.Log("Found potential answer plugin " + answersDLL.Name, LogLevel.Vrb);
                try
                {
                    // Load the assembly into memory first, that way the .dll file remains unlocked (shadow copying)
                    bool assemblyIsDialogPlugin = false;
                    byte[] tempAssembly;
                    using (MemoryStream dllWriteStream = new MemoryStream())
                    {
                        using (Stream dllReadStream = _pluginResourceManager.OpenStream(answersDLL, FileOpenMode.Open, FileAccessMode.Read))
                        {
                            dllReadStream.CopyTo(dllWriteStream);
                        }

                        tempAssembly = dllWriteStream.ToArray();
                    }

                    Assembly answerAssembly = Assembly.Load(tempAssembly);
                    
                    if (!AssemblyPassesFrameworkConstraints(answerAssembly, _maxAllowedFramework))
                    {
                        _logger.Log("The plugin file " + answersDLL.Name + " exceeded the maximum allowable .Net target framework version. This is a security feature to allow only \"safe\" plugins to be loaded.", LogLevel.Err);
                        continue;
                    }

                    // BUGBUG because of the way we do shadow copies, the loaded assemblies cannot depend on any other shadow copied assemblies.
                    // This prevents plugins from loading indirect dependencies that aren't already part of the core SDK
                    //foreach (var name in answerAssembly.GetReferencedAssemblies())
                    //{
                    //    _logger.Log(answerAssembly.GetName().Name + " references " + name.FullName);
                    //    _logger.Log(name.VersionCompatibility + " " + name.ProcessorArchitecture);
                    //}

                    Type[] allExportedTypes = answerAssembly.GetExportedTypes();
                    foreach (Type t in allExportedTypes)
                    {
                        if (!t.IsClass || t.IsAbstract || t.IsNested || !t.IsPublic)
                        {
                            continue;
                        }

                        // Zoom to the highest type of the inheritance tree (to support abstract and inherited answer classes)
                        Type rootType = t;
                        while (rootType.BaseType != null && rootType.BaseType.FullName != null && !rootType.BaseType.FullName.Equals("System.Object"))
                        {
                            rootType = rootType.BaseType;
                        }

                        if (rootType == expectedBaseType)
                        {
                            _logger.Log("Found answer class with typename of " + t.FullName + " inside " + answersDLL.Name, LogLevel.Vrb);
                            // Create an instance and inspect its domain
                            DurandalPlugin o = null;
                            PluginStrongName pluginName = null;
                            try
                            {
                                o = Activator.CreateInstance(t) as DurandalPlugin;
                                pluginName = o.GetStrongName();
                                assemblyIsDialogPlugin = true;
                            }
                            catch (TargetInvocationException e)
                            {
                                _logger.Log("A dll invocation exception occurred while loading answer provider \"" + t.FullName + "\"", LogLevel.Err);
                                _logger.Log(e, LogLevel.Err);
                            }
                            catch (MissingMethodException e)
                            {
                                _logger.Log("Error while invoking the constructor for \"" + t.FullName + "\" (This is expected if the class is abstract)", LogLevel.Wrn);
                                _logger.Log(e, LogLevel.Wrn);
                            }

                            if (o != null)
                            {
                                if (_knownAnswerTypes.ContainsKey(pluginName))
                                {
                                    // Replace existing registration, if a new dll came in and specified an updated type for a domain. This is to support on-the-fly plugin loading.
                                    _knownAnswerTypes.Remove(pluginName);
                                }

                                _knownAnswerTypes[pluginName] = t;
                            }
                        }
                    }

                    // Run the JIT on this assembly right now
                    if (assemblyIsDialogPlugin)
                    {
                        Jitter.PreJitMethods(answerAssembly, _logger);
                    }
                }
                catch (BadImageFormatException dllException)
                {
                    _logger.Log("The file " + answersDLL.FullName + " could not be loaded as a valid DLL!", LogLevel.Err);
                    _logger.Log(dllException, LogLevel.Err);
                }
                catch (Exception e) // TODO remove blanket catch statement?
                {
                    _logger.Log("An error occurred while loading plugins from " + answersDLL.FullName, LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                }
            }
        }

        private static bool AssemblyPassesFrameworkConstraints(Assembly assembly, PluginFrameworkLevel level)
        {
            TargetFrameworkAttribute targetFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            return true;
        }
    }
}
