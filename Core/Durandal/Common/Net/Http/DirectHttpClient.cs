using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using System.Threading;
using System.Diagnostics;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// An HTTP client which dispatches requests directly to an HttpServer object, bypassing the network entirely.
    /// Useful mostly for testing
    /// </summary>
    public class DirectHttpClient : IHttpClient
    {
        private readonly IHttpServerDelegate _targetServer;
        private int _disposed = 0;

        public DirectHttpClient(IHttpServerDelegate targetServer)
        {
            _targetServer = targetServer;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~DirectHttpClient()
        {
            Dispose(false);
        }
#endif

        public Uri ServerAddress
        {
            get
            {
                return new Uri("http://localhost:0");
            }
        }

        public HttpVersion MaxSupportedProtocolVersion => HttpVersion.HTTP_1_1;

        public HttpVersion InitialProtocolVersion
        {
            get
            {
                return HttpVersion.HTTP_1_1;
            }
            set
            {
            }
        }

        public Task<HttpResponse> SendRequestAsync(HttpRequest request)
        {
            return SendRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, null);
        }

        public Task<NetworkResponseInstrumented<HttpResponse>> SendInstrumentedRequestAsync(HttpRequest request)
        {
            return SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, null);
        }

        public async Task<HttpResponse> SendRequestAsync(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime = null, ILogger queryLogger = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            request.MakeProxied();
            request.RemoteHost = "direct.http";
            DirectHttpRequestContext requestContext = new DirectHttpRequestContext(request);
            await _targetServer.HandleConnection(requestContext, cancelToken, realTime).ConfigureAwait(false);
            requestContext.ClientResponse.MakeProxied();
            return requestContext.ClientResponse;
        }

        public async Task<NetworkResponseInstrumented<HttpResponse>> SendInstrumentedRequestAsync(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime = null, ILogger queryLogger = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            Stopwatch stopwatch = Stopwatch.StartNew();
            request.MakeProxied();
            request.RemoteHost = "direct.http";
            DirectHttpRequestContext requestContext = new DirectHttpRequestContext(request);
            await _targetServer.HandleConnection(requestContext, cancelToken, realTime).ConfigureAwait(false);
            stopwatch.Stop();
            requestContext.ClientResponse.MakeProxied();
            return new NetworkResponseInstrumented<HttpResponse>(requestContext.ClientResponse, 0, 0, 0, stopwatch.ElapsedMillisecondsPrecise(), 0);
        }

        public void SetReadTimeout(TimeSpan timeout) { }

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
            }
        }
    }
}
