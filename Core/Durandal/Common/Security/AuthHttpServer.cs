using Durandal.API;
using Durandal.Common.Client;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Durandal.Common.Security.Login;
using Durandal.Common.Security.OAuth;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Security
{
    public class AuthHttpServer : IHttpServerDelegate
    {
        private readonly Regex OAUTH_LOGIN_PROVIDER_PATH_MATCHER = new Regex("^\\/auth\\/login\\/oauth\\/([^\\/]+)$", RegexOptions.IgnoreCase);
        private readonly Regex LOGIN_PROVIDER_PATH_MATCHER = new Regex("^\\/auth\\/login\\/credentials\\/([^\\/]+)\\/([^\\/]+)$", RegexOptions.IgnoreCase);

        private readonly OAuthManager _oauthManager;
        private readonly ILogger _logger;
        private readonly IDictionary<string, ILoginProvider> _loginProviders;
        private readonly IUserProfileStorage _userProfileStorage;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;

        public AuthHttpServer(
            ILogger logger,
            IOAuthSecretStore oauthSecretStore,
            Uri thirdPartyOauthRedirectUrl,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            IEnumerable<ILoginProvider> loginProviders = null,
            IUserProfileStorage userProfileStorage = null)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _userProfileStorage = userProfileStorage;
            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            _loginProviders = new Dictionary<string, ILoginProvider>();

            if (loginProviders != null)
            {
                foreach (var loginProvider in loginProviders)
                {
                    _loginProviders[loginProvider.ProviderName] = loginProvider;
                }
            }

            if (thirdPartyOauthRedirectUrl != null && oauthSecretStore != null)
            {
                _oauthManager = new OAuthManager(thirdPartyOauthRedirectUrl.AbsoluteUri, oauthSecretStore, _metrics, _metricDimensions);
            }
            else
            {
                _logger.Log("Invalid oauth parameters given to auth server constructor. Third-party oauth will not be enabled.", LogLevel.Wrn);
            }
        }

        public async Task HandleConnection(IHttpServerContext serverContext, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpRequest clientRequest = serverContext.HttpRequest;
            HttpResponse finalResponse = null;
            ILogger queryLogger = _logger.CreateTraceLogger(Guid.NewGuid());
            try
            {
#region /auth/oauth/thirdparty
                // THIRDPARTY OAUTH CALLBACK PATH
                // Scenario: A skill has initiated a login flow with some third-party service (such as facebook, MS graph, fitbit, etc.)
                // After user logs in, the service sends the auth code to this endpoint as part of the auth code flow.
                // This handler takes the auth code and sends it to the oauth manager where it will be resolved into a token and stored in the db.
                if (string.Equals(clientRequest.DecodedRequestFile, "/auth/oauth/thirdparty", StringComparison.OrdinalIgnoreCase))
                {
                    if (_oauthManager == null)
                    {
                        string message = "OAuth manager is not properly configured";
                        queryLogger.Log(message, LogLevel.Err);
                        finalResponse = HttpResponse.ServerErrorResponse(message);
                    }

                    Guid traceId = Guid.NewGuid();
                    queryLogger = _logger.CreateTraceLogger(traceId);
                    await _oauthManager.HandleThirdPartyAuthCodeCallback(clientRequest, queryLogger, cancelToken, realTime).ConfigureAwait(false);
                    finalResponse = OAuthCloseWindowResponse();
                }
#endregion

#region /auth/login/oauth/{providername}
                // OAUTH LOGIN CALLBACK PATH
                // Scenario: The client's UI has initiated a user login flow which is backed by OAuth (for example, openID connect, MSA, facebook account, etc.)
                // That service then posts a callback to this endpoint as part of the auth code flow.
                // This handler takes the auth code and resolves it into a token, storing the resulting Durandal user profile into the private keys table
                Match pathMatch = OAUTH_LOGIN_PROVIDER_PATH_MATCHER.Match(clientRequest.DecodedRequestFile);
                if (finalResponse == null && pathMatch.Success)
                {
                    string providerName = pathMatch.Groups[1].Value.ToLowerInvariant();
                    ILoginProvider loginProvider;
                    if (!_loginProviders.TryGetValue(providerName, out loginProvider))
                    {
                        string message = "Unknown login provider \"" + providerName + "\"";
                        queryLogger.Log(message, LogLevel.Err);
                        finalResponse = HttpResponse.BadRequestResponse(message);
                    }

                    Guid traceId = Guid.NewGuid();
                    queryLogger = _logger.CreateTraceLogger(traceId);
                    await loginProvider.HandleOAuthCallback(clientRequest, queryLogger, cancelToken, realTime).ConfigureAwait(false);
                    finalResponse = OAuthCloseWindowResponse();
                }
#endregion

#region /auth/login/credentials/{scope}/{providername}
                // CLIENT LOGIN LONG POLL PATH 
                // Scenario: The client's UI has initiated a user login flow. The client also has a secret token value which it can use to prove that a
                // user has just interactively logged in. This secret token is delegated to the proper login provider, which will verify the token and then
                // send the client the private information about the user which has logged in, including their Durandal user id and private key.
                pathMatch = LOGIN_PROVIDER_PATH_MATCHER.Match(clientRequest.DecodedRequestFile);
                if (finalResponse == null && pathMatch.Success)
                {
                    string scopeString = pathMatch.Groups[1].Value.ToLowerInvariant();
                    string providerName = pathMatch.Groups[2].Value.ToLowerInvariant();
                    ILoginProvider loginProvider;
                    if (!_loginProviders.TryGetValue(providerName, out loginProvider))
                    {
                        string message = "Unknown login provider \"" + providerName + "\"";
                        queryLogger.Log(message, LogLevel.Err);
                        finalResponse = HttpResponse.BadRequestResponse(message);
                    }

                    string authHeader;
                    if (!clientRequest.RequestHeaders.TryGetValue("Authorization", out authHeader))
                    {
                        string message = "No authorization header in request";
                        queryLogger.Log(message, LogLevel.Err);
                        finalResponse = HttpResponse.BadRequestResponse(message);
                    }

                    if (!authHeader.Contains(" "))
                    {
                        string message = "Invalid authorization header: " + authHeader;
                        queryLogger.Log(message, LogLevel.Err);
                        finalResponse = HttpResponse.BadRequestResponse(message);
                    }

                    string token = authHeader.Substring(authHeader.IndexOf(' ') + 1);

                    ClientAuthenticationScope scope = ClientAuthenticationScope.None;
                    if (string.Equals(scopeString, "user"))
                    {
                        scope = ClientAuthenticationScope.User;
                    }
                    else if (string.Equals(scopeString, "client"))
                    {
                        scope = ClientAuthenticationScope.Client;
                    }
                    else if (string.Equals(scopeString, "userclient"))
                    {
                        scope = ClientAuthenticationScope.UserClient;
                    }
                    else
                    {
                        string message = "Unknown auth scope \"" + scopeString + "\"";
                        queryLogger.Log(message, LogLevel.Err);
                        finalResponse = HttpResponse.BadRequestResponse(message);
                    }

                    if (scope != ClientAuthenticationScope.None)
                    {
                        UserClientSecretInfo secretInfo = await loginProvider.VerifyExternalToken(scope, queryLogger, token, cancelToken, realTime).ConfigureAwait(false);

                        if (secretInfo == null)
                        {
                            // No secret information is available yet, so send 202 to tell the client to wait a while longer.
                            finalResponse = HttpResponse.AcceptedResponse();
                        }
                        else
                        {
                            // If a user scope is involved here, try and update (or create) the user's global profile with the information provided by the authentication backend.
                            // This will make things like user name and user email available to dialog engine globally
                            if (scope.HasFlag(ClientAuthenticationScope.User))
                            {
                                await UpdateUserProfileWithLoggedInUserInfo(secretInfo, queryLogger).ConfigureAwait(false);
                            }

                            finalResponse = HttpResponse.OKResponse();
                            finalResponse.SetContentJson(secretInfo);
                        }
                    }
                }
#endregion

                if (finalResponse == null)
                {
                    finalResponse = HttpResponse.NotFoundResponse();
                }

                try
                {
                    await serverContext.WritePrimaryResponse(finalResponse, queryLogger, cancelToken, realTime).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    queryLogger.Log(e, LogLevel.Err);
                }
            }
            catch (Exception e)
            {
                queryLogger.Log(e, LogLevel.Err);
                try
                {
                    await serverContext.WritePrimaryResponse(HttpResponse.ServerErrorResponse(e), queryLogger, cancelToken, realTime).ConfigureAwait(false);
                }
                catch (Exception e2)
                {
                    queryLogger.Log(e2, LogLevel.Err);
                }
            }
        }

        private async Task UpdateUserProfileWithLoggedInUserInfo(UserClientSecretInfo secretInfo, ILogger queryLogger)
        {
            if (_userProfileStorage != null)
            {
                queryLogger.Log("Updating global user profile with user metadata...");

                if (string.IsNullOrEmpty(secretInfo.UserId))
                {
                    queryLogger.Log("Can't update global user profile as there is no user ID!", LogLevel.Err);
                    return;
                }

                InMemoryDataStore globalProfile = null;
                RetrieveResult<UserProfileCollection> profileRetrieveResult = await _userProfileStorage.GetProfiles(UserProfileType.PluginGlobal, secretInfo.UserId, null, queryLogger).ConfigureAwait(false);
                if (profileRetrieveResult.Result != null)
                {
                    globalProfile = profileRetrieveResult.Result.GlobalProfile;
                }

                if (globalProfile == null)
                {
                    globalProfile = new InMemoryDataStore();
                }

                if (!globalProfile.ContainsKey(ClientContextField.UserFullName) &&
                    !string.IsNullOrEmpty(secretInfo.UserFullName))
                {
                    globalProfile.Put(ClientContextField.UserFullName, secretInfo.UserFullName);
                }
                if (!globalProfile.ContainsKey(ClientContextField.UserGivenName) &&
                    !string.IsNullOrEmpty(secretInfo.UserGivenName))
                {
                    globalProfile.Put(ClientContextField.UserGivenName, secretInfo.UserGivenName);
                }
                if (!globalProfile.ContainsKey(ClientContextField.UserSurname) &&
                    !string.IsNullOrEmpty(secretInfo.UserSurname))
                {
                    globalProfile.Put(ClientContextField.UserSurname, secretInfo.UserSurname);
                }
                if (!globalProfile.ContainsKey(ClientContextField.UserEmail) &&
                    !string.IsNullOrEmpty(secretInfo.UserEmail))
                {
                    globalProfile.Put(ClientContextField.UserEmail, secretInfo.UserEmail);
                }

                UserProfileCollection updatedProfiles = new UserProfileCollection(null, globalProfile, null);
                await _userProfileStorage.UpdateProfiles(UserProfileType.PluginGlobal, updatedProfiles, secretInfo.UserId, null, queryLogger).ConfigureAwait(false);
            }
        }

        private static HttpResponse OAuthCloseWindowResponse()
        {
            string html = "<html><body onload=\"window.close()\">Login successful. You may now close this window.</body></html>";
            HttpResponse returnVal = HttpResponse.OKResponse();
            returnVal.SetContent(html, "text/html");
            return returnVal;
        }
    }
}
