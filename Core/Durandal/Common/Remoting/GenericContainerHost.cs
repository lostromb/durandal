using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Packages;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using Durandal.Common.Remoting.Handlers;
using Durandal.Common.Events;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting
{
    public abstract class GenericContainerHost : IDisposable
    {
        private static readonly TimeSpan CONTAINER_STARTUP_TIMEOUT = TimeSpan.FromSeconds(10);

        private readonly ILogger _logger;
        private readonly string _serviceName;
        private readonly string _containerName;
        private readonly IRemoteDialogProtocol _remoteProtocol;
        private readonly IFileSystem _globalFileSystem;
        private readonly IHttpClientFactory _globalHttpClientFactory;
        private readonly CancellationTokenSource _shutdownCancelizer;
        private readonly bool _useDebugTimeouts;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _dimensions;
        private readonly IServerSocketFactory _serverSocketFactory;
        private readonly RemotingConfiguration _remotingConfig;

        private bool _started = false;
        private bool _loadedOk = false;
        private ISocket _serverSocket;
        private PostOffice _serverPostOffice;
        private RemoteDialogExecutorClient _dialogClient;
        private ContainerKeepaliveManager _keepAliveManager;
        private Task _containerLevelMessageDispatcher;
        private int _disposed = 0;

        /// <summary>
        /// Event that fires if the quality of service goes below the threshold.
        /// </summary>
        public AsyncEvent<EventArgs> ContainerBecameUnhealthyEvent { get; private set; }

        public string ServiceName => _serviceName;

        public GenericContainerHost(ContainerHostInitializationParameters containerParameters)
        {
            _serviceName = containerParameters.ServiceName.AssertNonNullOrEmpty(nameof(_serviceName));
            _logger = containerParameters.Logger.AssertNonNull(nameof(_logger));
            _containerName = containerParameters.ContainerName.AssertNonNullOrEmpty(nameof(_containerName));
            _remoteProtocol = containerParameters.RemotingProtocol.AssertNonNull(nameof(_remoteProtocol));
            _globalFileSystem = containerParameters.GlobalFileSystem.AssertNonNull(nameof(_globalFileSystem));
            _shutdownCancelizer = new CancellationTokenSource();
            _useDebugTimeouts = containerParameters.UseDebugTimeouts;
            if (_useDebugTimeouts)
            {
                _logger.Log("Execution container timeouts are set for DEBUG mode");
            }

            _metrics = containerParameters.Metrics.AssertNonNull(nameof(_metrics));
            _dimensions = containerParameters.ContainerDimensions.AssertNonNull(nameof(_dimensions));
            _globalHttpClientFactory = containerParameters.GlobalHttpClientFactory.AssertNonNull(nameof(_globalHttpClientFactory));
            _serverSocketFactory = containerParameters.ServerSocketFactory.AssertNonNull(nameof(_serverSocketFactory));
            _remotingConfig = containerParameters.RemotingConfig.AssertNonNull(nameof(_remotingConfig));
            ContainerBecameUnhealthyEvent = new AsyncEvent<EventArgs>();

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public async Task<bool> Start(IRealTimeProvider realTime)
        {
            if (_started)
            {
                throw new InvalidOperationException("Cannot start a container more than once");
            }

            _started = true;
            _logger.Log("Creating container \"" + _containerName + "\"");

            DimensionSet containerDimensions = _dimensions
                .Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_ContainerName, _serviceName));

            _serverSocket = _serverSocketFactory.CreateServerSocket(_logger, _metrics, containerDimensions);

            // Create a post office to send messages over this socket
            TimeSpan postOfficeTimeout = _useDebugTimeouts ? TimeSpan.FromMinutes(10) : TimeSpan.FromSeconds(30);
            _serverPostOffice = new PostOffice(
                _serverSocket,
                _logger.Clone("ContainerHostPO-" + _serviceName),
                postOfficeTimeout,
                isServer: false,
                realTime: DefaultRealTimeProvider.Singleton,
                metrics: _metrics,
                metricDimensions: containerDimensions,
                useDedicatedThread: _remotingConfig.UseDedicatedIpcThreads);
            MailboxId containerLevelMessageBoxId = _serverPostOffice.CreatePermanentMailbox(DefaultRealTimeProvider.Singleton, 1);

            _logger.Log("Container guest initializing...");

            bool initializeOk = await CreateInternalContainer(
                _logger,
                _containerName,
                _serviceName,
                containerDimensions,
                containerLevelMessageBoxId.Id,
                _serverSocket.RemoteEndpointString,
                _useDebugTimeouts,
                _remotingConfig.IpcProtocol).ConfigureAwait(false);
            if (!initializeOk)
            {
                _logger.Log("Container guest failed to initialize");
                return false;
            }

            // Wait for container to send the alive message on PO box
            _logger.Log("Waiting for startup signal...");
            using (NonRealTimeCancellationTokenSource startupTimeout = new NonRealTimeCancellationTokenSource(realTime, CONTAINER_STARTUP_TIMEOUT))
            {
                try
                {
                    MailboxMessage m = await _serverPostOffice.ReceiveMessage(containerLevelMessageBoxId, startupTimeout.Token, realTime);
                    m.DisposeOfBuffer();
                }
                catch (OperationCanceledException)
                {
                    _logger.Log("Container did not initialize in time", LogLevel.Err);
                    return false;
                }
            }

            _logger.Log("Container guest initialized successfully");

            _dialogClient = new RemoteDialogExecutorClient(
                _remoteProtocol,
                new WeakPointer<PostOffice>(_serverPostOffice),
                _logger.Clone("AppDomainHostDialogClient"),
                _metrics,
                containerDimensions,
                _useDebugTimeouts);

            // Start a task to read messages from container, for service-level logs and remoted filesystem access
            _logger.Log("Starting container-level remote message orchestrator");
            _containerLevelMessageDispatcher = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(async () =>
            {
                try
                {
                    _logger.Log("Started container-level remote message orchestrator");
                    RemoteProcedureRequestOrchestrator containerLevelRemotedServiceOrchestrator = new RemoteProcedureRequestOrchestrator(
                        _remoteProtocol,
                        new WeakPointer<PostOffice>(_serverPostOffice),
                        _logger,
                        new LoggerRemoteProcedureRequestHandler(_logger),
                        new FileSystemRemoteProcedureRequestHandler(_globalFileSystem, _logger.Clone("FileSystemRemoteHandler")),
                        new MetricRemoteProcedureRequestHandler(_metrics.Value),
                        new HttpRemoteProcedureRequestHandler(_globalHttpClientFactory));

                    while (!_shutdownCancelizer.IsCancellationRequested)
                    {
                        MailboxMessage message = await _serverPostOffice.ReceiveMessage(
                            containerLevelMessageBoxId,
                            _shutdownCancelizer.Token,
                            DefaultRealTimeProvider.Singleton);

                        if (!_shutdownCancelizer.IsCancellationRequested)
                        {
                            Tuple<object, Type> parsedMessage = _remoteProtocol.Parse(message.Buffer, _logger);
                            await containerLevelRemotedServiceOrchestrator.HandleIncomingMessage(
                                parsedMessage,
                                message,
                                _shutdownCancelizer.Token,
                                DefaultRealTimeProvider.Singleton);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
                finally
                {
                    _logger.Log("Stopped container-level remote message orchestrator");
                }
            });

            _keepAliveManager = new ContainerKeepaliveManager(
                _serverPostOffice,
                _metrics,
                containerDimensions,
                _logger.Clone("KeepAliveManager-" + _serviceName),
                _remoteProtocol,
                _remotingConfig);

            await _keepAliveManager.Start(DefaultRealTimeProvider.Singleton);
            _keepAliveManager.HealthCrossedFailureThresholdEvent.Subscribe(ContainerIsUnhealthyEventHandler);

            _loadedOk = true;
            return true;
        }

        public Task<DialogProcessingResponse> LaunchPlugin(
            PluginStrongName pluginId,
            string entryPoint,
            bool isRetry,
            QueryWithContext query,
            PluginServices services,
            ILogger queryLogger,
            IRealTimeProvider realTime)
        {
            if (_loadedOk)
            {
                return _dialogClient.LaunchPlugin(pluginId, entryPoint, isRetry, query, services, queryLogger, realTime);
            }
            else
            {
                return Task.FromResult<DialogProcessingResponse>(new DialogProcessingResponse(new PluginResult(Result.Skip), false));
            }
        }

        public Task<IEnumerable<PluginStrongName>> GetAllAvailablePlugins(IRealTimeProvider realTime)
        {
            if (_loadedOk)
            {
                return _dialogClient.GetAllAvailablePlugins(realTime);
            }
            else
            {
                return Task.FromResult<IEnumerable<PluginStrongName>>(new PluginStrongName[0]);
            }
        }

        public Task<TriggerProcessingResponse> TriggerPlugin(
            PluginStrongName pluginId,
            QueryWithContext query,
            PluginServices services,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            if (_loadedOk)
            {
                return _dialogClient.TriggerPlugin(pluginId, query, services, logger, realTime);
            }
            else
            {
                return Task.FromResult<TriggerProcessingResponse>(new TriggerProcessingResponse(new TriggerResult(BoostingOption.Suppress), new InMemoryDataStore()));
            }
        }

        public Task<CrossDomainRequestData> CrossDomainRequest(
            PluginStrongName pluginId,
            string targetIntent,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            if (_loadedOk)
            {
                return _dialogClient.CrossDomainRequest(pluginId, targetIntent, logger, realTime);
            }
            else
            {
                return Task.FromResult<CrossDomainRequestData>(null);
            }
        }

        public Task<CrossDomainResponseResponse> CrossDomainResponse(
            PluginStrongName pluginId,
            CrossDomainContext context,
            PluginServices services,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            if (_loadedOk)
            {
                return _dialogClient.CrossDomainResponse(pluginId, context, services, logger, realTime);
            }
            else
            {
                return Task.FromResult<CrossDomainResponseResponse>(null);
            }
        }

        public Task<LoadedPluginInformation> LoadPlugin(
            PluginStrongName plugin,
            PluginServices services,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            if (_loadedOk)
            {
                return _dialogClient.LoadPlugin(plugin, services, logger, realTime);
            }
            else
            {
                return Task.FromResult<LoadedPluginInformation>(null);
            }
        }

        public Task<bool> UnloadPlugin(
            PluginStrongName plugin,
            PluginServices services,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            if (_loadedOk)
            {
                return _dialogClient.UnloadPlugin(plugin, services, logger, realTime);
            }
            else
            {
                return Task.FromResult<bool>(false);
            }
        }

        public Task<CachedWebData> FetchPluginViewData(
            PluginStrongName plugin,
            string path,
            DateTimeOffset? ifModifiedSince,
            ILogger logger,
            IRealTimeProvider realTime)
        {
            if (_loadedOk)
            {
                return _dialogClient.FetchPluginViewData(plugin, path, ifModifiedSince, logger, realTime);
            }
            else
            {
                return Task.FromResult<CachedWebData>(new CachedWebData());
            }
        }

#if DEBUG
        public Task _UnitTesting_CrashContainer(IRealTimeProvider realTime)
        {
            if (_loadedOk)
            {
                return _dialogClient._UnitTesting_CrashContainer(realTime);
            }
            else
            {
                return DurandalTaskExtensions.NoOpTask;
            }
        }
#endif

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract Task<bool> CreateInternalContainer(
            ILogger logger,
            string containerName,
            string serviceName,
            DimensionSet containerDimensions,
            uint containerPermanentMailboxId,
            string socketConnectionString,
            bool useDebugTimeouts,
            string remoteProtocolName);

        /// <summary>
        /// Instructs the container guest to stop operation in anticipation of disposal
        /// </summary>
        /// <param name="logger"></param>
        protected abstract void StopInternalContainer(ILogger logger);

        /// <summary>
        /// Instructs the container guest to dispose of all of its resources and unload
        /// </summary>
        /// <param name="logger"></param>
        protected abstract void DestroyInternalContainer(ILogger logger);

        private Task ContainerIsUnhealthyEventHandler(object sender, EventArgs args, IRealTimeProvider realTime)
        {
            return ContainerBecameUnhealthyEvent.Fire(this, args, realTime);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (_started)
            {
                _keepAliveManager?.Stop().Await();
                _keepAliveManager?.HealthCrossedFailureThresholdEvent.TryUnsubscribe(ContainerIsUnhealthyEventHandler);

                try
                {
                    StopInternalContainer(_logger);
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }

                _shutdownCancelizer.Cancel();
                _containerLevelMessageDispatcher?.Await();

                if (disposing)
                {
                    _keepAliveManager?.Dispose();
                    _serverPostOffice?.Dispose();
                    _serverSocket?.Disconnect(CancellationToken.None, DefaultRealTimeProvider.Singleton).Await();
                    _serverSocket?.Dispose();
                }

                _serverSocket = null;
                _serverPostOffice = null;

                try
                {
                    DestroyInternalContainer(_logger);
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
            }
        }
    }
}
