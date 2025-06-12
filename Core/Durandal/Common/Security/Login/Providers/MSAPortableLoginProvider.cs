using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.Security.Client;
using Durandal.Common.Net.Http;
using Newtonsoft.Json;
using Durandal.Common.Security.OAuth;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using Durandal.Common.Security.Server;
using Durandal.Common.Utils;
using System.Threading;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Security.Login.Providers
{
    public class MSAPortableLoginProvider : ILoginProvider
    {
        // COMMON
        private readonly ILogger _logger;
        private int _disposed = 0;

        // CLIENT-ONLY PARAMETERS
        private readonly IHttpClient _durandalTokenHttpClient;

        // SERVER-ONLY PARAMETERS
        private static readonly TimeSpan LOGIN_WINDOW = TimeSpan.FromMinutes(5);
        private readonly IHttpClient _msoHttpClient;
        private readonly IHttpClient _msGraphHttpClient;
        private readonly IPrivateKeyStore _privateKeyVault;
        private readonly IPublicKeyStore _publicKeyVault;
        private readonly IRSADelegates _keyGenerator;
        private readonly string _oauthClientId;
        private readonly string _oauthClientSecret;
        private readonly string _oauthCallbackUrl;
        private readonly int _keySize;
        
        public string ProviderName => "msa-portable";
        public string UserIdScheme => "msa";
        public ClientAuthenticationScope SupportedScopes => ClientAuthenticationScope.User;
        public bool AllowMultiTenancy => true;

        /// <summary>
        /// Client constructor
        /// </summary>
        /// <param name="clientFactory"></param>
        /// <param name="logger"></param>
        /// <param name="durandalTokenServiceHost"></param>
        private MSAPortableLoginProvider(IHttpClientFactory clientFactory, ILogger logger, Uri durandalTokenServiceHost)
        {
            _logger = logger;
            _durandalTokenHttpClient = clientFactory.CreateHttpClient(durandalTokenServiceHost, logger);
            if (!string.Equals(durandalTokenServiceHost.Scheme, "https"))
            {
                _logger.Log("Durandal token service URL \"" + durandalTokenServiceHost.AbsoluteUri + "\" does not use HTTPS! This is highly discouraged for what should be obvious reasons", LogLevel.Err);
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        /// <summary>
        /// Server constructor
        /// </summary>
        /// <param name="clientFactory"></param>
        /// <param name="logger"></param>
        /// <param name="oauthClientId"></param>
        /// <param name="oauthClientSecret"></param>
        /// <param name="oauthCallbackUrl"></param>
        /// <param name="privateKeyVault"></param>
        /// <param name="publicKeyVault"></param>
        /// <param name="keyGenerator"></param>
        /// <param name="keySize"></param>
        private MSAPortableLoginProvider(
            IHttpClientFactory clientFactory,
            ILogger logger,
            string oauthClientId,
            string oauthClientSecret,
            Uri oauthCallbackUrl,
            IPrivateKeyStore privateKeyVault,
            IPublicKeyStore publicKeyVault,
            IRSADelegates keyGenerator, int keySize)
        {
            _logger = logger;
            _msGraphHttpClient = clientFactory.CreateHttpClient(new Uri("https://graph.microsoft.com"), logger);
            _msoHttpClient = clientFactory.CreateHttpClient(new Uri("https://login.microsoftonline.com"), logger);
            _privateKeyVault = privateKeyVault;
            _publicKeyVault = publicKeyVault;
            _oauthClientId = oauthClientId;
            _oauthClientSecret = oauthClientSecret;
            _keyGenerator = keyGenerator;
            _keySize = keySize;
            if (_keySize < 512)
            {
                throw new ArgumentOutOfRangeException("RSA key size cannot be smaller than 512 bits");
            }

            _oauthCallbackUrl = oauthCallbackUrl.Scheme + "://" + oauthCallbackUrl.Authority + "/auth/login/oauth/msa-portable";

            if (string.IsNullOrEmpty(oauthClientId))
            {
                _logger.Log("OAuth client ID is invalid!", LogLevel.Err);
            }
            if (string.IsNullOrEmpty(oauthClientSecret))
            {
                _logger.Log("OAuth client secret is invalid!", LogLevel.Err);
            }
            if (oauthCallbackUrl == null)
            {
                _logger.Log("OAuth redirect URL is invalid!", LogLevel.Err);
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }
        
        public static MSAPortableLoginProvider BuildForClient(IHttpClientFactory clientFactory, ILogger logger, Uri durandalTokenServiceHost)
        {
            return new MSAPortableLoginProvider(clientFactory, logger, durandalTokenServiceHost);
        }
        
        public static MSAPortableLoginProvider BuildForServer(
            IHttpClientFactory clientFactory,
            ILogger logger,
            string oauthClientId,
            string oauthClientSecret,
            Uri authCallbackUrl,
            IPrivateKeyStore privateKeyVault,
            IPublicKeyStore publicKeyVault,
            IRSADelegates keyGenerator = null,
            int keySize = 1024)
        {
            return new MSAPortableLoginProvider(
                clientFactory,
                logger,
                oauthClientId,
                oauthClientSecret,
                authCallbackUrl,
                privateKeyVault,
                publicKeyVault,
                keyGenerator ?? new StandardRSADelegates(),
                keySize);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~MSAPortableLoginProvider()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Called by the client: externalToken here is the state value generated at the start of the login
        /// </summary>
        /// <param name="keyScope"></param>
        /// <param name="logger"></param>
        /// <param name="externalToken"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public async Task<RetrieveResult<UserClientSecretInfo>> GetSecretUserInfo(
            ClientAuthenticationScope keyScope,
            ILogger logger,
            string externalToken,
            CancellationToken cancelToken,
            IRealTimeProvider realTime)
        {
            if (keyScope != ClientAuthenticationScope.User)
            {
                throw new ArgumentException("MSA auth provider only supports User auth scope");
            }

            logger = logger ?? _logger;
            using (HttpRequest getSecretInfoRequest = HttpRequest.CreateOutgoing("/auth/login/credentials/user/msa-portable", "GET"))
            {
                getSecretInfoRequest.RequestHeaders.Add("Authorization", "Token " + externalToken);

                string responsePayload = null;
                TimeSpan timeWaited = TimeSpan.Zero;
                TimeSpan retryInterval = TimeSpan.FromSeconds(5);
                TimeSpan maxWaitTime = TimeSpan.FromMinutes(1);
                do
                {
                    using (HttpResponse verifyResponse = await _durandalTokenHttpClient.SendRequestAsync(getSecretInfoRequest, cancelToken, realTime, logger).ConfigureAwait(false))
                    {
                        if (verifyResponse == null)
                        {
                            throw new Exception("Null response from auth service");
                        }

                        try
                        {
                            if (verifyResponse.ResponseCode == 202) // "Accepted" indicates the login is still in progress but user has not finished entering credentials yet
                            {
                                _logger.Log("Waiting for MSA credentials to be available... " + timeWaited.ToString(), LogLevel.Vrb);
                                await realTime.WaitAsync(retryInterval, cancelToken).ConfigureAwait(false);
                                timeWaited += retryInterval;
                                if (cancelToken.IsCancellationRequested)
                                {
                                    return new RetrieveResult<UserClientSecretInfo>(null, timeWaited.TotalMilliseconds, false);
                                }
                            }
                            else if (verifyResponse.ResponseCode == 200)
                            {
                                responsePayload = await verifyResponse.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false);
                            }
                            else
                            {
                                throw new Exception("Non-success response from auth service: " + verifyResponse.ResponseCode + " " + (await verifyResponse.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false)));
                            }
                        }
                        finally
                        {
                            if (verifyResponse != null)
                            {
                                await verifyResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                                verifyResponse.Dispose();
                            }
                        }
                    }
                } while (responsePayload == null && timeWaited < maxWaitTime);

                if (responsePayload == null)
                {
                    _logger.Log("Could not retrieve user credentials after " + timeWaited.TotalMilliseconds + " ms", LogLevel.Wrn);
                    return new RetrieveResult<UserClientSecretInfo>(null, timeWaited.TotalMilliseconds, false);
                }

                UserClientSecretInfo secretInfo = JsonConvert.DeserializeObject<UserClientSecretInfo>(responsePayload);
                return new RetrieveResult<UserClientSecretInfo>(secretInfo, timeWaited.TotalMilliseconds, true);
            }
        }
        
        /// <summary>
        /// Called on server side. External token is the state object generated by the client at start of login
        /// </summary>
        /// <param name="keyScope"></param>
        /// <param name="externalToken"></param>
        /// <param name="logger"></param>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public async Task<UserClientSecretInfo> VerifyExternalToken(ClientAuthenticationScope keyScope, ILogger logger, string externalToken, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            logger = logger ?? _logger;

            // First, get the logged in info from when we handled the initial callback
            RetrieveResult<PrivateKeyVaultEntry> userInfoRetrieve = await _privateKeyVault.GetUserInfoByStateKey(externalToken).ConfigureAwait(false);
            if (!userInfoRetrieve.Success || userInfoRetrieve.Result == null)
            {
                logger.Log("Login has not been initiated");
                return null;
            }

            PrivateKeyVaultEntry userInfo = userInfoRetrieve.Result;
            // Did the grace period expire?
            if ((realTime.Time - userInfo.LastLoginTime) > LOGIN_WINDOW)
            {
                userInfo.LoginInProgress = false;
                await _privateKeyVault.UpdateLoggedInUserInfo(userInfo).ConfigureAwait(false);
                throw new Exception("Login expired");
            }

            ClientKeyIdentifier keyId = new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: userInfo.VaultEntry.UserId);

            if (!userInfo.LoginInProgress)
            {
                // If we reach this point and a login is not in progress, we have to assume
                // that more than one actor has tried to access the private key on this turn.
                // Therefore, assume foul play and invalidate the user's key entirely.
                _logger.Log("Client has attempted to access the private key while a login was not active. For security reasons I will now invalidate the private key for " + keyId.ToString(), LogLevel.Err);
                await _publicKeyVault.DeleteClientState(keyId).ConfigureAwait(false);
                await _privateKeyVault.DeleteLoggedInUserInfo(keyId).ConfigureAwait(false);
                throw new Exception("Duplicate key fetch detected - user credentials have been invalidated.");
            }

            // Mark the login as completed on the private key table
            userInfo.LoginInProgress = false;
            await _privateKeyVault.UpdateLoggedInUserInfo(userInfo).ConfigureAwait(false);

            // Tell the public key vault that we trust this user now
            RetrieveResult<ServerSideAuthenticationState> existingPublicStateRetrieveResult = await _publicKeyVault.GetClientState(keyId).ConfigureAwait(false);

            if (!existingPublicStateRetrieveResult.Success || existingPublicStateRetrieveResult.Result == null)
            {
                // Create new public key table entry if none exists for whatever reason
                ServerSideAuthenticationState publicKeyState = new ServerSideAuthenticationState();
                publicKeyState.ClientInfo = new ClientIdentifier(userInfo.VaultEntry.UserId, userInfo.VaultEntry.UserFullName, null, null);
                publicKeyState.KeyScope = ClientAuthenticationScope.User;
                publicKeyState.PubKey = userInfo.VaultEntry.PrivateKey.GetPublicKey();
                publicKeyState.SaltValue = userInfo.VaultEntry.SaltValue;
                publicKeyState.Trusted = false;
                await _publicKeyVault.UpdateClientState(publicKeyState).ConfigureAwait(false);
            }
            else
            {
                existingPublicStateRetrieveResult.Result.Trusted = true;
                await _publicKeyVault.UpdateClientState(existingPublicStateRetrieveResult.Result).ConfigureAwait(false);
            }
            
            // We're validated, so we can return the user's secret login info now
            return userInfo.VaultEntry;
        }

        public async Task HandleOAuthCallback(HttpRequest callbackRequest, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (!callbackRequest.GetParameters.ContainsKey("code"))
            {
                throw new ArgumentException("OAuth callback is missing code parameter");
            }

            string code = callbackRequest.GetParameters["code"];
            string state;
            if (!callbackRequest.GetParameters.TryGetValue("state", out state))
            {
                state = string.Empty;
            }

            string authToken = await GetToken(code, logger, cancelToken, realTime).ConfigureAwait(false);

            if (authToken == null)
            {
                logger.Log("Unable to retrieve auth token!", LogLevel.Err);
                return;
            }

            ProfileResponseData userProfile = await GetLiveUserProfile(authToken, logger, cancelToken, realTime).ConfigureAwait(false);

            if (userProfile == null)
            {
                logger.Log("Unable to retrieve user profile!", LogLevel.Err);
                return;
            }

            // Does this user already exist?
            UserClientSecretInfo convertedUserInfo = ConvertLiveProfileToDurandalUserInfo(userProfile);
            ClientKeyIdentifier clientKey = new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: convertedUserInfo.UserId);

            // Try and fetch public key and private key state in parallel, since we'll eventually need both
            Task<RetrieveResult<ServerSideAuthenticationState>> publicKeyFetchTask = _publicKeyVault.GetClientState(clientKey);
            Task<RetrieveResult<PrivateKeyVaultEntry>> privateKeyFetchTask = _privateKeyVault.GetUserInfoById(clientKey);
            RetrieveResult<ServerSideAuthenticationState> publicKeyExistingResult = await publicKeyFetchTask.ConfigureAwait(false);
            RetrieveResult<PrivateKeyVaultEntry> loginInfoRetrieve = await privateKeyFetchTask.ConfigureAwait(false);

            PrivateKeyVaultEntry loginInfo;
            if (!loginInfoRetrieve.Success || loginInfoRetrieve.Result == null)
            {
                loginInfo = new PrivateKeyVaultEntry();
                PrivateKey newPrivateKey = _keyGenerator.GenerateRSAKey(_keySize);
                convertedUserInfo.PrivateKey = newPrivateKey;
                convertedUserInfo.SaltValue = CryptographyHelpers.GenerateRandomToken(newPrivateKey.N, 128);
                convertedUserInfo.AuthProvider = ProviderName;
                loginInfo.VaultEntry = convertedUserInfo;
            }
            else
            {
                loginInfo = loginInfoRetrieve.Result;
            }

            // Update the last login and login state code
            loginInfo.LastLoginTime = realTime.Time;
            loginInfo.LoginState = state;
            loginInfo.LoginInProgress = true;
            Task updatePrivateKeyTask = _privateKeyVault.UpdateLoggedInUserInfo(loginInfo);

            // Does a public key state already exist?
            if (!publicKeyExistingResult.Success)
            {
                // Create an entry in the public key database if needed
                ServerSideAuthenticationState publicKeyState = new ServerSideAuthenticationState();
                publicKeyState.ClientInfo = new ClientIdentifier(loginInfo.VaultEntry.UserId, loginInfo.VaultEntry.UserFullName, null, null);
                publicKeyState.KeyScope = ClientAuthenticationScope.User;
                publicKeyState.PubKey = loginInfo.VaultEntry.PrivateKey.GetPublicKey();
                publicKeyState.SaltValue = loginInfo.VaultEntry.SaltValue;
                publicKeyState.Trusted = false;
                await _publicKeyVault.UpdateClientState(publicKeyState).ConfigureAwait(false);
            }

            // Code is ordered this way so that writes are done in parallel
            await updatePrivateKeyTask.ConfigureAwait(false);
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
                _durandalTokenHttpClient?.Dispose();
                _msoHttpClient?.Dispose();
                _msGraphHttpClient?.Dispose();
            }
        }

        private async Task<string> GetToken(string code, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            HttpRequest getCodeRequest = HttpRequest.CreateOutgoing("/common/oauth2/v2.0/token", "POST");
            getCodeRequest.RequestHeaders.Add(HttpConstants.HEADER_KEY_CONTENT_TYPE, HttpConstants.MIME_TYPE_FORMDATA);
            Dictionary<string, string> formData = new Dictionary<string, string>();
            formData["client_id"] = _oauthClientId;
            formData["scope"] = "User.Read";
            formData["code"] = code;
            formData["redirect_uri"] = _oauthCallbackUrl;
            formData["grant_type"] = "authorization_code";
            formData["client_secret"] = _oauthClientSecret;
            getCodeRequest.SetContent(formData);
            using (HttpResponse getCodeResponse = await _msoHttpClient.SendRequestAsync(getCodeRequest, cancelToken, realTime, logger).ConfigureAwait(false))
            {
                if (getCodeResponse == null)
                {
                    return null;
                }

                try
                {
                    if (getCodeResponse.ResponseCode != 200)
                    {
                        return null;
                    }

                    OauthTokenResponse tokenResponse = JsonConvert.DeserializeObject<OauthTokenResponse>(await getCodeResponse.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false));
                    return tokenResponse.access_token;
                }
                finally
                {
                    if (getCodeResponse != null)
                    {
                        await getCodeResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task<ProfileResponseData> GetLiveUserProfile(string token, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            using (HttpRequest profileRequest = HttpRequest.CreateOutgoing("/v1.0/me", "GET"))
            {
                profileRequest.RequestHeaders.Add("Authorization", "Bearer " + token);
                using (HttpResponse verifyResponse = await _msGraphHttpClient.SendRequestAsync(profileRequest, cancelToken, realTime, logger).ConfigureAwait(false))
                {
                    if (verifyResponse == null)
                    {
                        return null;
                    }
                    if (verifyResponse.ResponseCode != 200)
                    {
                        return null;
                    }

                    ProfileResponseData liveProfile = await verifyResponse.ReadContentAsJsonObjectAsync<ProfileResponseData>(cancelToken, realTime).ConfigureAwait(false);
                    return liveProfile;
                }
            }
        }

        private static UserClientSecretInfo ConvertLiveProfileToDurandalUserInfo(ProfileResponseData profile)
        {
            UserClientSecretInfo userInfo = new UserClientSecretInfo();
            userInfo.UserEmail = !string.IsNullOrEmpty(profile.mail) ? profile.mail : profile.userPrincipalName;
            userInfo.UserFullName = profile.displayName;
            userInfo.UserGivenName = profile.givenName;
            userInfo.UserSurname = profile.surname;
            userInfo.UserId = "msa:" + profile.id;
            userInfo.UserIconPng = null;
            return userInfo;
        }

#pragma warning disable CS0649
        public class ProfileResponseData
        {
            public string displayName;
            public string givenName;
            public string mail; // Optional
            public string surname;
            public string userPrincipalName; // This is the user's email address in all cases I've seen
            public string id; // For MSA this is puid, for AAD this appears to be object ID
        }

        private class OauthTokenResponse
        {
            public string access_token;
            public string token_type;
            public string scope;
            public string refresh_token;
            public string id_token;
            public long expires_in;
        }
#pragma warning restore CS0649
    }
}
