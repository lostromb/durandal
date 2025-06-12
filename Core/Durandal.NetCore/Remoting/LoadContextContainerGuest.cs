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
using Durandal.Common.Utils;
using Durandal.Common.Remoting.Proxies;
using Durandal.Common.IO;
using System.Runtime.Loader;
using Durandal.API;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting
{
    public class LoadContextContainerGuest : IContainerGuest, IDisposable
    {
        private readonly CancellationTokenSource _shutdownSignal = new CancellationTokenSource();
        private ISocket _pipe;
        private IDurandalPluginLoader _pluginLoader;
        private RemoteDialogExecutorServer _pipeServer;
        private RemoteDialogMethodDispatcher _remoteContainerServiceDispatcher;
        private PostOffice _serverPostOffice;
        private RemotedLogger _remotedLogger;
        private IFileSystem _remotedFileSystem;
        private IHttpClientFactory _remotedHttpFactory;
        private IMetricCollector _remotedMetricCollector;
        private IList<IRemoteDialogProtocol> _dialogProtocols;
        private DimensionSet _metricDimensions;
        private ILogger _bootstrapLogger = NullLogger.Singleton;
        private int _disposed = 0;

        public LoadContextContainerGuest()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~LoadContextContainerGuest()
        {
            Dispose(false);
        }
#endif

        public void Initialize(string serializedJsonInitializationParameters)
        {
            ContainerGuestInitializationParameters initializationParameters = JsonConvert.DeserializeObject<ContainerGuestInitializationParameters>(serializedJsonInitializationParameters);
            string containerName = initializationParameters.ContainerName;

            _bootstrapLogger = new ImmutableLogger(new DebugLogger("ContainerDebug-" + containerName));
            _bootstrapLogger.Log("Bootstrapping load context container guest: " + containerName);
            _bootstrapLogger.Log("Base directory is: " + initializationParameters.DurandalBaseDirectory);
            _bootstrapLogger.Log("Container directory is: " + initializationParameters.ContainerDirectory);
            _bootstrapLogger.Log("Pipe URI: " + initializationParameters.SocketConnectionString);
            _bootstrapLogger.Log("Post office box: " + initializationParameters.ContainerLevelMessageBoxId);
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

            // use the version number of the actually loaded durandal.dll, in case it's different from the host
            _metricDimensions = _metricDimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_ServiceVersion, SVNVersionInfo.AssemblyVersion));

            _bootstrapLogger.Log("Initializing remoted services");
            string pipeUri = initializationParameters.SocketConnectionString;
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
            _serverPostOffice = new PostOffice(_pipe, _bootstrapLogger, postOfficeTimeout, true, realTime, new WeakPointer<IMetricCollector>(deferredMetricCollector), _metricDimensions);
            _remoteContainerServiceDispatcher = new RemoteDialogMethodDispatcher(_serverPostOffice, diagnosticMailbox, _bootstrapLogger, containerLevelProtocol);
            _remotedLogger = new RemotedLogger(new WeakPointer<RemoteDialogMethodDispatcher>(_remoteContainerServiceDispatcher), realTime, _bootstrapLogger, "Container-" + containerName);
            _remotedFileSystem = new RemotedFileSystem(new WeakPointer<RemoteDialogMethodDispatcher>(_remoteContainerServiceDispatcher), _remotedLogger, realTime);
            _remotedHttpFactory = new RemotedHttpClientFactory(_remoteContainerServiceDispatcher, realTime, _remotedLogger);
            _remotedMetricCollector = new RemotedMetricCollector(_remotedLogger, _remoteContainerServiceDispatcher, realTime);
            deferredMetricCollector.SetCollectorImplementation(_remotedMetricCollector);

            _dialogProtocols = new List<IRemoteDialogProtocol>()
            {
                new JsonRemoteDialogProtocol(),
                new BondRemoteDialogProtocol()
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

            _pluginLoader = new AssemblyLoadContextPluginLoader(
                AssemblyLoadContext.CurrentContextualReflectionContext,
                new BasicDialogExecutor(false),
                aggregateLogger.Clone("DllLoader-" + containerName),
                containerFileSystem,
                VirtualPath.Root,
                durandalEnvFileSystem);

            _pipeServer = new RemoteDialogExecutorServer(
                aggregateLogger,
                new WeakPointer<PostOffice>(_serverPostOffice),
                _pluginLoader,
                _dialogProtocols,
                new WeakPointer<IThreadPool>(localThreadPool),
                _remotedFileSystem,
                _remotedHttpFactory,
                new RoslynLGScriptCompiler(),
               nlToolsForLg,
               new WeakPointer<IMetricCollector>(_remotedMetricCollector),
               _metricDimensions);

            _pipeServer.Start(realTime);

            AppDomain.CurrentDomain.UnhandledException += PrintUnhandledException;

            // Tell the host that we are alive
            _serverPostOffice.SendMessage(new MailboxMessage(diagnosticMailbox, 0, BufferPool<byte>.Rent(0)), CancellationToken.None, realTime).Await();

            aggregateLogger.Log("Load context container guest " + containerName + " initialized");
        }

        public void Shutdown()
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!Durandal.Common.Utils.AtomicOperations.ExecuteOnce(ref _disposed))
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
