using Durandal.API;
using Durandal.Common.Security;
using Durandal.Common.Logger;
using Durandal.Common.Net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Durandal.Common.Dialog.Web;
using Durandal.Common.Net.Http;
using Durandal.Common.Security.Client;
using Durandal.Common.Time;
using System.Threading;
using Durandal.Common.IO;
using Durandal.Common.Audio;

namespace Durandal.Common.Dialog
{
    public interface IDialogClient : IDisposable
    {
        /// <summary>
        /// Returns an untyped bag of key-value pairs which indicate the current state of the dialog service
        /// </summary>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">Cancel token for the request</param>
        /// <param name="realTime">A definition of real time, used for unit tests</param>
        /// <returns></returns>
        Task<IDictionary<string, string>> GetStatus(
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null);

        /// <summary>
        /// Returns a connection string which can be used to access the dialog service via http.
        /// e.g. "https://dialogserver.com:62292"
        /// </summary>
        /// <returns></returns>
        Uri GetConnectionString();

        /// <summary>
        /// Invokes a direct dialog action indicated by a key given by the remote service. The response is a regular client response as with any other query.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="actionId"></param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">Cancel token for the request</param>
        /// <param name="realTime">A definition of real time, used for unit tests</param>
        /// <returns></returns>
        Task<NetworkResponseInstrumented<DialogResponse>> MakeDialogActionRequest(
            DialogRequest request,
            string actionId,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null);

        /// <summary>
        /// Sends a dialog query (audio or text) and returns the response.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">Cancel token for the request</param>
        /// <param name="realTime">A definition of real time, used for unit tests</param>
        /// <returns></returns>
        Task<NetworkResponseInstrumented<DialogResponse>> MakeQueryRequest(
            DialogRequest request,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null);

        /// <summary>
        /// Direct the remote service to reset the conversation stack for this user and client
        /// FIXME do we clear roaming state with this as well?
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="clientId"></param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">Cancel token for the request</param>
        /// <param name="realTime">A definition of real time, used for unit tests</param>
        /// <returns></returns>
        Task<ResetConversationStateResult> ResetConversationState(
            string userId,
            string clientId,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null);

        /// <summary>
        /// Called by the local presentation layer when the client wants to request a static or temporary resource.
        /// Since this is almost always a vanilla HTTP GET request, this function operates directly on http request/response objects
        /// </summary>
        /// <param name="request"></param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">Cancel token for the request</param>
        /// <param name="realTime">A definition of real time, used for unit tests</param>
        /// <returns></returns>
        Task<HttpResponse> MakeStaticResourceRequest(
            HttpRequest request,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null);

        Task<IAudioDataSource> GetStreamingAudioResponse(
            string relativeAudioUrl,
            ILogger queryLogger = null,
            CancellationToken cancelToken = default(CancellationToken),
            IRealTimeProvider realTime = null);
    }
}
