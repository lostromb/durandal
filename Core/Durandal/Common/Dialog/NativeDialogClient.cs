using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using Durandal.Common.Security;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Utils;
using Durandal.Common.Net.Http;
using Durandal.Common.Security.Client;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Dialog
{
    /// <summary>
    /// Dialog client which communicates directly with a DialogWebService instance running locally in the current process.
    /// It includes some HTTP methods out of necessity because lots of dialog web functionality (actions, SPA data, static views)
    /// are implemented over HTTP only.
    /// </summary>
    public class NativeDialogClient : IDialogClient
    {
        private readonly DialogWebService _core;
        private readonly IHttpClient _proxyHttpClient;
        private int _disposed = 0;

        public NativeDialogClient(DialogWebService core, IHttpServerDelegate dialogHttpServer)
        {
            _core = core;
            _proxyHttpClient = new DirectHttpClient(dialogHttpServer);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~NativeDialogClient()
        {
            Dispose(false);
        }
#endif

        public Uri GetConnectionString()
        {
            return new Uri("http://native");
        }

        public Task<IDictionary<string, string>> GetStatus(
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            return Task.FromResult(_core.GetStatus());
        }

        public async Task<NetworkResponseInstrumented<DialogResponse>> MakeDialogActionRequest(
            DialogRequest request,
            string url,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            DialogWebServiceResponse response = await _core.ProcessDialogAction(request, url, realTime).ConfigureAwait(false);
            return await Task.FromResult(new NetworkResponseInstrumented<DialogResponse>(response.ClientResponse)).ConfigureAwait(false);
        }

        public async Task<NetworkResponseInstrumented<DialogResponse>> MakeQueryRequest(
            DialogRequest request,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            DialogWebServiceResponse response = await _core.ProcessRegularQuery(request, cancelToken, realTime).ConfigureAwait(false);
            // Fixme: This does not utilize audio streams!
            return new NetworkResponseInstrumented<DialogResponse>(response.ClientResponse);
        }

        public Task<ResetConversationStateResult> ResetConversationState(
            string userId,
            string clientId,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            _core.ResetClientState(userId, clientId, queryLogger);
            return Task.FromResult(new ResetConversationStateResult()
                {
                    Success = true,
                    ErrorMessage = string.Empty
                });
        }

        // This shouldn't need to use HTTP, but it does rely on HTTP cache control for performance so it's hard to decouple right now...
        public async Task<HttpResponse> MakeStaticResourceRequest(
            HttpRequest request,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            NetworkResponseInstrumented<HttpResponse> response = await _proxyHttpClient.SendInstrumentedRequestAsync(request, cancelToken, realTime, queryLogger).ConfigureAwait(false);
            return response.Response;
        }
        
        public Task<IAudioDataSource> GetStreamingAudioResponse(
            string relativeAudioUrl,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null)
        {
            HttpRequest req = HttpRequest.CreateOutgoing(relativeAudioUrl, "GET");
            if (!req.GetParameters.ContainsKey("audio"))
            {
                throw new FormatException("Audio stream URI had invalid format: " + relativeAudioUrl);
            }

            string cacheKey = req.GetParameters["audio"];
            queryLogger = queryLogger ?? NullLogger.Singleton;
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            return _core.FetchStreamingAudio(cacheKey, queryLogger, cancelToken, realTime);
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
                _proxyHttpClient.Dispose();
            }
        }
    }
}
