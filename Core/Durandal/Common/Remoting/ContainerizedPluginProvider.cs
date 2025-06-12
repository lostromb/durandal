using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Dialog.Web;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.LG;
using Durandal.Common.LG.Statistical;
using Durandal.Common.LG.Template;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP;
using Durandal.Common.Ontology;
using Durandal.Common.Packages;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Utils;
using Durandal.Common.Collections;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting
{
    public abstract class ContainerizedPluginProvider : IDurandalPluginProvider
    {
        private readonly ReaderWriterLockAsync _lock = new ReaderWriterLockAsync(Environment.ProcessorCount);
        private readonly IDictionary<PluginStrongName, PluginServices> _allPluginServices;
        private readonly IEntityResolver _entityResolver;
        private readonly ISpeechSynth _speechSynth;
        private readonly ISpeechRecognizerFactory _speechReco;
        private readonly IOAuthManager _oauthManager;
        private readonly ILogger _coreLogger;
        private readonly IFileSystem _globalFileSystem;
        private readonly IHttpClientFactory _globalHttpClientFactory;
        private readonly VirtualPath _pluginDirectory;
        private readonly VirtualPath _runtimesDirectory;
        private readonly bool _useDebugTimeouts;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly IServerSocketFactory _serverSocketFactory;
        private readonly IRemoteDialogProtocol _dialogProtocol;
        private readonly string _preferredRuntime;
        private readonly ISet<string> _allowedRuntimes;
        private readonly RemotingConfiguration _remotingConfig;

        // Packaging installer components
        private PackageInstaller _packageInstaller;

        // 1:1 mapping of a package name to the manifest that defines what plugins are in that package
        private readonly IDictionary<VirtualPath, PackageManifest> _packageNameToManifest;

        // 1:1 mapping of a package name to the GUID of the container that package is running in
        private readonly IDictionary<VirtualPath, string> _packageNameToContainerName;

        // N:1 mapping of plugin ID to the package name that plugin can be found in
        private readonly IDictionary<PluginStrongName, VirtualPath> _pluginIdToPackageName;

        // 1:1 mapping of container GUID to the actual container that is running a certain package's code
        private readonly IDictionary<string, GenericContainerHost> _containerNameToContainer;

        // Set of plugin IDs that are currently actually loaded
        private readonly HashSet<PluginStrongName> _actuallyLoadedPlugins;

        // Set of plugin IDs that should be loaded (modify this, then call AttemptToReconcileLoadedPlugins() to resolve actually loaded plugins)
        private readonly HashSet<PluginStrongName> _pluginsThatShouldBeLoaded;

        private readonly List<ContainerRuntimeInformation> _availableRuntimes;

        private int _disposed = 0;

        protected ContainerizedPluginProvider(
            ILogger logger,
            IFileSystem globalFileSystem,
            IHttpClientFactory globalHttpClientFactory,
            IEntityResolver entityResolver,
            ISpeechSynth speechSynthesizer,
            ISpeechRecognizerFactory speechRecognizer,
            IOAuthManager oauthManager,
            bool useDebugTimeouts,
            IMetricCollector metrics,
            DimensionSet dimensions,
            IServerSocketFactory serverSocketFactory,
            IRealTimeProvider realTime,
            IRemoteDialogProtocol remotingProtocol,
            string preferredRuntime,
            ISet<string> allowedRuntimes,
            RemotingConfiguration remotingConfig)
        {
            _allPluginServices = new Dictionary<PluginStrongName, PluginServices>();
            _entityResolver = entityResolver;
            _speechSynth = speechSynthesizer;
            _speechReco = speechRecognizer;
            _oauthManager = oauthManager;
            _coreLogger = logger;
            _globalFileSystem = globalFileSystem;
            _globalHttpClientFactory = globalHttpClientFactory;
            _pluginDirectory = new VirtualPath(RuntimeDirectoryName.PLUGIN_DIR);
            _runtimesDirectory = new VirtualPath(RuntimeDirectoryName.RUNTIMES_DIR);
            _packageNameToContainerName = new Dictionary<VirtualPath, string>();
            _packageNameToManifest = new Dictionary<VirtualPath, PackageManifest>();
            _pluginIdToPackageName = new Dictionary<PluginStrongName, VirtualPath>();
            _containerNameToContainer = new Dictionary<string, GenericContainerHost>();
            _actuallyLoadedPlugins = new HashSet<PluginStrongName>();
            _pluginsThatShouldBeLoaded = new HashSet<PluginStrongName>();
            _availableRuntimes = new List<ContainerRuntimeInformation>();
            _useDebugTimeouts = useDebugTimeouts;
            _metrics = new WeakPointer<IMetricCollector>(metrics);
            _dimensions = dimensions;
            _serverSocketFactory = serverSocketFactory.AssertNonNull(nameof(serverSocketFactory));
            _dialogProtocol = remotingProtocol;
            _preferredRuntime = preferredRuntime;
            _allowedRuntimes = allowedRuntimes;
            if (!_allowedRuntimes.Contains(_preferredRuntime))
            {
                throw new ArgumentException("Cannot prefer a runtime \"" + _preferredRuntime + "\" which is not installed");
            }

            _remotingConfig = remotingConfig.AssertNonNull(nameof(remotingConfig));

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ContainerizedPluginProvider()
        {
            Dispose(false);
        }
#endif

        protected async Task Initialize(IRealTimeProvider realTime)
        {
            _packageInstaller = await PackageInstaller.Create(
                _globalFileSystem,
                new PortableZipPackageFileLoader(_coreLogger.Clone("ZipPackageLoader")),
                _coreLogger.Clone("PackageInstaller"),
                realTime,
                PackageComponent.Dialog);

            // Get the initial list of packages from the package loader and register them
            IList<KeyValuePair<VirtualPath, PackageManifest>> knownPackages = await _packageInstaller.InitializePackages();
            foreach (var kvp in knownPackages)
            {
                _packageNameToManifest[kvp.Key] = kvp.Value;
                foreach (PluginStrongName pluginId in kvp.Value.PluginIds)
                {
                    _pluginIdToPackageName[pluginId] = kvp.Key;
                }
            }

            _packageInstaller.PackageInstalledEvent.Subscribe(HandlePackageInstalled);
            _packageInstaller.PackageUninstallingEvent.Subscribe(HandlePackageUninstalling);
            _packageInstaller.PackageUpdatingEvent.Subscribe(HandlePackageUpdating);
            _packageInstaller.PackageUpdatedEvent.Subscribe(HandlePackageUpdated);

            await AttemptToReconcileLoadedPlugins().ConfigureAwait(false);
            await EnumerateAvailableRuntimes().ConfigureAwait(false);
        }

        public async Task<DialogProcessingResponse> LaunchPlugin(
            PluginStrongName plugin,
            string entryPoint,
            bool isRetry,
            QueryWithContext query,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime)
        {
            int hRead = await _lock.EnterReadLockAsync();
            int hRead2 = await _packageInstaller.EnterReadLockAsync();
            try
            {
                GenericContainerHost container = GetContainerForPlugin(plugin);
                PluginServices services = RetrievePluginServices(plugin, queryLogger, localSessionStore, userProfiles, entityContext, contextualEntities);
                return await container.LaunchPlugin(plugin, entryPoint, isRetry, query, services, queryLogger, realTime);
            }
            finally
            {
                _lock.ExitReadLock(hRead);
                _packageInstaller.ExitReadLock(hRead2);
            }
        }

        public async Task<TriggerProcessingResponse> TriggerPlugin(
            PluginStrongName pluginId,
            QueryWithContext query,
            ILogger queryLogger,
            InMemoryDataStore localSessionStore,
            UserProfileCollection userProfiles,
            KnowledgeContext entityContext,
            IList<ContextualEntity> contextualEntities,
            IRealTimeProvider realTime)
        {
            int hRead = await _lock.EnterReadLockAsync();
            int hRead2 = await _packageInstaller.EnterReadLockAsync();
            try
            {
                GenericContainerHost container = GetContainerForPlugin(pluginId);
                PluginServices services = RetrievePluginServices(pluginId, queryLogger, localSessionStore, userProfiles, entityContext, contextualEntities);
                return await container.TriggerPlugin(pluginId, query, services, queryLogger, realTime);
            }
            finally
            {
                _lock.ExitReadLock(hRead);
                _packageInstaller.ExitReadLock(hRead2);
            }
        }

        public async Task<CrossDomainRequestData> CrossDomainRequest(PluginStrongName targetPlugin, string targetIntent, ILogger queryLogger, IRealTimeProvider realTime)
        {
            int hRead = await _lock.EnterReadLockAsync();
            int hRead2 = await _packageInstaller.EnterReadLockAsync();
            try
            {
                GenericContainerHost container = GetContainerForPlugin(targetPlugin);
                return await container.CrossDomainRequest(targetPlugin, targetIntent, queryLogger, realTime);
            }
            finally
            {
                _lock.ExitReadLock(hRead);
                _packageInstaller.ExitReadLock(hRead2);
            }
        }

        public async Task<CrossDomainResponseResponse> CrossDomainResponse(
            PluginStrongName targetPlugin,
            CrossDomainContext context,
            ILogger queryLogger,
            InMemoryDataStore sessionStore,
            InMemoryDataStore globalUserProfile,
            KnowledgeContext entityContext,
            IRealTimeProvider realTime)
        {
            int hRead = await _lock.EnterReadLockAsync();
            int hRead2 = await _packageInstaller.EnterReadLockAsync();
            try
            {
                GenericContainerHost container = GetContainerForPlugin(targetPlugin);
                PluginServices services = RetrievePluginServices(targetPlugin, queryLogger, sessionStore, null, entityContext, null); // FIXME this is all messed up
                return await container.CrossDomainResponse(targetPlugin, context, services, queryLogger, realTime);
            }
            finally
            {
                _lock.ExitReadLock(hRead);
                _packageInstaller.ExitReadLock(hRead2);
            }
        }

        public Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime)
        {
            return Task.FromResult<IEnumerable<PluginStrongName>>(_pluginIdToPackageName.Keys);
        }

        public async Task<CachedWebData> FetchPluginViewData(PluginStrongName plugin, string path, DateTimeOffset? ifModifiedSince, ILogger traceLogger, IRealTimeProvider realTime)
        {
            int hRead = await _lock.EnterReadLockAsync();
            int hRead2 = await _packageInstaller.EnterReadLockAsync();
            try
            {
                GenericContainerHost container = GetContainerForPlugin(plugin);
                return await container.FetchPluginViewData(plugin, path, ifModifiedSince, traceLogger, realTime);
            }
            finally
            {
                _lock.ExitReadLock(hRead);
                _packageInstaller.ExitReadLock(hRead2);
            }
        }

        public async Task<LoadedPluginInformation> LoadPlugin(PluginStrongName plugin, ILogger logger, IRealTimeProvider realTime)
        {
            if (!_pluginsThatShouldBeLoaded.Contains(plugin))
            {
                logger.Log("Attempting to load plugin " + plugin.ToString() + " into containerized runtime");
                _pluginsThatShouldBeLoaded.Add(plugin);
            }

            return await LoadPluginInternal(plugin, logger, realTime);
        }

#if DEBUG
        public async Task _UnitTesting_CrashContainer(
            PluginStrongName plugin,
            IRealTimeProvider realTime)
        {
            int hRead = await _lock.EnterReadLockAsync();
            int hRead2 = await _packageInstaller.EnterReadLockAsync();
            try
            {
                GenericContainerHost container = GetContainerForPlugin(plugin);
                await container._UnitTesting_CrashContainer(realTime);
            }
            finally
            {
                _lock.ExitReadLock(hRead);
                _packageInstaller.ExitReadLock(hRead2);
            }
        }
#endif

        private async Task<LoadedPluginInformation> LoadPluginInternal(PluginStrongName plugin, ILogger logger, IRealTimeProvider realTime)
        {
            int hWrite = await _lock.EnterWriteLockAsync();
            try
            {
                // Is this plugin known?
                VirtualPath packageName;
                if (!_pluginIdToPackageName.TryGetValue(plugin, out packageName))
                {
                    throw new DialogException("Plugin ID " + plugin.ToString() + " is not known");
                }

                // Is it already loaded?
                if (_actuallyLoadedPlugins.Contains(plugin))
                {
                    throw new DialogException("Cannot load the plugin " + plugin.ToString() + " more than once");
                }

                // Is the container already initialized?
                string containerName;
                GenericContainerHost container;
                bool createdContainer = false;
                if (!_packageNameToContainerName.TryGetValue(packageName, out containerName))
                {
                    // If not, create a new container here
                    containerName = packageName.NameWithoutExtension + "-" + Guid.NewGuid().ToString("N");
                    VirtualPath pluginDir = _pluginDirectory.Combine(packageName.NameWithoutExtension);
                    PackageManifest manifest = _packageNameToManifest[packageName];
                    logger.Log("Initializing new container " + containerName + " to host plugin " + pluginDir.FullName);
                    container = await InitializeContainer(pluginDir, realTime, containerName, manifest);
                    if (container == null)
                    {
                        // Failed to create container, so abort loading the plugin
                        logger.Log(plugin.ToString() + " has not been loaded", LogLevel.Err);
                        return null;
                    }

                    _packageNameToContainerName[packageName] = containerName;
                    _containerNameToContainer[containerName] = container;
                    createdContainer = true;
                }
                else
                {
                    container = _containerNameToContainer[containerName];
                }

                logger.Log("Building plugin services for " + plugin.ToString());
                PluginServices services = BuildPluginServices(plugin, logger);
                logger.Log("Loading plugin " + plugin.ToString());
                LoadedPluginInformation returnVal = await container.LoadPlugin(plugin, services, logger, realTime);
                if (returnVal == null)
                {
                    logger.Log("Error while loading plugin " + plugin.ToString() + ". The returned information was null", LogLevel.Err);

                    // Tear down container if we just created one
                    if (createdContainer)
                    {
                        container.Dispose();
                        _containerNameToContainer.Remove(containerName);
                        _packageNameToContainerName.Remove(packageName);
                    }

                    return null;
                }

                _allPluginServices[plugin] = services;
                _actuallyLoadedPlugins.Add(plugin);

                logger.Log("Successfully loaded plugin " + plugin.ToString() + " into container " + containerName);

                return returnVal;
            }
            finally
            {
                _lock.ExitWriteLock(hWrite);
            }
        }

        public async Task<bool> UnloadPlugin(PluginStrongName plugin, ILogger logger, IRealTimeProvider realTime)
        {
            if (_pluginsThatShouldBeLoaded.Contains(plugin))
            {
                _pluginsThatShouldBeLoaded.Remove(plugin);
            }

            return await UnloadPluginInternal(plugin, logger, realTime);
        }

        private async Task<bool> UnloadPluginInternal(PluginStrongName plugin, ILogger logger, IRealTimeProvider realTime)
        {
            int hWrite = await _lock.EnterWriteLockAsync();
            try
            {
                // Is this plugin known?
                VirtualPath packageName;
                if (!_pluginIdToPackageName.TryGetValue(plugin, out packageName))
                {
                    throw new DialogException("Plugin ID " + plugin.ToString() + " is not known");
                }

                // Is it loaded?
                if (!_actuallyLoadedPlugins.Contains(plugin))
                {
                    throw new DialogException("Cannot unload plugin " + plugin.ToString() + " because it is not loaded");
                }

                // Run the Unload() method within the container
                string containerName = _packageNameToContainerName[packageName];
                GenericContainerHost container = _containerNameToContainer[containerName];
                PluginServices services = BuildPluginServices(plugin, logger);
                bool returnVal = await container.UnloadPlugin(plugin, services, logger, realTime);

                _allPluginServices.Remove(plugin);
                _actuallyLoadedPlugins.Remove(plugin);

                // Are there any plugins still loaded within this container?
                // If not, we can dispose of it
                bool containerStillInUse = false;
                foreach (PluginStrongName loadedPlugin in _actuallyLoadedPlugins)
                {
                    if (packageName.Equals(_pluginIdToPackageName[loadedPlugin]))
                    {
                        containerStillInUse = true;
                        break;
                    }
                }

                if (!containerStillInUse)
                {
                    logger.Log("Disposing of unused appdomain container " + containerName);
                    container.Dispose();
                    _containerNameToContainer.Remove(containerName);
                    _packageNameToContainerName.Remove(packageName);
                }

                return returnVal;
            }
            finally
            {
                _lock.ExitWriteLock(hWrite);
            }
        }

        /// <summary>
        /// Indicates whether this runtime has an available development version runtime. Used as an assertion on unit tests
        /// to make sure the tests run against the latest compiled runtime.
        /// </summary>
        /// <param name="runtimeFramework">A target framework filter to apply, or null for no filter.</param>
        /// <returns></returns>
        public bool HasDevRuntimeAvailable(string runtimeFramework = null)
        {
            foreach (ContainerRuntimeInformation info in _availableRuntimes)
            {
                if (info.IsDevelopmentVersion)
                {
                    if (string.IsNullOrEmpty(runtimeFramework) ||
                        string.Equals(runtimeFramework, info.RuntimeFramework, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
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
                _actuallyLoadedPlugins.Clear();
                _pluginsThatShouldBeLoaded.Clear();
                _pluginIdToPackageName.Clear();
                _packageNameToContainerName.Clear();

                foreach (GenericContainerHost container in _containerNameToContainer.Values)
                {
                    container.Dispose();
                }

                _containerNameToContainer.Clear();
                _lock.Dispose();
                _packageInstaller.Dispose();
            }
        }

        protected PluginServices RetrievePluginServices(
            PluginStrongName pluginId,
            ILogger queryLogger,
            InMemoryDataStore localObjectStore,
            UserProfileCollection UserProfiles,
            KnowledgeContext inputEntityContext,
            IList<ContextualEntity> contextualEntities)
        {
            if (_allPluginServices.ContainsKey(pluginId))
            {
                return _allPluginServices[pluginId].Clone(
                    queryLogger.TraceId,
                    queryLogger,
                    localObjectStore,
                    UserProfiles,
                    inputEntityContext,
                    contextualEntities,
                    new InMemoryDialogActionCache(),
                    new InMemoryWebDataCache());
            }

            return null;
        }

        private GenericContainerHost GetContainerForPlugin(PluginStrongName plugin)
        {
            if (!_actuallyLoadedPlugins.Contains(plugin))
            {
                throw new DialogException("Cannot invoke plugin " + plugin.ToString() + " because it is not loaded!");
            }

            VirtualPath packageName = _pluginIdToPackageName[plugin];
            string containerName = _packageNameToContainerName[packageName];
            GenericContainerHost container = _containerNameToContainer[containerName];
            return container;
        }

        private async Task HandlePackageInstalled(object sender, PackageInstaller.PackageChangedEventArgs args, IRealTimeProvider realTime)
        {
            _packageNameToManifest[args.PackageFile] = args.Manifest;
            foreach (PluginStrongName pluginId in args.Manifest.PluginIds)
            {
                _pluginIdToPackageName[pluginId] = args.PackageFile;
            }

            await AttemptToReconcileLoadedPlugins();
        }

        private async Task HandlePackageUninstalling(object sender, PackageInstaller.PackageChangedEventArgs args, IRealTimeProvider realTime)
        {
            foreach (PluginStrongName pluginId in args.Manifest.PluginIds)
            {
                if (_actuallyLoadedPlugins.Contains(pluginId))
                {
                    await UnloadPluginInternal(pluginId, _coreLogger, DefaultRealTimeProvider.Singleton);
                }
            }

            foreach (PluginStrongName pluginId in args.Manifest.PluginIds)
            {
                if (_actuallyLoadedPlugins.Contains(pluginId))
                {
                    await UnloadPluginInternal(pluginId, _coreLogger, DefaultRealTimeProvider.Singleton);
                }
            }

            _packageNameToManifest.Remove(args.PackageFile);

            foreach (PluginStrongName pluginId in args.Manifest.PluginIds)
            {
                _pluginIdToPackageName.Remove(pluginId);
            }

            await AttemptToReconcileLoadedPlugins();
        }

        private Task HandlePackageUpdating(object sender, PackageInstaller.PackageChangedEventArgs args, IRealTimeProvider realTime)
        {
            return HandlePackageUninstalling(sender, args, realTime);
        }

        private Task HandlePackageUpdated(object sender, PackageInstaller.PackageChangedEventArgs args, IRealTimeProvider realTime)
        {
            return HandlePackageInstalled(sender, args, realTime);
        }

        private async Task EnumerateAvailableRuntimes()
        {
            if (await _globalFileSystem.ExistsAsync(_runtimesDirectory).ConfigureAwait(false))
            {
                foreach (VirtualPath runtimeDir in await _globalFileSystem.ListDirectoriesAsync(_runtimesDirectory).ConfigureAwait(false))
                {
                    try
                    {
                        ContainerRuntimeInformation info = ContainerRuntimeInformation.Parse(runtimeDir.Name);
                        if (_allowedRuntimes.Contains(info.RuntimeFramework))
                        {
                            _coreLogger.Log("Discovered available runtime " + runtimeDir.Name);
                            _availableRuntimes.Add(info);
                        }
                        else
                        {
                            _coreLogger.Log("Discovered available runtime " + runtimeDir.Name + ", but it is not allowed in the current configuration", LogLevel.Wrn);
                        }
                    }
                    catch (Exception e)
                    {
                        _coreLogger.Log(e, LogLevel.Err);
                    }
                }
            }
        }

        private class RuntimeDiscoveryInfo
        {
            public ContainerRuntimeInformation DevelopmentVersion { get; set; }
            public ContainerRuntimeInformation NearestRequestedVersion { get; set; }
            public ContainerRuntimeInformation HighestVersion { get; set; }
        }

        private RuntimeDiscoveryInfo DiscoverRuntimeInfo(string runtimeType, Version requestedMinimumVersion)
        {
            RuntimeDiscoveryInfo returnVal = null;
            foreach (ContainerRuntimeInformation info in _availableRuntimes)
            {
                if (string.Equals(runtimeType, info.RuntimeFramework))
                {
                    if (returnVal == null)
                    {
                        returnVal = new RuntimeDiscoveryInfo();
                    }

                    if (info.IsDevelopmentVersion)
                    {
                        returnVal.DevelopmentVersion = info;
                    }

                    if (returnVal.HighestVersion == null || info.RuntimeVersion > returnVal.HighestVersion.RuntimeVersion)
                    {
                        returnVal.HighestVersion = info;
                    }

                    if (requestedMinimumVersion != null)
                    {
                        // Pick the closest version that is equal to or above the package's requested version
                        if (info.RuntimeVersion >= requestedMinimumVersion &&
                            (returnVal.NearestRequestedVersion == null || info.RuntimeVersion < returnVal.NearestRequestedVersion.RuntimeVersion))
                        {
                            returnVal.NearestRequestedVersion = info;
                        }
                    }
                }
            }

            return returnVal;
        }

        private ContainerRuntimeInformation SelectBestRuntimeForContainer(PackageManifest pluginManifest, ILogger logger)
        {
            string requiredRuntime = DialogRuntimeFramework.RUNTIME_PORTABLE;
            Version minimumRequiredPluginVersion = null;
            foreach (var plugin in pluginManifest.DialogComponents)
            {
                Version thisPluginVersion;
                if (Version.TryParse(plugin.PluginRuntimeVersion, out thisPluginVersion) &&
                    minimumRequiredPluginVersion == null || thisPluginVersion > minimumRequiredPluginVersion)
                {
                    minimumRequiredPluginVersion = thisPluginVersion;
                }

                if (!string.IsNullOrEmpty(plugin.PluginRuntimeType) &&
                    !string.Equals(plugin.PluginRuntimeType, DialogRuntimeFramework.RUNTIME_PORTABLE))
                {
                    requiredRuntime = plugin.PluginRuntimeType;
                }
            }

            logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Plugin is requesting runtime {0} version {1}", requiredRuntime, minimumRequiredPluginVersion);

            SmallDictionary<string, RuntimeDiscoveryInfo> runtimeInfo = new SmallDictionary<string, RuntimeDiscoveryInfo>();
            foreach (string allowedRuntime in _allowedRuntimes)
            {
                RuntimeDiscoveryInfo thisRuntime = DiscoverRuntimeInfo(allowedRuntime, minimumRequiredPluginVersion);

                if (thisRuntime != null)
                {
                    runtimeInfo[allowedRuntime] = thisRuntime;
                }
            }

            RuntimeDiscoveryInfo runtimeToUse;
            if (string.Equals(requiredRuntime, DialogRuntimeFramework.RUNTIME_PORTABLE))
            {
                if (!runtimeInfo.TryGetValue(_preferredRuntime, out runtimeToUse))
                {
                    runtimeToUse = runtimeInfo.Values.FirstOrDefault();
                }
            }
            else if (!runtimeInfo.TryGetValue(requiredRuntime, out runtimeToUse))
            {
                return null;
            }

            // If dev is an option, pick it always
            if (runtimeToUse.DevelopmentVersion != null)
            {
                return runtimeToUse.DevelopmentVersion;
            }
            else if (runtimeToUse.NearestRequestedVersion != null)
            {
                if (minimumRequiredPluginVersion != null && runtimeToUse.NearestRequestedVersion.RuntimeVersion != minimumRequiredPluginVersion)
                {
                    logger.Log("Preferred runtime version " + minimumRequiredPluginVersion + " was not found. Reverting to nearest available version " + runtimeToUse.NearestRequestedVersion.RuntimeVersion, LogLevel.Wrn);
                }

                return runtimeToUse.NearestRequestedVersion;
            }
            else
            {
                logger.Log("Preferred runtime version was not found. Reverting to highest known version " + runtimeToUse.HighestVersion.RuntimeVersion, LogLevel.Wrn);
                return runtimeToUse.HighestVersion;
            }
        }

        private async Task AttemptToReconcileLoadedPlugins()
        {
            ISet<PluginStrongName> pluginsLoadedThatShouldntBe = new HashSet<PluginStrongName>(_actuallyLoadedPlugins);
            pluginsLoadedThatShouldntBe.ExceptWith(_pluginsThatShouldBeLoaded);
            foreach (PluginStrongName toUnload in pluginsLoadedThatShouldntBe)
            {
                try
                {
                    _coreLogger.Log("Attempting to unload plugin " + toUnload);
                    await UnloadPluginInternal(toUnload, _coreLogger, DefaultRealTimeProvider.Singleton);
                }
                catch (Exception e)
                {
                    _coreLogger.Log(e, LogLevel.Err);
                }
            }

            ISet<PluginStrongName> pluginsUnloadedThatShouldBe = new HashSet<PluginStrongName>(_pluginsThatShouldBeLoaded);
            pluginsUnloadedThatShouldBe.ExceptWith(_actuallyLoadedPlugins);
            foreach (PluginStrongName toLoad in pluginsUnloadedThatShouldBe)
            {
                try
                {
                    _coreLogger.Log("Attempting to load plugin " + toLoad);
                    await LoadPluginInternal(toLoad, _coreLogger, DefaultRealTimeProvider.Singleton);
                }
                catch (Exception e)
                {
                    _coreLogger.Log(e, LogLevel.Err);
                }
            }
        }

        protected abstract GenericContainerHost InitializeContainerInternal(ContainerHostInitializationParameters containerParameters);

        private async Task<GenericContainerHost> InitializeContainer(VirtualPath pluginDir, IRealTimeProvider realTime, string containerName, PackageManifest pluginManifest)
        {
            try
            {
                string packageName = pluginDir.Name;
                ContainerRuntimeInformation runtimeInfo = SelectBestRuntimeForContainer(pluginManifest, _coreLogger);
                if (runtimeInfo == null)
                {
                    _coreLogger.Log("The package " + packageName + " has asked for a runtime which is not installed. Plugins will not be loaded", LogLevel.Crt);
                    return null;
                }

                _coreLogger.Log("Loading container " + packageName + " with runtime version " + runtimeInfo.FolderPath.Name);

                ContainerHostInitializationParameters containerParams = new ContainerHostInitializationParameters()
                {
                    ContainerName = containerName,
                    ServiceName = packageName,
                    PluginDirectory = pluginDir,
                    GlobalFileSystem = _globalFileSystem,
                    GlobalHttpClientFactory = _globalHttpClientFactory,
                    Logger = _coreLogger.Clone("ContainerHost-" + packageName),
                    RemotingProtocol = _dialogProtocol,
                    ServerSocketFactory = _serverSocketFactory,
                    UseDebugTimeouts = _useDebugTimeouts,
                    Metrics = _metrics,
                    ContainerDimensions = _dimensions,
                    RuntimeDirectory = runtimeInfo.FolderPath,
                    RemotingConfig = _remotingConfig,
                    RuntimeInformation = runtimeInfo
                };

                GenericContainerHost container = InitializeContainerInternal(containerParams);

                bool ok = await container.Start(realTime);
                if (!ok)
                {
                    return null;
                }

                container.ContainerBecameUnhealthyEvent.Subscribe(ContainerIsUnhealthyEventHandler);
                _coreLogger.Log("Container " + packageName + " fully initialized");
                return container;
            }
            catch (ReflectionTypeLoadException e)
            {
                _coreLogger.Log("Failed to create execution container " + pluginDir.FullName, LogLevel.Err);
                _coreLogger.Log(e, LogLevel.Err);
                foreach (Exception exception in e.LoaderExceptions)
                {
                    _coreLogger.Log(e.Message, LogLevel.Err);
                }
                return null;
            }
            catch (Exception e)
            {
                _coreLogger.Log("Failed to create execution container " + pluginDir.FullName, LogLevel.Err);
                _coreLogger.Log(e, LogLevel.Err);
                return null;
            }
        }

        private async Task ContainerIsUnhealthyEventHandler(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            // Restart the failing container
            GenericContainerHost failingContainer = sender as GenericContainerHost;
            if (failingContainer != null)
            {
                _coreLogger.Log("Container \"" + failingContainer.ServiceName + "\" has become unhealthy and will now be recycled...", LogLevel.Err);
                await RecycleContainer(failingContainer, realTime);
            }
            else
            {
                _coreLogger.Log("Got a ContainerIsUnhealthy event, but an invalid container was specified in arguments", LogLevel.Err);
            }
        }

        private async Task RecycleContainer(GenericContainerHost failingContainer, IRealTimeProvider realTime)
        {
            int packageInstallerReadLock = await _packageInstaller.EnterReadLockAsync();
            try
            {
                string existingContainerId = null;
                VirtualPath packageName = null;
                HashSet<PluginStrongName> pluginsToLoadInThisContainer = new HashSet<PluginStrongName>();
                int containerReadLock = await _lock.EnterReadLockAsync();
                try
                {
                    // Find out the container GUID using reverse lookup
                    foreach (var kvp in _containerNameToContainer)
                    {
                        if (kvp.Value == failingContainer)
                        {
                            existingContainerId = kvp.Key;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(existingContainerId))
                    {
                        _coreLogger.Log("Can't find a container that maps to the service name \"" + failingContainer.ServiceName + "\" to recycle! No way to recover", LogLevel.Err);
                        return;
                    }

                    // Find the package name using reverse lookup
                    foreach (var kvp in _packageNameToContainerName)
                    {
                        if (kvp.Value == existingContainerId)
                        {
                            packageName = kvp.Key;
                            break;
                        }
                    }

                    if (packageName == null)
                    {
                        _coreLogger.Log("Can't find a package that maps to the container guid \"" + existingContainerId + "\" to recycle! No way to recover", LogLevel.Err);
                        return;
                    }

                    foreach (PluginStrongName pluginId in _actuallyLoadedPlugins)
                    {
                        if (_pluginIdToPackageName.ContainsKey(pluginId) &&
                            _pluginIdToPackageName[pluginId] == packageName)
                        {
                            pluginsToLoadInThisContainer.Add(pluginId);
                        }
                    }
                }
                finally
                {
                    _lock.ExitReadLock(containerReadLock);
                }

                string newContainerId = packageName.NameWithoutExtension + "-" + Guid.NewGuid().ToString("N");
                VirtualPath pluginDir = _pluginDirectory.Combine(packageName.NameWithoutExtension);
                PackageManifest manifest = _packageNameToManifest[packageName];
                _coreLogger.Log("Initializing recycled container " + newContainerId + " to host plugin " + pluginDir.FullName + " (replacing failing container " + existingContainerId + ")");
                GenericContainerHost newContainer = await InitializeContainer(pluginDir, realTime, newContainerId, manifest);
                if (newContainer == null)
                {
                    // Failed to create container, so abort loading the plugin
                    _coreLogger.Log("Failed to recycle failing container \"" + existingContainerId + "\"", LogLevel.Err);
                    return;
                }

                // Load whatever plugins should be loaded in the new container
                foreach (PluginStrongName toLoad in pluginsToLoadInThisContainer)
                {
                    try
                    {
                        _coreLogger.Log("Attempting to load plugin " + toLoad + " into recycled container " + newContainerId);
                        PluginServices services = BuildPluginServices(toLoad, _coreLogger);
                        LoadedPluginInformation returnVal = await newContainer.LoadPlugin(toLoad, services, _coreLogger, realTime);
                        if (returnVal == null)
                        {
                            _coreLogger.Log("Error while loading plugin " + toLoad.ToString() + ". The returned information was null", LogLevel.Err);
                            return;
                        }

                        _coreLogger.Log("Successfully loaded plugin " + toLoad + " into recycled container " + newContainerId);
                    }
                    catch (Exception e)
                    {
                        _coreLogger.Log(e, LogLevel.Err);
                        return;
                    }
                }

                // Switch dictionary values to point to the new container
                // This call to enter the write lock may get starved out for a while depending on how loaded the server is. But it is guaranteed to succeed eventually.
                _coreLogger.Log("Requesting write lock to start container swap...");
                int containerWriteLock = await _lock.EnterWriteLockAsync();
                try
                {
                    _coreLogger.Log("Swapping failing container " + existingContainerId + " with newly created container " + newContainerId);
                    _packageNameToContainerName[packageName] = newContainerId;
                    _containerNameToContainer[newContainerId] = newContainer;
                }
                finally
                {
                    _lock.ExitWriteLock(containerWriteLock);
                }

                _coreLogger.Log("Disposing of failing container " + existingContainerId);
                failingContainer.Dispose();
            }
            finally
            {
                _packageInstaller.ExitReadLock(packageInstallerReadLock);
            }
        }

        private PluginServices BuildPluginServices(PluginStrongName pluginStrongName, ILogger logger)
        {
            return new PluginServices(
                pluginStrongName,
                new InMemoryConfiguration(logger),
                logger,
                NullFileSystem.Singleton,
                new NullLGEngine(),
                _entityResolver,
                _speechSynth,
                _speechReco,
                _oauthManager,
                new NullHttpClientFactory());
        }
    }
}
