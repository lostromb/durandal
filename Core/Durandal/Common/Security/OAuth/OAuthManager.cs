using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Net.Http;
using Newtonsoft.Json;
using Durandal.Common.Net;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.MathExt;
using Durandal.Common.Instrumentation;
using Durandal.Common.Dialog.Services;
using System.Threading;
using Durandal.Common.Remoting;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.IO.Hashing;

namespace Durandal.Common.Security.OAuth
{
    /// <summary>
    /// Manages OAuth2 requests and tokens inside the service. The high-level use of this class is as follows:
    /// - A plugin creates an OAuthConfig and passes it to CreateAuthUri. This will store an initial OAuth state in the secret store,
    /// and return a URL to be sent to the client to use for logging in
    /// - After user logs in to 3rd-party site, 3p service calls back to redirect_url.
    /// The HTTP handler for this endpoint should call HandleThirdPartyAuthCodeCallback()
    /// which extracts the auth code and ensures a token is properly retrieved
    /// - Plugin then calls GetToken() to try and retrieve a valid token which it can use for whatever authenticated calls it needs to perform
    /// </summary>
    public class OAuthManager : IOAuthManager
    {
        private readonly IRandom _srand;
        private readonly string _authorizeRedirectUrl;
        private readonly IOAuthSecretStore _secretStore;
        private readonly IHttpClientFactory _httpClientFactory;
        
        /// <summary>
        /// Constructs a new OAuth manager
        /// </summary>
        /// <param name="authorizeRedirectUrl">The redirect URL to pass to third-party services, telling them what callback to use for the authorize flow</param>
        /// <param name="secretStore">A secure store for caching OAuth states and tokens</param>
        /// <param name="metrics">A collector for metrics</param>
        /// <param name="metricDimensions">Dimensions to use for metrics</param>
        /// <param name="httpClientFactory">A factory which provides the HTTP clients that call into various services. Mainly used for unit testing. If null, PortableHttpClientFactory is used</param>
        /// <param name="secureRandom">The source of randomness to use for generating code verifiers</param>
        public OAuthManager(
            string authorizeRedirectUrl,
            IOAuthSecretStore secretStore,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions,
            IHttpClientFactory httpClientFactory = null,
            IRandom secureRandom = null)
        {
            _authorizeRedirectUrl = authorizeRedirectUrl;
            _secretStore = secretStore;
            _httpClientFactory = httpClientFactory ?? new PortableHttpClientFactory(metrics, metricDimensions);
            _srand = new DefaultRandom((secureRandom ?? new DefaultRandom()).NextInt()); // Use secure random to generate the seed for the regular random, so we don't drain too much entropy
        }

        /// <inheritdoc />
        public async Task<OAuthToken> GetToken(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (queryLogger == null)
            {
                queryLogger = NullLogger.Singleton;
            }

            RetrieveResult<OAuthState> tokenRetrieveResult = await _secretStore.RetrieveState(durandalUserId, owningPlugin, config, queryLogger.TraceId).ConfigureAwait(false);

            if (!tokenRetrieveResult.Success)
            {
                return null;
            }

            queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_OauthSecretRead, tokenRetrieveResult.LatencyMs), LogLevel.Ins);

            bool tokenIsValid = await UpdateToken(tokenRetrieveResult.Result, queryLogger, cancelToken, realTime, true).ConfigureAwait(false);

            if (!tokenIsValid)
            {
                // Delete the entire state here because we should only ever have one state with a given userid + domain + configname
                queryLogger.Log("The OAuth token was invalid and could not be refreshed. Deleting the current OAuth state...", LogLevel.Wrn);
                await _secretStore.DeleteState(tokenRetrieveResult.Result.UniqueId, queryLogger.TraceId).ConfigureAwait(false);
                return null;
            }

            return tokenRetrieveResult.Result.Token;
        }

        /// <inheritdoc />
        public async Task DeleteToken(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (queryLogger == null)
            {
                queryLogger = NullLogger.Singleton;
            }

            // Get the state for this config, if it already exists.
            // We need to do this because we need the unique ID of the oauth state to use as key for the deletion
            // TODO: That behavior could change. We could just delete all tokens with the given userID + domain instead.
            // But that might have unintended side effects to the developer.
            RetrieveResult<OAuthState> tokenRetrieveResult = await _secretStore.RetrieveState(
                durandalUserId, owningPlugin, config, queryLogger.TraceId).ConfigureAwait(false);

            if (tokenRetrieveResult.Success)
            {
                await _secretStore.DeleteState(tokenRetrieveResult.Result.UniqueId, queryLogger.TraceId).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<Uri> CreateAuthUri(string durandalUserId, PluginStrongName owningPlugin, OAuthConfig config, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (queryLogger == null)
            {
                queryLogger = NullLogger.Singleton;
            }

            // Validate the config
            if (config == null)
            {
                throw new ArgumentNullException("OAuth config");
            }
            if (string.IsNullOrEmpty(durandalUserId))
            {
                throw new ArgumentNullException("durandalUserId");
            }
            if (owningPlugin == null)
            {
                throw new ArgumentNullException("pluginDomain");
            }

            if (string.IsNullOrEmpty(config.ConfigName))
            {
                throw new ArgumentException("OAuth Config Name is required");
            }
            //if (string.IsNullOrEmpty(config.Domain))
            //{
            //    throw new ArgumentException("OAuth Domain is required");
            //}
            if (string.IsNullOrEmpty(config.ClientId))
            {
                throw new ArgumentException("OAuth ClientId is required");
            }
            if (string.IsNullOrEmpty(config.ClientSecret))
            {
                throw new ArgumentException("OAuth ClientSecret is required");
            }
            if (string.IsNullOrEmpty(config.TokenUri))
            {
                throw new ArgumentException("OAuth TokenUri is required");
            }
            if (string.IsNullOrEmpty(config.AuthUri))
            {
                throw new ArgumentException("OAuth AuthUri is required");
            }
            if (!Uri.IsWellFormedUriString(config.AuthUri, UriKind.Absolute))
            {
                throw new ArgumentException("OAuth AuthUri is not well-formed");
            }

            Uri authUri = new Uri(config.AuthUri);
            if (!string.Equals("https", authUri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The given OAuth authorize url must use HTTPS");
            }

            if (!Uri.IsWellFormedUriString(config.TokenUri, UriKind.Absolute))
            {
                throw new ArgumentException("OAuth TokenUri is not well-formed");
            }
            Uri tokenUri = new Uri(config.TokenUri);
            if (!string.Equals("https", tokenUri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The given OAuth token url must use HTTPS");
            }

            if (string.IsNullOrEmpty(_authorizeRedirectUrl))
            {
                queryLogger.Log("The service has not been properly configured with an oauth redirect URL. Because of this, the authorization flow is not usable", LogLevel.Err);
                return null;
            }

            // Create a new oauth state for this request
            OAuthState state = new OAuthState()
                {
                    Config = config,
                    UniqueId = Guid.NewGuid().ToString("N"),
                    DurandalPluginId = owningPlugin.PluginId,
                    DurandalUserId = durandalUserId,
                    OriginalTraceId = queryLogger.TraceId.HasValue ? CommonInstrumentation.FormatTraceId(queryLogger.TraceId.Value) : null
                };

            // Then build the URI
            HttpRequest uriBuilder = HttpRequest.CreateOutgoing(authUri.AbsoluteUri);
            uriBuilder.GetParameters["response_type"] = "code";
            uriBuilder.GetParameters["client_id"] = config.ClientId;
            uriBuilder.GetParameters["scope"] = config.Scope;
            uriBuilder.GetParameters["redirect_uri"] = _authorizeRedirectUrl;
            uriBuilder.GetParameters["state"] = state.UniqueId;

            // Are we using PKCE? If so, make a code verifier
            if (state.Config.UsePKCE)
            {
                byte[] codeVerifierBytes = new byte[32];
                _srand.NextBytes(codeVerifierBytes);
                state.PKCECodeVerifier = BinaryHelpers.EncodeUrlSafeBase64(codeVerifierBytes, 0, codeVerifierBytes.Length);

                SHA256 hasher = new SHA256();
                byte[] codeVerifier = Encoding.UTF8.GetBytes(state.PKCECodeVerifier);
                byte[] hashedCodeChallenge = hasher.ComputeHash(codeVerifier);
                string codeChallenge = BinaryHelpers.EncodeUrlSafeBase64(hashedCodeChallenge, 0, hashedCodeChallenge.Length);
                uriBuilder.GetParameters["code_challenge"] = codeChallenge;
                uriBuilder.GetParameters["code_challenge_method"] = "S256";
            }

            // Save the state that this URL will hook into
            await _secretStore.SaveState(state, queryLogger.TraceId).ConfigureAwait(false);

            return new Uri(authUri.Scheme + "://" + authUri.Authority + uriBuilder.BuildUri());
        }

        /// <summary>
        /// When a third-party site finishes its login, it will hand the auth code to the callback URL.
        /// This should be the method that handles that request on the HTTP listener.
        /// </summary>
        /// <param name="incomingRequest">The HTTP request that has been posted to us by the third party service</param>
        /// <param name="genericLogger">A logger</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time, for unit tests</param>
        /// <returns>True if the auth code was successfully stored, error if something bad happened</returns>
        public async Task HandleThirdPartyAuthCodeCallback(HttpRequest incomingRequest, ILogger genericLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Figure out what stateID this is calling back for
            string stateId = null;
            // OAuth spec says it should be in the "state" URL parameter
            if (incomingRequest.GetParameters != null && incomingRequest.GetParameters.ContainsKey("state"))
            {
                stateId = incomingRequest.GetParameters["state"];
            }
            else if (incomingRequest.RequestHeaders.ContainsKey("Referer"))
            {
                // Try and find the state parameter from the referer header
                string referer = incomingRequest.RequestHeaders["Referer"];
                HttpRequest refererRequest = HttpRequest.CreateOutgoing(referer);
                if (refererRequest != null && refererRequest.GetParameters != null && refererRequest.GetParameters.ContainsKey("state"))
                {
                    stateId = refererRequest.GetParameters["state"];
                }
            }

            if (string.IsNullOrEmpty(stateId))
            {
                // We can't do anything because we don't know what request this is correlated with
                throw new Exception("Cannot correlate OAuth callback with a valid OAuth state ID!");
            }

            // Retrieve that OAuth state and stash the code
            RetrieveResult<OAuthState> stateRetrieveResult = await _secretStore.RetrieveState(stateId, genericLogger.TraceId).ConfigureAwait(false);

            if (!stateRetrieveResult.Success)
            {
                throw new Exception("No OAuth state found");
            }

            OAuthState state = stateRetrieveResult.Result;

            // Build a quick queryLogger
            ILogger queryLogger = genericLogger.Clone("OAuthCallback");
            if (!string.IsNullOrEmpty(state.OriginalTraceId))
            {
                queryLogger = queryLogger.CreateTraceLogger(CommonInstrumentation.TryParseTraceIdGuid(state.OriginalTraceId));
            }

            // Do we even have an auth_code?
            if (!incomingRequest.GetParameters.ContainsKey("code"))
            {
                // No auth_code. Something bad happened
                string errorMessage = "Third-party OAuth callback did not provide a valid code.";
                ArraySegment<byte> responseMessage = await incomingRequest.ReadContentAsByteArrayAsync(cancelToken, realTime).ConfigureAwait(false);
                if (responseMessage.Array != null && responseMessage.Count > 0)
                {
                    errorMessage += " Payload was \"" + Encoding.UTF8.GetString(responseMessage.Array, responseMessage.Offset, responseMessage.Count) + "\"";
                }

                throw new Exception(errorMessage);
            }
            
            // We have a code and everything is fine. Update the state
            state.AuthCode = incomingRequest.GetParameters["code"];
            state.AuthCodeIssuedAt = realTime.Time.UtcDateTime;

            // Call UpdateToken to get the actual token
            bool tokenIsValid = await UpdateToken(state, queryLogger, cancelToken, realTime, false).ConfigureAwait(false);

            if (!tokenIsValid)
            {
                throw new Exception("Failed to get initial token");
            }

            // And then save the state back
            await _secretStore.SaveState(state, queryLogger.TraceId).ConfigureAwait(false);

            queryLogger.Log("OAuth callback succeeded");
        }

        /// <summary>
        /// Validates and potentially refreshes the current auth token.
        /// </summary>
        /// <param name="state">Current OAuth state</param>
        /// <param name="queryLogger">A query logger</param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time, for unit tests</param>
        /// <param name="updateStateIfChanged">If true, update the secret store immediately if we retrieved a new token</param>
        /// <returns>True if the oauth state contains a valid, non-null and non-expired token</returns>
        private async Task<bool> UpdateToken(OAuthState state, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime, bool updateStateIfChanged = true)
        {
            // Do we have a token at all?
            if (state.Token == null)
            {
                // Do we still have a valid auth code? Then use it to retrieve our token
                if (!string.IsNullOrEmpty(state.AuthCode) && (state.AuthCodeIssuedAt + TimeSpan.FromHours(24)) > realTime.Time.UtcDateTime)
                {
                    // Do the call
                    state.Token = await GetAccessToken(state, queryLogger, cancelToken, realTime).ConfigureAwait(false);

                    if (updateStateIfChanged)
                    {
                        // Save our state back
                        await _secretStore.SaveState(state, queryLogger.TraceId).ConfigureAwait(false);
                    }

                    return state.Token != null;
                }

                // No token and no auth code. Can't proceed
                return false;
            }

            // Has our token expired?
            if (state.Token.ExpiresAt < realTime.Time)
            {
                // Do we have a refresh token?
                if (!string.IsNullOrEmpty(state.Token.RefreshToken))
                {
                    // Invoke the refresh flow
                    state.Token = await RefreshToken(state, queryLogger, cancelToken, realTime).ConfigureAwait(false);
                    if (state.Token == null)
                    {
                        // Refresh failed. Delete our state
                        queryLogger.Log("Token has expired and refresh flow failed. User must reauthenticate", LogLevel.Wrn);
                        await _secretStore.DeleteState(state.UniqueId, queryLogger.TraceId).ConfigureAwait(false);
                    }
                    else if (updateStateIfChanged)
                    {
                        // Refresh succeeded. Save our state back
                        await _secretStore.SaveState(state, queryLogger.TraceId).ConfigureAwait(false);
                    }

                    return state.Token != null && state.Token.ExpiresAt > realTime.Time;
                }
                else
                {
                    queryLogger.Log("Token has expired and has no associated refresh token. User must reauthenticate", LogLevel.Wrn);
                }
            }

            return state.Token != null && state.Token.ExpiresAt > realTime.Time;
        }
        
        private class SerializedAuthToken
        {
            public string access_token { get; set; }
            public long expires_in { get; set; }
            public string token_type { get; set; }
            public string refresh_token { get; set; }
            public string scope { get; set; }
            // todo: what else can go here?
        }

        /// <summary>
        /// Uses an auth code to retrieve an initial auth token from the token URL service.
        /// This is not for refresh; it only works the first time (todo change that?)
        /// </summary>
        /// <param name="state"></param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time, for unit tests</param>
        /// <returns>An access token, or null if there was an error</returns>
        private async Task<OAuthToken> GetAccessToken(OAuthState state, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            Uri tokenUri = new Uri(state.Config.TokenUri);
            IHttpClient tokenClient = _httpClientFactory.CreateHttpClient(tokenUri.Host, tokenUri.Port, tokenUri.Scheme.Equals("https"), queryLogger);
            using (HttpRequest tokenRequest = HttpRequest.CreateOutgoing(tokenUri.AbsolutePath, "POST"))
            {
                // Add headers if needed
                if (!string.IsNullOrEmpty(state.Config.AuthorizationHeader))
                {
                    tokenRequest.RequestHeaders.Add("Authorization", state.Config.AuthorizationHeader);
                }

                Dictionary<string, string> postParams = new Dictionary<string, string>();
                postParams["grant_type"] = "authorization_code";
                postParams["code"] = state.AuthCode;
                postParams["client_id"] = state.Config.ClientId;
                postParams["client_secret"] = state.Config.ClientSecret;
                postParams["redirect_uri"] = _authorizeRedirectUrl;

                if (state.Config.UsePKCE)
                {
                    postParams["code_verifier"] = state.PKCECodeVerifier;
                }

                tokenRequest.SetContent(postParams);

                using (HttpResponse tokenResponse = await tokenClient.SendRequestAsync(
                    tokenRequest, cancelToken, realTime, queryLogger).ConfigureAwait(false))
                {
                    if (tokenResponse == null)
                    {
                        queryLogger.Log("Getting auth token failed", LogLevel.Err);
                        return null;
                    }

                    try
                    {
                        if (tokenResponse.ResponseCode != 200)
                        {
                            queryLogger.Log("Getting auth token failed with HTTP error " + tokenResponse.ResponseCode, LogLevel.Err);
                            string errorPayload = await tokenResponse.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(errorPayload))
                            {
                                queryLogger.Log(errorPayload, LogLevel.Vrb);
                            }

                            return null;
                        }

                        SerializedAuthToken rawToken = await tokenResponse.ReadContentAsJsonObjectAsync<SerializedAuthToken>(cancelToken, realTime).ConfigureAwait(false);

                        OAuthToken returnVal = new OAuthToken()
                        {
                            Token = rawToken.access_token,
                            TokenType = rawToken.token_type,
                            IssuedAt = realTime.Time,
                            ExpiresAt = realTime.Time.AddSeconds(rawToken.expires_in),
                            RefreshToken = rawToken.refresh_token
                        };

                        return returnVal;
                    }
                    finally
                    {
                        await tokenResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Invokes the token refresh flow using a previously stored refresh token.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="queryLogger"></param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time, for unit tests</param>
        /// <returns></returns>
        private async Task<OAuthToken> RefreshToken(OAuthState state, ILogger queryLogger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (state.Token == null || state.Token.RefreshToken == null)
            {
                queryLogger.Log("Attempting to refresh a token that has no stored refresh token", LogLevel.Err);
                return null;
            }

            Uri tokenUri = new Uri(state.Config.TokenUri);
            IHttpClient tokenClient = _httpClientFactory.CreateHttpClient(tokenUri.Host, tokenUri.Port, tokenUri.Scheme.Equals("https"), queryLogger);
            using (HttpRequest tokenRequest = HttpRequest.CreateOutgoing(tokenUri.AbsolutePath, "POST"))
            {
                Dictionary<string, string> postParams = new Dictionary<string, string>();
                postParams["grant_type"] = "refresh_token";
                postParams["client_id"] = state.Config.ClientId;
                postParams["client_secret"] = state.Config.ClientSecret;
                postParams["refresh_token"] = state.Token.RefreshToken;
                tokenRequest.SetContent(postParams);

                using (HttpResponse tokenResponse = await tokenClient.SendRequestAsync(
                    tokenRequest, CancellationToken.None, realTime, queryLogger).ConfigureAwait(false))
                {
                    if (tokenResponse == null)
                    {
                        queryLogger.Log("Refreshing auth token failed", LogLevel.Err);
                        return null;
                    }

                    try
                    {
                        if (tokenResponse.ResponseCode != 200)
                        {
                            queryLogger.Log("Refreshing auth token failed with HTTP error " + tokenResponse.ResponseCode, LogLevel.Err);
                            string errorPayload = await tokenResponse.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(errorPayload))
                            {
                                queryLogger.Log(errorPayload, LogLevel.Vrb);
                            }

                            return null;
                        }

                        SerializedAuthToken rawToken = await tokenResponse.ReadContentAsJsonObjectAsync<SerializedAuthToken>(cancelToken, realTime).ConfigureAwait(false);

                        OAuthToken returnVal = new OAuthToken()
                        {
                            Token = rawToken.access_token,
                            TokenType = rawToken.token_type,
                            IssuedAt = realTime.Time,
                            ExpiresAt = realTime.Time.AddSeconds(rawToken.expires_in),
                            RefreshToken = rawToken.refresh_token
                        };

                        return returnVal;
                    }
                    finally
                    {
                         await tokenResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
