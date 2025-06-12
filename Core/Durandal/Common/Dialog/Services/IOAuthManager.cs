using System;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.API;
using Durandal.Common.Time;
using System.Threading;

namespace Durandal.Common.Dialog.Services
{
    public interface IOAuthManager
    {
        /// <summary>
        /// Attempts to retrieve an auth token for the given user and plugin. 
        /// </summary>
        /// <param name="durandalUserId">The ID of the user to try and fetch a token for</param>
        /// <param name="owningPlugin">The plugin ID that owns the token</param>
        /// <param name="config">The configuration that specifies which token we want (to allow plugins to manage multiple oauth services)</param>
        /// <param name="queryLogger">A tracing logger</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The auth token object, or null if the auth token was never issued, is expired, or is otherwise invalid</returns>
        Task<OAuthToken> GetToken(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Step 1 of the Authorize flow. Accepts an OAuth config, and returns a URI that should be sent to a client
        /// to start the authorization process for that config (the returned URL will link to the 3rd-party auth supplier's page)
        /// </summary>
        /// <param name="durandalUserId">The ID of the user for whom this token will be issued</param>
        /// <param name="owningPlugin">The strong ID of the plugin that is owning this token</param>
        /// <param name="config">The OAuth configuration (client secret, endpoint string, etc.) to generate a URL for</param>
        /// <param name="queryLogger">A tracing logger</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An auth URI to be invoked on the client that will start the 3rd-party login process</returns>
        Task<Uri> CreateAuthUri(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Deletes a user's existing OAuth token that is tied with the given configuration
        /// </summary>
        /// <param name="durandalUserId">The ID of the user for whom we want to delete the token</param>
        /// <param name="owningPlugin">The plugin ID of the owner of the plugin</param>
        /// <param name="config">The configuration that specifies what service we want to delete the token for</param>
        /// <param name="queryLogger">A tracing logger</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        Task DeleteToken(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}