using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Security.Client;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Security.Login
{
    public interface ILoginProvider : IDisposable
    {
        string ProviderName { get; }

        string UserIdScheme { get; }
        ClientAuthenticationScope SupportedScopes { get; }
        bool AllowMultiTenancy { get; }

        /// <summary>
        /// Called on the client side. Requests a private key for the given client identifier
        /// </summary>
        /// <param name="keyScope">The scope that we are authenticating</param>
        /// <param name="logger">A logger for the operation</param>
        /// <param name="externalToken">A token returned by the login provider</param>
        /// <param name="cancelToken">Cancellation token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns></returns>
        Task<RetrieveResult<UserClientSecretInfo>> GetSecretUserInfo(ClientAuthenticationScope keyScope, ILogger logger, string externalToken, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Called on the server side. Verifies that the given token belongs to the bearer identified by the client identifier
        /// </summary>
        /// <param name="keyScope">The scope that this token was issued for</param>
        /// <param name="logger">A logger</param>
        /// <param name="externalToken">The token received from the client</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns></returns>
        Task<UserClientSecretInfo> VerifyExternalToken(ClientAuthenticationScope keyScope, ILogger logger, string externalToken, CancellationToken cancelToken, IRealTimeProvider realTime);

        Task HandleOAuthCallback(HttpRequest callbackRequest, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}
