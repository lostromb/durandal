using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Net.Http;
using Durandal.Common.Utils;
using Durandal.Common.Time;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Remoting.Proxies
{
    public class RemotedHttpClient : IHttpClient
    {
        private readonly WeakPointer<RemoteDialogMethodDispatcher> _dispatcher;
        private readonly IRealTimeProvider _realTime;
        private readonly Uri _baseAddress;
        private readonly ILogger _fallbackLogger;
        private int _disposed = 0;

        public RemotedHttpClient(Uri baseUri, ILogger fallbackLogger, RemoteDialogMethodDispatcher dispatcher, IRealTimeProvider realTime)
        {
            _baseAddress = baseUri;
            _dispatcher = new WeakPointer<RemoteDialogMethodDispatcher>(dispatcher);
            _realTime = realTime;
            _fallbackLogger = fallbackLogger;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~RemotedHttpClient()
        {
            Dispose(false);
        }
#endif

        public Uri ServerAddress => _baseAddress;

        public HttpVersion MaxSupportedProtocolVersion => HttpVersion.HTTP_1_1;

        public HttpVersion InitialProtocolVersion
        {
            get
            {
                return HttpVersion.HTTP_1_1;
            }
            set { }
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
            }
        }

        public Task<NetworkResponseInstrumented<HttpResponse>> SendInstrumentedRequestAsync(HttpRequest request)
        {
            return SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _fallbackLogger);
        }

        public Task<NetworkResponseInstrumented<HttpResponse>> SendInstrumentedRequestAsync(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime = null, ILogger queryLogger = null)
        {
            return _dispatcher.Value.Http_Request(request, _baseAddress, queryLogger ?? _fallbackLogger, realTime, cancelToken);
        }

        public async Task<HttpResponse> SendRequestAsync(HttpRequest request)
        {
            NetworkResponseInstrumented<HttpResponse> resp = await SendInstrumentedRequestAsync(request, CancellationToken.None, DefaultRealTimeProvider.Singleton, _fallbackLogger).ConfigureAwait(false);
            return resp.Response;
        }

        public async Task<HttpResponse> SendRequestAsync(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime = null, ILogger queryLogger = null)
        {
            NetworkResponseInstrumented<HttpResponse> resp = await SendInstrumentedRequestAsync(request, cancelToken, realTime, queryLogger ?? _fallbackLogger).ConfigureAwait(false);
            return resp.Response;
        }

        public void SetReadTimeout(TimeSpan timeout)
        {
        }
    }
}
