

namespace Durandal.Common.Remoting
{
    using Durandal.Common.File;
    using Durandal.Common.Instrumentation;
    using Durandal.Common.Logger;
    using Durandal.Common.Net;
    using Durandal.Common.Net.Http;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Remoting;
    using System.Threading.Tasks;

    public class AppDomainContainerHost : GenericContainerHost
    {
        private readonly DirectoryInfo _dialogServiceDirectory;
        private readonly DirectoryInfo _containerDirectory;
        private readonly DirectoryInfo _runtimeDirectory;
        private readonly string _serviceNameForDebug;
        private readonly bool _useDedicatedPostOfficeThread;

        private AppDomain _container;
        private ObjectHandle _hContainerHost;
        private IContainerGuest _containerGuest;
        private AppDomainContainerSponsor _containerSponsor;

        public AppDomainContainerHost(
            ContainerHostInitializationParameters containerParameters,
            DirectoryInfo dialogServiceDirectory,
            DirectoryInfo runtimeDirectory,
            DirectoryInfo containerDirectory)
            : base(containerParameters)
        {
            _dialogServiceDirectory = dialogServiceDirectory;
            _containerDirectory = containerDirectory;
            _serviceNameForDebug = containerParameters.ServiceName;
            _runtimeDirectory = runtimeDirectory;
            _useDedicatedPostOfficeThread = containerParameters.RemotingConfig.UseDedicatedIpcThreads;

            if (!_containerDirectory.Exists)
            {
                _containerDirectory.Create();
            }
        }

        ~AppDomainContainerHost()
        {
            Dispose(false);
        }

        protected override Task<bool> CreateInternalContainer(
            ILogger logger,
            string containerName,
            string serviceName,
            DimensionSet containerDimensions,
            uint containerPermanentMailboxId,
            string socketConnectionString,
            bool useDebugTimeouts,
            string remoteProtocolName)
        {
            AppDomain partiallyCreatedDomain = null;

            try
            {
                AppDomainSetup setup = new AppDomainSetup();
                setup.ApplicationBase = _runtimeDirectory.FullName;
                setup.PrivateBinPath = _runtimeDirectory.FullName;

                // this is needed so that we can pick up binding redirects defined by the container's project
                // for assemblies that get resolved from within the container's appdomain
                string bindingRedirectFile = _runtimeDirectory.FullName + ".\\Durandal.ContainerizedRuntime.exe.config";
                if (File.Exists(bindingRedirectFile))
                {
                    setup.ConfigurationFile = bindingRedirectFile;
                }
                else
                { 
                    logger.Log("Could not find container assembly configuration file \"" + bindingRedirectFile + "\". Assembly binding redirects may fail", LogLevel.Wrn);
                }

                // If this is set to MultiDomain, memory use goes down because of deduplication of Durandal.dll, but it also prevents us from entirely 
                // unloading the domain because plugin assemblies start getting loaded as domain-neutral and they lock down the dll files.
                // We also have to consider that we might be loading multiple versions of durandal.dll simultaneously
                setup.LoaderOptimization = LoaderOptimization.MultiDomainHost;

                List<MetricDimension> modifiedDimensions = new List<MetricDimension>();
                foreach (var dimension in containerDimensions)
                {
                    // Don't pass service version to the container, since the app domain might have a different runtime version
                    if (!string.Equals(CommonInstrumentation.Key_Dimension_ServiceVersion, dimension.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        modifiedDimensions.Add(dimension);
                    }
                }

                partiallyCreatedDomain = AppDomain.CreateDomain(containerName, null, setup);

                ContainerGuestInitializationParameters initializationParameters = new ContainerGuestInitializationParameters()
                {
                    ContainerName = serviceName,
                    ContainerDimensions = new DimensionSet(modifiedDimensions.ToArray()),
                    ContainerLevelMessageBoxId = containerPermanentMailboxId,
                    SocketConnectionString = socketConnectionString,
                    UseDebugTimeouts = useDebugTimeouts,
                    ContainerDirectory = _containerDirectory.FullName,
                    DurandalBaseDirectory = _dialogServiceDirectory.FullName,
                    UseDedicatedPostOfficeThread = _useDedicatedPostOfficeThread,
                    RemoteProtocolName = remoteProtocolName,
                };

                string serializedInitializationParameters = JsonConvert.SerializeObject(initializationParameters);

                // Create the remote container guest inside of the appdomain
                // instantiate a helper object derived from MarshalByRefObject in other domain
                string targetRuntimeDllName = typeof(AppDomainContainerGuest).Assembly.GetName().Name + ".dll";
                string targetRuntimeDllPath = _runtimeDirectory.FullName + Path.DirectorySeparatorChar + targetRuntimeDllName;
                if (!File.Exists(targetRuntimeDllPath))
                {
                    logger.Log("Could not find entry point dll " + targetRuntimeDllPath + " to use to create app domain guest! It looks like this runtime version is broken or does not exist", LogLevel.Err);
                    return Task.FromResult(false);
                }

                _hContainerHost = partiallyCreatedDomain.CreateInstanceFrom(targetRuntimeDllPath, typeof(AppDomainContainerGuest).FullName);
                object rawGuestProxyObject = _hContainerHost.Unwrap();

                _containerGuest = rawGuestProxyObject as IContainerGuest;
                MarshalByRefObject mbro = rawGuestProxyObject as MarshalByRefObject;

                if (_containerGuest == null || mbro == null)
                {
                    logger.Log("Error while creating remoting proxy to app domain container. This could be caused by breaking namespace or interface changes between runtime versions.", LogLevel.Err);
                    return Task.FromResult(false);
                }

                logger.Log("Initializing appdomain guest...");
                _containerGuest.Initialize(serializedInitializationParameters);
                logger.Log("Finished initializing appdomain guest...");

                // Create a sponsor that will keep a cross-domain memory reference to the guest container object to keep it from being garbage collected
                // see https://stackoverflow.com/questions/6339469/object-has-been-disconnected-or-does-not-exist-at-the-server-exception
                _containerSponsor = new AppDomainContainerSponsor(mbro);

                _container = partiallyCreatedDomain;
                partiallyCreatedDomain = null;
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Err);

                // Clean up if we broke the app domain halfway through
                try
                {
                    if (partiallyCreatedDomain != null)
                    {
                        AppDomain.Unload(partiallyCreatedDomain);
                    }
                }
                catch (Exception e2)
                {
                    logger.Log(e2, LogLevel.Err);
                }

                return Task.FromResult(false);
            }
        }

        protected override void StopInternalContainer(ILogger logger)
        {
            _containerGuest?.Shutdown();
        }

        protected override void DestroyInternalContainer(ILogger logger)
        {
            _containerSponsor?.Dispose();
            _containerSponsor = null;

            try
            {
                if (_container != null)
                {
                    logger.Log("Unloading app domain " + _serviceNameForDebug + "...");
                    AppDomain.Unload(_container);
                    _container = null;
                }

                logger.Log("Attempting to delete container directory " + _containerDirectory.Name + "...");
                _containerDirectory.Delete(true);
                logger.Log("Container cleanup succeeded");
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Wrn);
            }
        }
    }
}
