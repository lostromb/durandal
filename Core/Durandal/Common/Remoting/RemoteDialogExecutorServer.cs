using Durandal.API;
using Durandal.Common.Config;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Runtime;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Dialog.Web;
using Durandal.Common.File;
using Durandal.Common.LG;
using Durandal.Common.LG.Statistical;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.NLP;
using Durandal.Common.Remoting.Protocol;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Speech.SR;
using Durandal.Common.Speech.TTS;
using Durandal.Common.Test;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.Instrumentation;
using Durandal.Common.Events;
using System.Diagnostics;
using Durandal.Common.NLP.Language;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting
{
    public class RemoteDialogExecutorServer : IDisposable
    {
        private readonly ILogger _logger;
        private readonly WeakPointer<PostOffice> _postOffice;
        private readonly WeakPointer<IThreadPool> _executorThreadPool;
        private readonly IDurandalPluginLoader _pluginLoader;
        private readonly IDictionary<uint, IRemoteDialogProtocol> _protocols;
        private readonly CancellationTokenSource _cancelToken;
        private readonly IFileSystem _localFileSystem;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILGScriptCompiler _lgScriptCompiler;
        private readonly INLPToolsCollection _pluginNlpTools;
        private readonly FastConcurrentDictionary<PluginStrongName, CachedRemotePluginServicesConstants> _cachedPluginServices;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private Task _serverTask;
        private int _disposed = 0;

        public RemoteDialogExecutorServer(
            ILogger logger,
            WeakPointer<PostOffice> postOffice,
            IDurandalPluginLoader pluginLoader,
            IEnumerable<IRemoteDialogProtocol> dialogProtocols,
            WeakPointer<IThreadPool> executorThreadPool,
            IFileSystem localFileSystem,
            IHttpClientFactory httpClientFactory,
            ILGScriptCompiler lgScriptCompiler,
            INLPToolsCollection pluginNlpTools,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions)
        {
            _logger = logger;
            _postOffice = postOffice;
            _pluginLoader = pluginLoader;
            _protocols = new Dictionary<uint, IRemoteDialogProtocol>();
            foreach (IRemoteDialogProtocol protocol in dialogProtocols)
            {
                _protocols[protocol.ProtocolId] = protocol;
            }

            _executorThreadPool = executorThreadPool.AssertNonNull(nameof(executorThreadPool));
            _localFileSystem = localFileSystem.AssertNonNull(nameof(localFileSystem));
            _httpClientFactory = httpClientFactory.AssertNonNull(nameof(httpClientFactory));
            _lgScriptCompiler = lgScriptCompiler.AssertNonNull(nameof(lgScriptCompiler));
            _pluginNlpTools = pluginNlpTools.AssertNonNull(nameof(pluginNlpTools));
            _cancelToken = new CancellationTokenSource();
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            _cachedPluginServices = new FastConcurrentDictionary<PluginStrongName, CachedRemotePluginServicesConstants>();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RemoteDialogExecutorServer()
        {
            Dispose(false);
        }
#endif

        public void Start(IRealTimeProvider realTime)
        {
            IRealTimeProvider listenThreadTime = realTime.Fork("RemoteDialogExecutorServer");
            _serverTask = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(
                async () =>
                {
                    try
                    {
                        await RunListenerThread(listenThreadTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        listenThreadTime.Merge();
                    }
                });
        }

        public void Stop()
        {
            _cancelToken.Cancel();
        }
        
        private async Task RunListenerThread(IRealTimeProvider realTime)
        {
            try
            {
                CancellationToken cancelToken = _cancelToken.Token;
                while (!cancelToken.IsCancellationRequested)
                {
                    MailboxId incomingMailbox = await _postOffice.Value.WaitForMessagesOnNewMailbox(cancelToken, realTime).ConfigureAwait(false);
                    RemoteDialogServerRequestHandler handler = new RemoteDialogServerRequestHandler(
                        _logger,
                        _postOffice,
                        _pluginLoader,
                        _protocols,
                        _cachedPluginServices,
                        incomingMailbox,
                        _executorThreadPool.Value,
                        _localFileSystem,
                        _httpClientFactory,
                        _lgScriptCompiler,
                        _pluginNlpTools,
                        _metrics,
                        _metricDimensions);

                    Stopwatch timer = Stopwatch.StartNew();
                    IRealTimeProvider threadLocalTime = realTime.Fork("RemoteDialogProcessIncomingRequest");
                    _executorThreadPool.Value.EnqueueUserAsyncWorkItem(
                        async () =>
                        {
                            try
                            {
                                timer.Stop();
                                _metrics.Value.ReportPercentile(CommonInstrumentation.Key_Counter_RemoteDialogExecutor_StartThread, _metricDimensions, timer.ElapsedMillisecondsPrecise());
                                await handler.ProcessIncomingRequest(cancelToken, threadLocalTime).ConfigureAwait(false);

                                // we need to flush the remoted logger every once in a while so might as well do it here
                                await _logger.Flush(cancelToken, threadLocalTime, false);
                            }
#if DEBUG
                            catch (PlatformNotSupportedException)
                            {
                                // This is the special unit test case for intentionally crashing the container by killing the executor thread
                                _logger.Log("Remote dialog executor server is \"crashing\" as part of a unit test", LogLevel.Wrn);
                                Stop();
                            }
#endif
                            finally
                            {
                                threadLocalTime.Merge();
                            }
                        });
                }
            }
            catch (OperationCanceledException)
            {
                // Server is stopping normally
            }
            catch (Exception e)
            {
                // Some other issue happened. Raise an error event in case the container needs to reset or something
                _logger.Log(e, LogLevel.Err);
            }
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
                Stop();

                // we don't own the handle to any other members (thread pool, sockets, etc.) so we don't dispose them here
                _cancelToken.Dispose();
            }
        }
    }
}
