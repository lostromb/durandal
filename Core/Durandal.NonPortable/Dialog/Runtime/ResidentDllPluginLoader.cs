namespace Durandal.Common.Dialog.Runtime
{
    using Durandal.API;
        using Durandal.Common.Dialog.Web;
    using Durandal.Common.File;
    using Durandal.Common.LG.Statistical;
    using Durandal.Common.Logger;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Utils;
    using Durandal.Common.IO;
    using Durandal.Common.Time;
    using Durandal.Common.Ontology;
    using Security.OAuth;
    using Services;
    using System;
    using System.Collections.Generic;
    using System.Configuration.Assemblies;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Versioning;
    using System.Security;
    using System.Security.Permissions;
    using System.Security.Policy;
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
        private readonly IFileSystem _pluginFileSystem;
        private readonly IFileSystem _pluginContentFileSystem;
        private readonly PluginFrameworkLevel _maxAllowedFramework;
        private readonly IDictionary<string, long> _pluginFileModifyTimes = new Dictionary<string, long>();
        private readonly bool _doShadowCopy;

        public ResidentDllPluginLoader(
            IDialogExecutor executor,
            ILogger logger,
            IFileSystem pluginFileSystem,
            VirtualPath pluginDirectory,
            IFileSystem pluginContentFileSystem,
            PluginFrameworkLevel maxPluginFrameworkLevel,
            bool doShadowCopy = true)
        {
            _executor = executor;
            _logger = logger;
            _doShadowCopy = doShadowCopy;
            _pluginFileSystem = pluginFileSystem;
            _pluginContentFileSystem = pluginContentFileSystem;
            _pluginDir = pluginDirectory;
            _knownAnswerTypes = new Dictionary<PluginStrongName, Type>();
            _maxAllowedFramework = maxPluginFrameworkLevel;

            if (!_doShadowCopy && !(_pluginFileSystem is RealFileSystem))
            {
                throw new Exception("If you choose to not do shadowcopying, the file system must be a non-virtual filesystem (i.e. a " + nameof(RealFileSystem) + ")");
            }

            if (!_pluginFileSystem.Exists(_pluginDir))
            {
                _logger.Log("The plugin directory " + _pluginDir.FullName + " does not exist. No plugins will be loaded.", LogLevel.Err);
                return;
            }

            RefreshPluginDirectoryIfNeeded();
        }

        public Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime)
        {
            return Task.FromResult<IEnumerable<PluginStrongName>>(_knownAnswerTypes.Keys);
        }

        public Task<DialogProcessingResponse> LaunchPlugin(
            PluginStrongName pluginId,
            string entryPoint,
            bool isRetry,
            QueryWithContext query,
            IPluginServicesInternal services,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            if (!_loadedPlugins.ContainsKey(pluginId))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " isn't loaded!");
            }

            DurandalPlugin plugin = _loadedPlugins[pluginId];
            return _executor.LaunchPlugin(plugin, entryPoint, isRetry, query, services, queryLogger);
        }

        public Task<TriggerProcessingResponse> TriggerPlugin(
            PluginStrongName pluginId,
            QueryWithContext query,
            IPluginServicesInternal services,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            if (!_loadedPlugins.ContainsKey(pluginId))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " isn't loaded!");
            }

            DurandalPlugin plugin = _loadedPlugins[pluginId];
            return _executor.TriggerPlugin(plugin, query, services, queryLogger);
        }

        public Task<CrossDomainRequestData> CrossDomainRequest(
            PluginStrongName pluginId,
            string targetIntent,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            if (!_loadedPlugins.ContainsKey(pluginId))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " isn't loaded!");
            }

            DurandalPlugin plugin = _loadedPlugins[pluginId];
            return _executor.CrossDomainRequest(plugin, targetIntent);
        }

        public async Task<CrossDomainResponseResponse> CrossDomainResponse(
            PluginStrongName pluginId,
            CrossDomainContext context,
            IPluginServicesInternal pluginServices,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            if (!_loadedPlugins.ContainsKey(pluginId))
            {
                throw new Exception("Plugin " + pluginId.ToString() + " isn't loaded!");
            }

            DurandalPlugin plugin = _loadedPlugins[pluginId];
            CrossDomainResponseData pluginOutput = await _executor.CrossDomainResponse(plugin, context, pluginServices).ConfigureAwait(false);
            return new CrossDomainResponseResponse()
            {
                PluginResponse = pluginOutput,
                OutEntityContext = pluginServices.EntityContext
            };
        }

        public async Task<LoadedPluginInformation> LoadPlugin(
            PluginStrongName pluginId,
            IPluginServicesInternal services,
            ILogger logger,
            IRealTimeProvider realTime)
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
            
            await _executor.LoadPlugin(plugin, services, logger).ConfigureAwait(false);

            IConversationTree conversationTree = plugin.GetConversationTreeSingleton(services.FileSystem, services. PluginDataDirectory);
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

        public async Task<bool> UnloadPlugin(
            PluginStrongName pluginId,
            IPluginServicesInternal services,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            DurandalPlugin plugin = CreatePluginInstance(pluginId);
            if (plugin == null)
            {
                throw new Exception("Plugin " + pluginId.ToString() + " doesn't exist!");
            }
            
            _loadedPlugins.Remove(pluginId);
            return await _executor.UnloadPlugin(plugin, services, logger).ConfigureAwait(false);
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
            if (_pluginContentFileSystem.Exists(localFile))
            {
                int cacheValiditySeconds = -1;

                // Does the client say they have it cached?
                if (ifModifiedSince.HasValue)
                {
                    FileStat localFileStat = await _pluginContentFileSystem.StatAsync(localFile).ConfigureAwait(false);
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
                        using (Stream fileIn = await _pluginContentFileSystem.OpenStreamAsync(localFile, FileOpenMode.Open, FileAccessMode.Read).ConfigureAwait(false))
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
            DurandalPlugin plugin = null;
            try
            {
                plugin = Activator.CreateInstance(t) as DurandalPlugin;
            }
            catch (TargetInvocationException e)
            {
                _logger.Log("A dll invocation exception occurred while loading answer provider " + t.FullName, LogLevel.Err);
                _logger.Log("The binary may have been built against a different version of the API", LogLevel.Err);
                _logger.Log(e, LogLevel.Err);
            }

            return plugin;
        }

        /// <summary>
        /// Returns the list of all new or modified dll files within the plugin directory since the last invocation of this method
        /// </summary>
        private IList<VirtualPath> GetTouchedFiles()
        {
            List<VirtualPath> returnVal = new List<VirtualPath>();
            if (_pluginFileSystem.Exists(_pluginDir))
            {
                FindDllFilesRecursive(_pluginDir, returnVal);
            }

            return returnVal;
        }

        private void FindDllFilesRecursive(VirtualPath path, List<VirtualPath> returnVal)
        {
            foreach (VirtualPath file in _pluginFileSystem.ListFiles(path))
            {
                if (!file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                FileStat pluginStat = _pluginFileSystem.Stat(file);
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

            foreach (VirtualPath dir in _pluginFileSystem.ListDirectories(path))
            {
                FindDllFilesRecursive(dir, returnVal);
            }
         }

        //private class AssemblyLoadMetadata
        //{
        //    public AssemblyName AssemblyName { get; set; }
        //    public StrongName StrongName { get; set; }
        //    public AssemblyName[] DependentAssemblies { get; set; }
        //}

        /// <summary>
        /// Inspects the /plugins/* directory  and all subdirectories for .dll files and attempts to use reflection to load their contained IDurandalPlugin assemblies
        /// </summary>
        private void RefreshPluginDirectoryIfNeeded()
        {
            if (!_pluginFileSystem.Exists(_pluginDir))
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
                _logger.Log("Found potential Durandal plugin " + answersDLL.Name, LogLevel.Vrb);
                try
                {
                    bool assemblyIsDialogPlugin = false;
                    Assembly answerAssembly;

                    if (_doShadowCopy)
                    {
                        // Load the assembly into memory first, that way the .dll file remains unlocked (shadow copying)
                        // The advantage of this is that you can delete the dll or replace it during runtime
                        // The drawback is that you can't load any indirect dependencies of the dll in the same way because the assembly identity is lost
                        byte[] tempAssembly;
                        using (RecyclableMemoryStream dllWriteStream = new RecyclableMemoryStream(RecyclableMemoryStreamManager.Default))
                        {
                            using (Stream dllReadStream = _pluginFileSystem.OpenStream(answersDLL, FileOpenMode.Open, FileAccessMode.Read))
                            {
                                dllReadStream.CopyTo(dllWriteStream);
                            }

                            tempAssembly = dllWriteStream.ToArray();
                        }

                        answerAssembly = Assembly.Load(tempAssembly);
                    }
                    else
                    {
                        RealFileSystem realFs = _pluginFileSystem as RealFileSystem;
                        //answerAssembly = Assembly.ReflectionOnlyLoadFrom(realFs.MapResourceToFileName(answersDLL));
                        //StrongName sn = answerAssembly.Evidence.GetHostEvidence<StrongName>();
                        //Hash codeHash = answerAssembly.Evidence.GetHostEvidence<Hash>();
                        //AssemblyName assemblyName = answerAssembly.GetName();
                        //_logger.Log("Attempting to load assembly " + assemblyName.ToString() + " (strong named: " + (sn == null ? "no" : "yes") + ")");
                        //answerAssembly = Assembly.Load(assemblyName);
                        answerAssembly = Assembly.LoadFrom(realFs.MapVirtualPathToFileName(answersDLL));
                    }

                    if (!AssemblyPassesFrameworkConstraints(answerAssembly, _maxAllowedFramework))
                    {
                        _logger.Log("The plugin file " + answersDLL.Name + " exceeded the maximum allowable .Net target framework version. This is a security feature to allow only \"safe\" plugins to be loaded.", LogLevel.Err);
                        continue;
                    }

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
                            _logger.Log("Found plugin class with typename of " + t.FullName + " inside " + answersDLL.Name, LogLevel.Vrb);
                            // Create an instance and inspect its domain
                            DurandalPlugin plugin = null;
                            PluginStrongName pluginName = null;
                            try
                            {
                                plugin = Activator.CreateInstance(t) as DurandalPlugin;
                                pluginName = plugin.GetStrongName();
                                assemblyIsDialogPlugin = true;

                                // Replace existing registration, if a new dll came in and specified an updated type for a domain. This is to support on-the-fly plugin loading.
                                _knownAnswerTypes[pluginName] = t;
                            }
                            catch (TargetInvocationException e)
                            {
                                _logger.Log("A dll invocation exception occurred while loading plugin DLL \"" + t.FullName + "\"", LogLevel.Err);
                                _logger.Log(e, LogLevel.Err);
                            }
                            catch (MissingMethodException e)
                            {
                                _logger.Log("Error while invoking the constructor for \"" + t.FullName + "\" (This is expected if the class is abstract)", LogLevel.Wrn);
                                _logger.Log(e, LogLevel.Wrn);
                            }
                            finally
                            {
                                plugin?.Dispose();
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
                catch (FileNotFoundException e)
                {
                    _logger.Log("An error occurred while loading plugins from " + answersDLL.FullName, LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    if (_doShadowCopy)
                    {
                        _logger.Log("This is likely because the plugin has external .dll dependencies which cannot be loaded with the current shadowcopying configuration. Try using a containerized plugin loader instead.", LogLevel.Err);
                    }

                    throw;
                }
                catch (SecurityException e)
                {
                    //new PermissionSet(PermissionState.Unrestricted).Assert();
                    _logger.Log("A security exception occurred while loading " + answersDLL.FullName, LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    //CodeAccessPermission.RevertAssert();
                    throw;
                }
                catch (FileLoadException e)
                {
                    //new PermissionSet(PermissionState.Unrestricted).Assert();
                    _logger.Log("Failed to load the plugin or dependency " + answersDLL.FullName, LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    //CodeAccessPermission.RevertAssert();
                    throw;
                }
                catch (ReflectionTypeLoadException e)
                {
                    _logger.Log("Failed to load the plugin or dependency " + answersDLL.FullName, LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    foreach (Exception exception in e.LoaderExceptions)
                    {
                        _logger.Log(e.Message, LogLevel.Err);
                    }

                    throw;
                }
                catch (Exception e) // TODO remove blanket catch statement?
                {
                    _logger.Log("An error occurred while loading plugins from " + answersDLL.FullName, LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    throw;
                }
            }
        }

#pragma warning disable CA1801 // Remove unused parameter
        private static bool AssemblyPassesFrameworkConstraints(Assembly assembly, PluginFrameworkLevel level)
        {
            // TODO this feature currently does nothing
            TargetFrameworkAttribute targetFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
            return true;
        }
#pragma warning restore CA1801 // Remove unused parameter
    }
}
