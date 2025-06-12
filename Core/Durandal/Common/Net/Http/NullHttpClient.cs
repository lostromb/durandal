using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Net.Http
{
    /// <summary>
    /// Http client which returns a "network timed out" response for all requests - as though the client has no network connection
    /// </summary>
    public class NullHttpClient : IHttpClient
    {
        private readonly CancellationTokenSource _cancelTokenSource;
        private TimeSpan _readTimeout;
        private int _disposed = 0;

        public NullHttpClient() : this(TimeSpan.FromSeconds(10))
        {
        }

        public NullHttpClient(TimeSpan simulatedTimeout)
        {
            _cancelTokenSource = new CancellationTokenSource();
            _readTimeout = simulatedTimeout;
            if (simulatedTimeout < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("Timeout cannot be negative");
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~NullHttpClient()
        {
            Dispose(false);
        }
#endif

        public Uri ServerAddress => new Uri("http://null");

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
                _cancelTokenSource.Cancel();
                _cancelTokenSource.Dispose();
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
            await realTime.WaitAsync(_readTimeout, _cancelTokenSource.Token).ConfigureAwait(false);
            return null;
        }

        public async Task<NetworkResponseInstrumented<HttpResponse>> SendInstrumentedRequestAsync(HttpRequest request, CancellationToken cancelToken, IRealTimeProvider realTime = null, ILogger queryLogger = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            await realTime.WaitAsync(_readTimeout, _cancelTokenSource.Token).ConfigureAwait(false);
            return new NetworkResponseInstrumented<HttpResponse>(null, 0, 0, 0, _readTimeout.TotalMilliseconds, 0);
        }

        public void SetReadTimeout(TimeSpan timeout)
        {
            _readTimeout = timeout;
        }
    }
}
