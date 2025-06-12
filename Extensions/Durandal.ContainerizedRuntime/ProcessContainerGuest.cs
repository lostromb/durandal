using Durandal.Extensions.BondProtocol;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.File;
using Durandal.Common.Instrumentation;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;
using Durandal.Common.NLP.Language.English;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.MathExt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Durandal.Common.IO;
using Durandal.Common.Utils;
using Durandal.Common.Remoting.Proxies;
using Durandal.Common.Remoting;
using Durandal.Common.Collections;
using Durandal.Common.Instrumentation.Profiling;
using Durandal.API;
using Durandal.Common.ServiceMgmt;

namespace Durandal.ContainerizedRuntime
{
    /// <summary>
    /// This class manages a single process-level plugin container on the the container (guest) side.
    /// In other words, this is the code that composes the majority of the containerized process logic.
    /// It has one main method, Run(), which initializes the container and listens for messages.
    /// </summary>
    public class ProcessContainerGuest : IDisposable
    {
        private readonly CancellationTokenSource _shutdownSignal = new CancellationTokenSource();
        private Process _parentProcess;
        private ISocket _pipe;
        private IDurandalPluginLoader _pluginLoader;
        private RemoteDialogExecutorServer _pipeServer;
        private RemoteDialogMethodDispatcher _remoteContainerServiceDispatcher;
        private PostOffice _serverPostOffice;
        private RemotedLogger _remotedLogger;
        private IFileSystem _remotedFileSystem;
        private IHttpClientFactory _remotedHttpFactory;
        private RemotedMetricCollector _remotedMetricCollector;
        private IList<IRemoteDialogProtocol> _dialogProtocols;
        private DimensionSet _metricDimensions;
        private ILogger _bootstrapLogger;
        private int _disposed = 0;

        public ProcessContainerGuest()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ProcessContainerGuest()
        {
            Dispose(false);
        }
#endif

        public async Task Run(ProcessContainerGuestInitializationParams initializationParameters)
        {
            // Extract properties from the app domain to determine base path, dll search path, and friendly name for logging
            string containerName = initializationParameters.ContainerName;
            string baseDirectory = initializationParameters.DurandalBaseDirectory;
            string pipeUri = initializationParameters.SocketConnectionString;
            _bootstrapLogger = new ImmutableLogger(new ConsoleLogger("ProcessDebug-" + containerName));
            //MicroProfiler.Initialize(await FileMicroProfilerClient.CreateAsync("Durandal.ContainerizedRuntime." + containerName), _bootstrapLogger.Clone("Microprofiler"));
            _bootstrapLogger.Log("Bootstrapping process container guest: " + containerName);
            _bootstrapLogger.Log("Base directory is: " + baseDirectory);
            _bootstrapLogger.Log("Container directory is: " + initializationParameters.ContainerDirectory);
            _bootstrapLogger.Log("Pipe URI: " + pipeUri);
            _bootstrapLogger.Log("Post office box: " + initializationParameters.ContainerLevelMessageBoxId);
            _bootstrapLogger.Log("Finding parent process " + initializationParameters.ParentProcessId);
            try
            {
                _parentProcess = Process.GetProcessById(initializationParameters.ParentProcessId);
                _parentProcess.EnableRaisingEvents = true;
            }
            catch (Exception)
            {
                _parentProcess = null;
            }

            if (_parentProcess == null)
            {
                _bootstrapLogger.Log("FAILED to find parent process", LogLevel.Err);
            }

            CancellationToken shutdownToken = _shutdownSignal.Token;

            _bootstrapLogger.Log("Initializing local services...");


            IRealTimeProvider realTime = DefaultRealTimeProvider.Singleton;
            IRemoteDialogProtocol containerLevelProtocol;
            if (string.Equals("bond", initializationParameters.RemoteProtocolName, StringComparison.OrdinalIgnoreCase))
            {
                containerLevelProtocol = new BondRemoteDialogProtocol();
                _bootstrapLogger.Log("Using Bond remote dialog protocol");
            }
            else
            {
                containerLevelProtocol = new JsonRemoteDialogProtocol();
                _bootstrapLogger.Log("Using Json remote dialog protocol");
            }

            IThreadPool localThreadPool = new TaskThreadPool();
            RealFileSystem containerFileSystem = new RealFileSystem(_bootstrapLogger.Clone("FileSystem-" + containerName), initializationParameters.ContainerDirectory);
            RealFileSystem durandalEnvFileSystem = new RealFileSystem(_bootstrapLogger.Clone("FileSystem-" + containerName), initializationParameters.DurandalBaseDirectory);
            _metricDimensions = initializationParameters.ContainerDimensions;
            DeferredInstantiationMetricCollector deferredMetricCollector = new DeferredInstantiationMetricCollector();

            _metricDimensions = _metricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceName, "Durandal.ContainerizedRuntime"));
            _metricDimensions = _metricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceVersion, SVNVersionInfo.AssemblyVersion));

            _bootstrapLogger.Log("Initializing remoted services; pipe URI is " + pipeUri);
            if (pipeUri.StartsWith(MMIOClientSocket.PROTOCOL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
#if NETCOREAPP
                if (OperatingSystem.IsWindows())
                {
                    _pipe = new MMIOClientSocket(pipeUri, _bootstrapLogger.Clone("MMIOClient"), deferredMetricCollector, _metricDimensions);
                }
                else
                {
                    throw new Exception("MMIO IPC implementatin is only supported on Windows. Use \"pipe\" instead.");
                }
#else
                _pipe = new MMIOClientSocket(pipeUri, _bootstrapLogger.Clone("MMIOClient"), deferredMetricCollector, _metricDimensions);
#endif
            }
            else if (pipeUri.StartsWith(AnonymousPipeClientSocket.PROTOCOL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                _pipe = new AnonymousPipeClientSocket(pipeUri);
            }
            else if (pipeUri.StartsWith(RawTcpSocket.PROTOCOL_SCHEME, StringComparison.OrdinalIgnoreCase))
            {
                _pipe = new RawTcpSocket(pipeUri);
            }
            else
            {
                throw new Exception("Unknown IPC implementation " + pipeUri);
            }

            MailboxId diagnosticMailbox = new MailboxId(initializationParameters.ContainerLevelMessageBoxId);

            TimeSpan postOfficeTimeout = initializationParameters.UseDebugTimeouts ? TimeSpan.FromMinutes(10) : TimeSpan.FromSeconds(30);
            _serverPostOffice = new PostOffice(
                _pipe,
                _bootstrapLogger,
                postOfficeTimeout,
                isServer: true,
                realTime: realTime,
                metrics: new WeakPointer<IMetricCollector>(deferredMetricCollector),
                metricDimensions: _metricDimensions,
                useDedicatedThread: initializationParameters.UseDedicatedPostOfficeThread);

            // Tell the host that we are alive.
            // Technically we should wait until we are actually completely started up before we send this. However,
            // the host expects this to be the very first message sent. If we instead send something like a
            // logging message from the container level dispatcher below, that would get mixed up in the protocol and cause issues.
            PooledBuffer<byte> aliveMessage = BufferPool<byte>.Rent(5);
            Encoding.ASCII.GetBytes("ALIVE", 0, 5, aliveMessage.Buffer, 0);
            await _serverPostOffice.SendMessage(new MailboxMessage(diagnosticMailbox, 0, aliveMessage), shutdownToken, realTime);

            _remoteContainerServiceDispatcher = new RemoteDialogMethodDispatcher(_serverPostOffice, diagnosticMailbox, _bootstrapLogger, containerLevelProtocol);
            _remotedLogger = new RemotedLogger(new WeakPointer<RemoteDialogMethodDispatcher>(_remoteContainerServiceDispatcher), realTime, _bootstrapLogger, "Container-" + containerName);
            _remotedFileSystem = new RemotedFileSystem(new WeakPointer<RemoteDialogMethodDispatcher>(_remoteContainerServiceDispatcher), _remotedLogger, realTime);
            _remotedHttpFactory = new RemotedHttpClientFactory(_remoteContainerServiceDispatcher, realTime, _remotedLogger);
            _remotedMetricCollector = new RemotedMetricCollector(_remotedLogger, _remoteContainerServiceDispatcher, realTime);
            deferredMetricCollector.SetCollectorImplementation(_remotedMetricCollector);

#if NETCOREAPP
            // Report local process metrics to the parent. It's instanced by exact process ID so we should be able to keep track of resource usage for individual containers
            if (OperatingSystem.IsWindows())
            {
                _remotedMetricCollector.AddMetricSource(new WindowsPerfCounterReporter(
                    _remotedLogger,
                    _metricDimensions,
                    WindowsPerfCounterSet.BasicCurrentProcess));
            }

            _remotedMetricCollector.AddMetricSource(new NetCorePerfCounterReporter(_metricDimensions));
#else
            // Report local process metrics to the parent. It's instanced by exact process ID so we should be able to keep track of resource usage for individual containers
            _remotedMetricCollector.AddMetricSource(new WindowsPerfCounterReporter(
                    _remotedLogger,
                    _metricDimensions,
                    WindowsPerfCounterSet.BasicCurrentProcess |
                    WindowsPerfCounterSet.DotNetClrCurrentProcess));
#endif

            _dialogProtocols = new List<IRemoteDialogProtocol>()
            {
                new JsonRemoteDialogProtocol(),
                new BondRemoteDialogProtocol(),
            };

            NLPTools englishNlTools = new NLPTools()
            {
                CultureInfoFactory = new WindowsCultureInfoFactory(),
                LGFeatureExtractor = new EnglishLGFeatureExtractor(),
                FeaturizationWordBreaker = new EnglishWordBreaker(),
                WordBreaker = new EnglishWholeWordBreaker(),
            };

            NLPToolsCollection nlToolsForLg = new NLPToolsCollection();
            nlToolsForLg.Add(LanguageCode.EN_US, englishNlTools);

            ILogger aggregateLogger = new AggregateLogger(
                _remotedLogger.ComponentName,
                localThreadPool,
                _remotedLogger,
                _bootstrapLogger);

            aggregateLogger.Log("Creating dialog plugin loader and executor...");

            _pluginLoader = new ResidentDllPluginLoader(
                new BasicDialogExecutor(false),
                aggregateLogger.Clone("DllLoader-" + containerName),
                containerFileSystem,
                VirtualPath.Root,
                durandalEnvFileSystem,
                PluginFrameworkLevel.NetFull,
                false);

            _pipeServer = new RemoteDialogExecutorServer(
                aggregateLogger,
                new WeakPointer<PostOffice>(_serverPostOffice),
                _pluginLoader,
                _dialogProtocols,
                new WeakPointer<IThreadPool>(localThreadPool),
                _remotedFileSystem,
                _remotedHttpFactory,
#if NETCOREAPP
                new RoslynLGScriptCompiler(),
#elif NETFRAMEWORK
                new CodeDomLGScriptCompiler(),
#endif
               nlToolsForLg,
               new WeakPointer<IMetricCollector>(_remotedMetricCollector),
               _metricDimensions);

            _pipeServer.Start(realTime);

            AppDomain.CurrentDomain.UnhandledException += PrintUnhandledException;
            aggregateLogger.Log("Process container guest " + containerName + " initialized");

            // Create a hook that will shut down this process when its parent exits
            if (_parentProcess != null)
            {
                _parentProcess.Exited += ParentProcessExited;
            }

            // Listen on stdin for the "EXIT" signal
            await ListenForExitOnStdIn(shutdownToken).ConfigureAwait(false);
        }

        private async Task ListenForExitOnStdIn(CancellationToken cancelToken)
        {
            char[] buffer = new char[4];
            int charIdx = 0;
            while (charIdx < 4 && !cancelToken.IsCancellationRequested)
            {
                if (Console.In.Peek() < 0)
                {
                    await Task.Delay(100);
                }
                else
                {
                    int amountRead = await Console.In.ReadAsync(buffer, charIdx, 4 - charIdx).ConfigureAwait(false);
                    if (amountRead > 0)
                    {
                        charIdx += amountRead;
                    }
                }
            }

            if (charIdx == 4 &&
                    buffer[0] == 'E' &&
                    buffer[1] == 'X' &&
                    buffer[2] == 'I' &&
                    buffer[3] == 'T')
            {
                _bootstrapLogger.Log("Got EXIT signal. Shutting down...");
                Shutdown();
            }
        }

        private void ParentProcessExited(object source, EventArgs args)
        {
            _bootstrapLogger.Log("Detected parent process exit. Shutting down...");
            Shutdown();
        }

        private void Shutdown()
        {
            this.Dispose();
            Environment.Exit(0);
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
                _shutdownSignal.Cancel();
                _remoteContainerServiceDispatcher?.Dispose();
                _pipeServer?.Dispose();
                _remotedMetricCollector?.Dispose();
                _serverPostOffice?.Dispose();
                _pipe?.Dispose();
                _parentProcess?.Dispose();
                _shutdownSignal.Dispose();
            }
        }

        private void PrintUnhandledException(object source, UnhandledExceptionEventArgs args)
        {
            _bootstrapLogger.Log("A potentially fatal unhandled exception was raised in the AppDomain", LogLevel.Err);
            object exceptionObject = args.ExceptionObject;
            _bootstrapLogger.Log(exceptionObject.GetType().Name, LogLevel.Err);
            if (exceptionObject is Exception)
            {
                Exception ex = exceptionObject as Exception;
                _bootstrapLogger.Log(ex, LogLevel.Err);
            }

            _remotedMetricCollector?.ReportInstant("Unhandled AppDomain Exceptions / sec", _metricDimensions);
        }
    }
}
