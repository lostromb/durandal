
namespace Durandal.Common.Remoting
{
    using Durandal.Common.Dialog.Runtime;
    using Durandal.Common.Dialog.Services;
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.LG.Statistical;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Durandal.Common.NLP;
    using Durandal.Common.Speech.SR;
    using Durandal.Common.Speech.TTS;
    using Durandal.Common.Time;
    using Durandal.Common.Utils;
    using Durandal.Extensions.BondProtocol;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;

    /// <summary>
    /// Plugin provider which operates with this design:
    /// - The constructor takes a directory on the filesystem, each containing subfolders. Each subfolder will become a container.
    /// - For each container (folder), spawn a separate process and load all DLLs inside the folder into that process
    /// - Create anonymous pipes to communicate with each process via remoting
    /// - Maintain a mapping of plugin strong name, version, etc. available in each container, as well as which plugins are initialized.
    /// - Route requests to each container which will process the actual execution of each plugin
    /// - If a single container reports that it is unhealthy, recycle the process and route requests to a replacement container
    /// </summary>
    public class ProcessIsolatedPluginProvider : ContainerizedPluginProvider
    {
        private readonly DirectoryInfo _tempDirectory;
        private readonly DirectoryInfo _dialogRootDirectory;
        private readonly RealFileSystem _tempFileSystem;

        private ProcessIsolatedPluginProvider(
            ILogger logger,
            IFileSystem globalFileSystem,
            DirectoryInfo dialogRootDirectory,
            IHttpClientFactory globalHttpClientFactory,
            DirectoryInfo tempDirectory,
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
            RemotingConfiguration remotingConfig,
            string preferredRuntimeFramework) : base(
                logger: logger,
                globalFileSystem: globalFileSystem,
                globalHttpClientFactory: globalHttpClientFactory,
                entityResolver: entityResolver,
                speechSynthesizer: speechSynthesizer,
                speechRecognizer: speechRecognizer,
                oauthManager: oauthManager,
                useDebugTimeouts: useDebugTimeouts,
                metrics: metrics,
                dimensions: dimensions,
                serverSocketFactory: serverSocketFactory,
                realTime: realTime,
                remotingProtocol: remotingProtocol,
                preferredRuntime: preferredRuntimeFramework,
                allowedRuntimes: new HashSet<string>() { DialogRuntimeFramework.RUNTIME_NETCORE , DialogRuntimeFramework.RUNTIME_NETFRAMEWORK }, // for process isolation we don't care what framework runs the plugin
                remotingConfig: remotingConfig)
        {
            _tempDirectory = tempDirectory;
            _dialogRootDirectory = dialogRootDirectory;
            _tempFileSystem = new RealFileSystem(logger, _tempDirectory.FullName);
        }

        /// <summary>
        /// Creates a containerized plugin provider.
        /// </summary>
        /// <param name="logger">The static logger to use for the plugin provider.</param>
        /// <param name="globalFileSystem">The global filesystem, meaning it refers to the root directory of a Durandal installation, with /plugins, /cache, /data directories, etc.</param>
        /// <param name="dialogRootDirectory">Root directory of durandal environment</param>
        /// <param name="globalHttpClientFactory">An http client factory to use for plugin services</param>
        /// <param name="tempDirectory">The temporary directory on the physical file system to place temporary container directories.</param>
        /// <param name="entityResolver">An entity resolver to be used by plugins</param>
        /// <param name="speechSynthesizer">A speech synthesizer to be used by plugins</param>
        /// <param name="speechRecognizer">A speech recognizer to be used by plugins</param>
        /// <param name="oauthManager">An oauth manager to be used by plugins</param>
        /// <param name="realTime">A definition of real time - not really used "properly" since it can't cross the remoting barrier</param>
        /// <param name="remotingProtocol"></param>
        /// <param name="metrics"></param>
        /// <param name="dimensions"></param>
        /// <param name="serverSocketFactory"></param>
        /// <param name="remotingConfig"></param>
        /// <param name="preferredRuntimeFramework">One of the DialogRuntimeFramework enumerations specifying which runtime we'd prefer to run</param>
        /// <param name="useDebugTimeouts"></param>
        /// <returns></returns>
        public static async Task<ProcessIsolatedPluginProvider> Create(
            ILogger logger,
            IFileSystem globalFileSystem,
            DirectoryInfo dialogRootDirectory,
            IHttpClientFactory globalHttpClientFactory,
            DirectoryInfo tempDirectory,
            IEntityResolver entityResolver,
            ISpeechSynth speechSynthesizer,
            ISpeechRecognizerFactory speechRecognizer,
            IOAuthManager oauthManager,
            IRealTimeProvider realTime,
            IRemoteDialogProtocol remotingProtocol,
            IMetricCollector metrics,
            DimensionSet dimensions,
            IServerSocketFactory serverSocketFactory,
            RemotingConfiguration remotingConfig,
            string preferredRuntimeFramework,
            bool useDebugTimeouts = false)
        {
            if (!tempDirectory.Exists)
            {
                tempDirectory.Create();
            }

            logger.Log("Cleaning out old containers from " + tempDirectory.FullName);
            foreach (DirectoryInfo subDir in tempDirectory.EnumerateDirectories())
            {
                subDir.Delete(true);
            }

            ProcessIsolatedPluginProvider returnVal = new ProcessIsolatedPluginProvider(
                logger,
                globalFileSystem,
                dialogRootDirectory,
                globalHttpClientFactory,
                tempDirectory,
                entityResolver,
                speechSynthesizer,
                speechRecognizer,
                oauthManager,
                useDebugTimeouts,
                metrics,
                dimensions,
                serverSocketFactory,
                realTime,
                remotingProtocol,
                remotingConfig,
                preferredRuntimeFramework);

            await returnVal.Initialize(realTime);
            return returnVal;
        }

        protected override GenericContainerHost InitializeContainerInternal(ContainerHostInitializationParameters containerParameters)
        {
            ILogger logger = containerParameters.Logger.AssertNonNull(nameof(logger));

            try
            {
                // Copy each container to a temp directory on the actual filesystem
                logger.Log("Mapping plugin directory " + containerParameters.PluginDirectory.Name + " to virtual container " + containerParameters.ContainerName);
                FileHelpers.CopyAllFiles(containerParameters.GlobalFileSystem, containerParameters.PluginDirectory, _tempFileSystem, new VirtualPath(containerParameters.ContainerName), logger);

                DirectoryInfo runtimeDirectory = new DirectoryInfo(_dialogRootDirectory.FullName + containerParameters.RuntimeDirectory.FullName.Replace('/', '\\'));

                // And then create the process in this temp directory. This keeps the original plugin files unlocked so we can reload or reinstall them without having to coordinate closely with the running processes
                DirectoryInfo containerDirectory = new DirectoryInfo(_tempDirectory.FullName + Path.DirectorySeparatorChar + containerParameters.ContainerName);
                GenericContainerHost container = new ProcessContainerHost(
                    containerParameters,
                    dialogServiceDirectory: _dialogRootDirectory,
                    containerRuntimeDirectory: runtimeDirectory,
                    containerDirectory: containerDirectory);

                return container;
            }
            catch (ReflectionTypeLoadException e)
            {
                logger.Log("Failed to create execution container " + containerParameters.ContainerName, LogLevel.Err);
                logger.Log(e, LogLevel.Err);
                foreach (Exception exception in e.LoaderExceptions)
                {
                    logger.Log(e.Message, LogLevel.Err);
                }
                return null;
            }
            catch (Exception e)
            {
                logger.Log("Failed to create execution container " + containerParameters.ContainerName, LogLevel.Err);
                logger.Log(e, LogLevel.Err);
                return null;
            }
        }

        /// <summary>
        /// Static factory method for compatability with Inque test driver. Implements the <see cref="Durandal.Common.Test.InqueTestDriver.BuildPluginProviderDelegate"/> delegate.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="loader"></param>
        /// <param name="fileSystem"></param>
        /// <param name="nlTools"></param>
        /// <param name="entityResolver"></param>
        /// <param name="speechSynth"></param>
        /// <param name="speechReco"></param>
        /// <param name="oauthManager"></param>
        /// <param name="httpClientFactory"></param>
        /// <param name="lgScriptCompiler"></param>
        /// <param name="realTime"></param>
        /// <param name="remotingConfig"></param>
        /// <returns></returns>
        public static async Task<IDurandalPluginProvider> BuildContainerizedPluginProvider(
            ILogger logger,
            IDurandalPluginLoader loader,
            IFileSystem fileSystem,
            IDictionary<string, NLPTools> nlTools,
            IEntityResolver entityResolver,
            ISpeechSynth speechSynth,
            ISpeechRecognizerFactory speechReco,
            IOAuthManager oauthManager,
            IHttpClientFactory httpClientFactory,
            ILGScriptCompiler lgScriptCompiler,
            IRealTimeProvider realTime,
            RemotingConfiguration remotingConfig)
        {
            string environmentDir = ServiceCommon.GetDurandalEnvironmentDirectory(new string[0], logger);
            return await Create(
                    logger,
                    fileSystem,
                    new DirectoryInfo(environmentDir),
                    httpClientFactory,
                    new DirectoryInfo(".\\temp_containers"),
                    entityResolver,
                    speechSynth,
                    speechReco,
                    oauthManager,
                    realTime,
                    new BondRemoteDialogProtocol(),
                    NullMetricCollector.Singleton, // No metrics are tracked during unit tests. I assume this is OK
                    DimensionSet.Empty,
                    new AnonymousPipeServerSocketFactory(),
                    remotingConfig,
                    DialogRuntimeFramework.RUNTIME_NETFRAMEWORK,
                    Debugger.IsAttached);
        }
    }
}
