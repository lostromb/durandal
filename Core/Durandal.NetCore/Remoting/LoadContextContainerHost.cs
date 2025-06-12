

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
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Remoting;
    using System.Threading.Tasks;

    public class LoadContextContainerHost : GenericContainerHost
    {
        private readonly DirectoryInfo _dialogServiceDirectory;
        private readonly DirectoryInfo _containerDirectory;
        private readonly DirectoryInfo _runtimeDirectory;
        private readonly string _serviceNameForDebug;
        private readonly bool _useDedicatedIpcThread;

        private DurandalPluginLoadContext _loadContext;
        private object _containerGuest;

        public LoadContextContainerHost(
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
            _useDedicatedIpcThread = containerParameters.RemotingConfig.UseDedicatedIpcThreads;

            if (!_containerDirectory.Exists)
            {
                _containerDirectory.Create();
            }
        }

        ~LoadContextContainerHost()
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
            try
            {
                List<MetricDimension> modifiedDimensions = new List<MetricDimension>();
                foreach (var dimension in containerDimensions)
                {
                    // Don't pass service version to the container; it will set those itself
                    if (!string.Equals(CommonInstrumentation.Key_Dimension_ServiceVersion, dimension.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        modifiedDimensions.Add(dimension);
                    }
                }

                ContainerGuestInitializationParameters initializationParameters = new ContainerGuestInitializationParameters()
                {
                    ContainerName = serviceName,
                    ContainerDimensions = new DimensionSet(modifiedDimensions.ToArray()),
                    ContainerLevelMessageBoxId = containerPermanentMailboxId,
                    SocketConnectionString = socketConnectionString,
                    UseDebugTimeouts = useDebugTimeouts,
                    ContainerDirectory = _containerDirectory.FullName,
                    DurandalBaseDirectory = _dialogServiceDirectory.FullName,
                    RemoteProtocolName = remoteProtocolName,
                    UseDedicatedPostOfficeThread = _useDedicatedIpcThread
                };

                string serializedInitializationParameters = JsonConvert.SerializeObject(initializationParameters);

                _loadContext = new DurandalPluginLoadContext(logger.Clone("PluginLoadContext"), _runtimeDirectory, _containerDirectory);
                AssemblyName assemblyName = typeof(LoadContextContainerGuest).Assembly.GetName();
                Assembly containerHostAssembly = _loadContext.LoadFromAssemblyName(assemblyName);
                if (containerHostAssembly == null)
                {
                    logger.Log("Could not find entry point dll " + assemblyName + " to use to create load context guest! It looks like this runtime version is broken or does not exist", LogLevel.Err);
                    return Task.FromResult(false);
                }

                string containerGuestTypeName = typeof(LoadContextContainerGuest).FullName;
                Type containerGuestType = containerHostAssembly.ExportedTypes.FirstOrDefault(t => string.Equals(t.FullName, containerGuestTypeName));
                _containerGuest = Activator.CreateInstance(containerGuestType);
                if (_containerGuest == null)
                {
                    logger.Log("Error while creating remoting proxy to load context container. This could be caused by breaking namespace or interface changes between runtime versions.", LogLevel.Err);
                    return Task.FromResult(false);
                }

                // For some reason we run into troubles when just trying to cast the returned object as an IContainerGuest (probably because the defining assemblies of the interface are different).
                // So we have to use reflection to find the initialization method and invoke it
                MethodInfo initializeMethodSig = _containerGuest.GetType().GetMethod(nameof(IContainerGuest.Initialize), new Type[] { typeof(string) });
                if (initializeMethodSig == null)
                {
                    logger.Log("Error while looking for Initialize method on load context container. This could be caused by breaking namespace or interface changes between runtime versions.", LogLevel.Err);
                    return Task.FromResult(false);
                }

                logger.Log("Initializing load context guest...");
                // Make sure we set the AssemblyLoadContext.CurrentContextualReflectionContext at initialization
                using (var scope = _loadContext.EnterContextualReflection())
                {
                    initializeMethodSig.Invoke(_containerGuest, new object[] { serializedInitializationParameters });
                }

                logger.Log("Finished initializing load context guest...");

                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Err);
                return Task.FromResult(false);
            }
        }

        protected override void StopInternalContainer(ILogger logger)
        {
            if (_containerGuest != null)
            {
                MethodInfo shutdownMethodSig = _containerGuest.GetType().GetMethod(nameof(IContainerGuest.Shutdown), Array.Empty<Type>());
                if (shutdownMethodSig != null)
                {
                    shutdownMethodSig.Invoke(_containerGuest, Array.Empty<object>());
                }
            }
        }

        protected override void DestroyInternalContainer(ILogger logger)
        {
            try
            {
                if (_loadContext != null)
                {
                    logger.Log("Unloading load context " + _serviceNameForDebug + "...");
                    _loadContext.Unload();
                    _loadContext = null;
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
