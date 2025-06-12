using Durandal.Extensions.BondProtocol;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using DurandalServices.ChannelProxy.Connectors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.Instrumentation;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Security;

namespace DurandalServices.ChannelProxy
{
    public class ProxyServer : IHttpServerDelegate, IServer
    {
        private IList<IConnector> _allConnectors;
        private IDialogClient _dialog;
        private ILogger _logger;
        private IHttpServer _baseServer;
        private IDialogTransportProtocol _dialogProtocol;
        private int _disposed = 0;

        public ProxyServer(ILogger logger, IList<IConnector> connectors, IEnumerable<ServerBindingInfo> listenerEndpoints, Uri dialogTarget, WeakPointer<IThreadPool> threadPool)
        {
            _allConnectors = connectors;
            _dialogProtocol = new DialogBondTransportProtocol();
            _dialog = new DialogHttpClient(
                new PortableHttpClient(
                    dialogTarget,
                    logger.Clone("Dialog HTTP transport"),
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty),
                logger.Clone("DialogClient"),
                _dialogProtocol);
            _logger = logger;
            _baseServer = new SocketHttpServer(
                new RawTcpSocketServer(
                    listenerEndpoints,
                    logger,
                    DefaultRealTimeProvider.Singleton,
                    NullMetricCollector.WeakSingleton,
                    DimensionSet.Empty,
                    threadPool),
                logger,
                new CryptographicRandom(),
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty);
            _baseServer.RegisterSubclass(this);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ProxyServer()
        {
            Dispose(false);
        }
#endif

        public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpResponse resp = await HandleConnectionInternal(serverContext.HttpRequest, cancelToken, realTime).ConfigureAwait(false);
            if (resp != null)
            {
                try
                {
                    await serverContext.WritePrimaryResponse(resp, _logger, cancelToken, realTime).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Err);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "This method returns an IDisposable so the caller should be responsible for disposal")]
        private async Task<HttpResponse> HandleConnectionInternal(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            foreach (IConnector connector in _allConnectors)
            {
                if (request.RequestFile.StartsWith(connector.Prefix))
                {
                    return await connector.HandleRequest(_dialog, request, cancelToken, realTime);
                }
            }

            _logger.Log("Got unsupported request using path " + request.RequestFile);

            return HttpResponse.ServerErrorResponse();
        }

        public IEnumerable<ServerBindingInfo> Endpoints
        {
            get
            {
                return _baseServer.Endpoints;
            }
        }

        public bool Running
        {
            get
            {
                return _baseServer.Running;
            }
        }

        public Task<bool> StartServer(string serverName, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _baseServer.StartServer(serverName, cancelToken, realTime);
        }

        public Task StopServer(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _baseServer.StopServer(cancelToken, realTime);
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
                _baseServer.Dispose();
            }
        }
    }
}
