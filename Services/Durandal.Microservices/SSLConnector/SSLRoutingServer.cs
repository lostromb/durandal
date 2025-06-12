using Durandal.Common.Dialog;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Security;
using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using DurandalServices.ChannelProxy.Connectors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Instrumentation;
using Durandal.Common.Net.Http2;
using Durandal.Common.ServiceMgmt;

namespace DurandalServices.SSLConnector
{
    public class SSLRoutingServer : IHttpServerDelegate, IServer
    {
        private IHttpClient _defaultRoute;
        private IHttpClient _connectorRoute;
        private ISocketServer _socketServer;
        private IHttpServer _baseServer;
        private ISocketFactory _socketFactory;
        private ILogger _logger;

        private AuthHttpServer _authServer;

        private int _disposed = 0;

        public SSLRoutingServer(AuthHttpServer authServer, ILogger logger, WeakPointer<IThreadPool> threadPool)
        {
            // fixme hardcoded everything
            _authServer = authServer.AssertNonNull(nameof(authServer));
            _socketFactory = new PooledTcpClientSocketFactory(logger, NullMetricCollector.Singleton, DimensionSet.Empty);
            _defaultRoute = new SocketHttpClient(
                new WeakPointer<ISocketFactory>(_socketFactory),
                new TcpConnectionConfiguration(
                    "localhost",
                    62297,
                    useSsl: false),
                logger,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                Http2SessionManager.Default,
                new Http2SessionPreferences());
            _connectorRoute = new SocketHttpClient(
                new WeakPointer<ISocketFactory>(_socketFactory),
                new TcpConnectionConfiguration(
                    "localhost",
                    62294,
                    useSsl: false),
                logger,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                Http2SessionManager.Default,
                new Http2SessionPreferences());
            _socketServer = new RawTcpSocketServer(
                new ServerBindingInfo[]
                {
                    new ServerBindingInfo(ServerBindingInfo.WILDCARD_HOSTNAME, 443, CertificateIdentifier.BySubjectName("durandal-ai.net"))
                },
                logger,
                DefaultRealTimeProvider.Singleton,
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty,
                threadPool);
            _baseServer = new SocketHttpServer(
                _socketServer,
                logger,
                new CryptographicRandom(),
                NullMetricCollector.WeakSingleton,
                DimensionSet.Empty);

            _baseServer.RegisterSubclass(this);
            _logger = logger;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~SSLRoutingServer()
        {
            Dispose(false);
        }
#endif

        public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpRequest request = serverContext.HttpRequest;
            HttpResponse response;

            try
            {
                // Look at the path and route accordingly
                if (request.RequestFile.StartsWith("/auth/"))
                {
                    _logger.Log("Got auth request with path " + request.RequestFile);
                    await _authServer.HandleConnection(serverContext, cancelToken, realTime);
                    request = null;
                    return;
                }
                else if (request.RequestFile.StartsWith("/connectors/"))
                {
                    _logger.Log("Got connector request with path " + request.RequestFile);
                    request.MakeProxied();
                    response = await _connectorRoute.SendRequestAsync(request, cancelToken, realTime, _logger).ConfigureAwait(false);
                }
                else
                {
                    _logger.Log("Got default request with path " + request.RequestFile);
                    request.MakeProxied();
                    response = await _defaultRoute.SendRequestAsync(request, cancelToken, realTime, _logger).ConfigureAwait(false);
                }

                if (response != null)
                {
                    response.MakeProxied();

                    try
                    {
                        await serverContext.WritePrimaryResponse(response, _logger, cancelToken, realTime).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _logger.Log(e, LogLevel.Err);
                    }
                }
            }
            finally
            {
                // need to dispose request only if we made it proxied earlier
                request?.Dispose();
            }
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
                _baseServer?.Dispose();
                _socketServer?.Dispose();
                _connectorRoute?.Dispose();
                _defaultRoute?.Dispose();
                _socketFactory?.Dispose();
            }
        }
    }
}
