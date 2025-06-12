using Durandal.Common.Dialog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Security;
using Durandal.Common.Net.Http;
using Durandal.Common.Security.Client;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.Audio;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Test
{
    public class FakeDialogClient : IDialogClient
    {
        private DialogResponse _nextResponse = null;
        private int _disposed = 0;

        public FakeDialogClient()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FakeDialogClient()
        {
            Dispose(false);
        }
#endif

        public Uri GetConnectionString()
        {
            return new Uri("http://fake-dialog-service");
        }

        public void SetResponse(DialogResponse nextResponse)
        {
            _nextResponse = nextResponse;
        }

        public Task<IDictionary<string, string>> GetStatus(ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>());
        }
        
        public Task<NetworkResponseInstrumented<DialogResponse>> MakeDialogActionRequest(DialogRequest request, string url, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult(new NetworkResponseInstrumented<DialogResponse>(_nextResponse));
        }

        public Task<NetworkResponseInstrumented<DialogResponse>> MakeQueryRequest(DialogRequest request, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult(new NetworkResponseInstrumented<DialogResponse>(_nextResponse));
        }

        public Task<ResetConversationStateResult> ResetConversationState(string userId, string clientId, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult(new ResetConversationStateResult()
            {
                Success = true,
                ErrorMessage = string.Empty
            });
        }

        public Task<HttpResponse> MakeStaticResourceRequest(HttpRequest request, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult(HttpResponse.NotFoundResponse());
        }

        public Task<IAudioDataSource> GetStreamingAudioResponse(string relativeAudioUrl, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return Task.FromResult<IAudioDataSource>(null);
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
    }
}
