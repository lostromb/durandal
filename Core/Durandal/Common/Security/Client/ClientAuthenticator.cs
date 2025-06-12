namespace Durandal.Common.Security.Client
{
    using System;
    using System.IO;

    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using Durandal.API;
    using Durandal.Common.Time;
    using Durandal.Common.Client;
    using Login;
    using Durandal.Common.Tasks;
    using System.Threading;
    using Durandal.Common.MathExt;

    /// <summary>
    /// A self-contained component for a client to manage its private key and authenticate itself with a Durandal service.
    /// </summary>
    public class ClientAuthenticator
    {
        /// <summary>
        /// Defines the time, in ms, that each request will remain valid before its token expires.
        /// </summary>
        private readonly TimeSpan DEFAULT_REQUEST_LIFETIME = TimeSpan.FromSeconds(300);

        private readonly ILogger _logger;
        private readonly IRSADelegates _rsaImpl;
        private readonly IClientSideKeyStore _keyStorage;

        /// <summary>
        /// The dictionary of all known client identities, keyed by client id
        /// </summary>
        private readonly IDictionary<string, ClientIdentity> _clientIdentities;

        /// <summary>
        /// The dictionary of all known user identities, keyed by user id
        /// </summary>
        private readonly IDictionary<string, UserIdentity> _userIdentities;

        /// <summary>
        /// The list of all providers which can give private keys to this client
        /// </summary>
        private readonly IDictionary<string, ILoginProvider> _loginProviders;

        private ClientAuthenticator(
            ILogger logger,
            IRSADelegates rsaImpl,
            IClientSideKeyStore keyStorage,
            IList<ILoginProvider> loginProviders)
        {
            _logger = logger;
            _keyStorage = keyStorage;
            _rsaImpl = rsaImpl;
            _clientIdentities = new Dictionary<string, ClientIdentity>();
            _userIdentities = new Dictionary<string, UserIdentity>();
            _loginProviders = new Dictionary<string, ILoginProvider>();
            foreach (ILoginProvider loginProvider in loginProviders)
            {
                _loginProviders.Add(loginProvider.ProviderName, loginProvider);
            }
        }

        /// <summary>
        /// Creates a client authentication manager
        /// </summary>
        /// <param name="logger">A logger</param>
        /// <param name="rsaImpl">An implementation of RSA algorithms for encryption</param>
        /// <param name="keyStorage">A secure storage for the private keys of each identity</param>
        /// <param name="loginProviders">A list of providers that can provide private keys for different identities</param>
        /// <returns></returns>
        public static async Task<ClientAuthenticator> Create(
            ILogger logger,
            IRSADelegates rsaImpl,
            IClientSideKeyStore keyStorage,
            IList<ILoginProvider> loginProviders = null)
        {
            ClientAuthenticator returnVal = new ClientAuthenticator(logger, rsaImpl, keyStorage, loginProviders ?? new List<ILoginProvider>());
            await returnVal.LoadPersistedIdentities().ConfigureAwait(false);
            return returnVal;
        }

        private async Task LoadPersistedIdentities()
        {
            _userIdentities.Clear();
            foreach (UserIdentity ident in await _keyStorage.GetUserIdentities().ConfigureAwait(false))
            {
                _userIdentities[ident.Id] = ident;
            }

            _clientIdentities.Clear();
            foreach (ClientIdentity ident in await _keyStorage.GetClientIdentities().ConfigureAwait(false))
            {
                _clientIdentities[ident.Id] = ident;
            }
        }

        /// <summary>
        /// Lists all user identities currently registered with this service
        /// </summary>
        /// <returns></returns>
        public IList<UserIdentity> GetAvailableUserIdentities()
        {
            IList<UserIdentity> returnVal = new List<UserIdentity>();
            foreach (UserIdentity userIdent in _userIdentities.Values)
            {
                returnVal.Add(userIdent);
            }

            return returnVal;
        }

        /// <summary>
        /// Logs out a specified user, removing their context and associated private keys from the device.
        /// </summary>
        /// <param name="userId"></param>
        public async Task LogOutUser(string userId)
        {
            _logger.Log("Logging out user " + userId);

            if (!_userIdentities.ContainsKey(userId))
            {
                throw new ArgumentException("User id \"" + userId + "\" is not known");
            }

            await _keyStorage.DeleteIdentity(new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: userId)).ConfigureAwait(false);
            _userIdentities.Remove(userId);
        }

        /// <summary>
        /// Lists all user identities currently registered with this service
        /// </summary>
        /// <returns></returns>
        public IList<ClientIdentity> GetAvailableClientIdentities()
        {
            IList<ClientIdentity> returnVal = new List<ClientIdentity>();
            foreach (ClientIdentity clientIdent in _clientIdentities.Values)
            {
                returnVal.Add(clientIdent);
            }

            return returnVal;
        }

        /// <summary>
        /// Provides credentials to this authenticator that will be used to register a new user account.
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="externalToken"></param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns></returns>
        public async Task<UserIdentity> RegisterNewAuthenticatedUser(string providerName, string externalToken, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            _logger.Log("Starting to register a new authenticated user with provider " + providerName);
            ILoginProvider provider = null;
            if (!_loginProviders.TryGetValue(providerName, out provider))
            {
                throw new ArgumentException("Unknown login provider \"" + providerName + "\"");
            }

            RetrieveResult<UserClientSecretInfo> retrieveResult = await provider.GetSecretUserInfo(ClientAuthenticationScope.User, _logger, externalToken, cancelToken, realTime).ConfigureAwait(false);
            if (!retrieveResult.Success)
            {
                return null;
            }

            UserClientSecretInfo secretInfo = retrieveResult.Result;
            UserIdentity publicInfo = ConvertSecretInfoToUserIdentity(secretInfo);
            _logger.Log("Registering new user identity with ID  " + secretInfo.UserId);
            if (_userIdentities.ContainsKey(secretInfo.UserId))
            {
                throw new Exception("That user is already logged in!");
            }

            _userIdentities.Add(secretInfo.UserId, publicInfo);
            
            if (_keyStorage != null)
            {
                bool success = await _keyStorage.StoreIdentity(secretInfo).ConfigureAwait(false);
                if (success)
                {
                    _logger.Log("Wrote private key information to local store");
                }
                else
                {
                    _logger.Log("Error occurred while writing private key information to local store", LogLevel.Err);
                }
            }

            return publicInfo;
        }

        /// <summary>
        /// Provides credentials to this authenticator which will be retrieved from the given auth provider (though that provider will be "adhoc" for all currently conceivable cases)
        /// </summary>
        /// <param name="providerName"></param>
        /// <param name="externalToken"></param>
        /// <param name="cancelToken">A cancellation token</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns></returns>
        public async Task<ClientIdentity> RegisterAuthenticatedClient(string providerName, string externalToken, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            ILoginProvider provider = null;
            if (!_loginProviders.TryGetValue(providerName, out provider))
            {
                throw new ArgumentException("Unknown login provider \"" + providerName + "\"");
            }

            RetrieveResult<UserClientSecretInfo> retrieveResult = await provider.GetSecretUserInfo(
                ClientAuthenticationScope.Client, _logger, externalToken, cancelToken, realTime).ConfigureAwait(false);
            if (!retrieveResult.Success)
            {
                return null;
            }

            UserClientSecretInfo secretInfo = retrieveResult.Result;
            _logger.Log("Registering new client identity with ID  " + secretInfo.ClientId);
            ClientIdentity publicInfo = ConvertSecretInfoToClientIdentity(secretInfo);
            _clientIdentities.Add(secretInfo.ClientId, publicInfo);

            if (_keyStorage != null)
            {
                bool success = await _keyStorage.StoreIdentity(secretInfo).ConfigureAwait(false);
                if (success)
                {
                    _logger.Log("Wrote private key information to local store");
                }
                else
                {
                    _logger.Log("Error occurred while writing private key information to local store", LogLevel.Err);
                }
            }

            return publicInfo;
        }
        
        /// <summary>
        /// Uses the current authenticator state (including active user + client identities) to add authentication tokens to the input client request.
        /// If the active user identity is different from the one in clientrequest.context, that identity will be OVERWRITTEN. Same with client identity.
        /// </summary>
        /// <param name="targetRequest"></param>
        /// <param name="logger"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public async Task AuthenticateClientRequest(DialogRequest targetRequest, ILogger logger, IRealTimeProvider realTime)
        {
            string userId = targetRequest.ClientContext.UserId;
            string clientId = targetRequest.ClientContext.ClientId;

            //ClientKeyIdentifier keyId = new ClientKeyIdentifier(ClientAuthenticationScope.UserClient, userId: userId, clientId: clientId);
            // First try user+client combined auth
            // TODO If we ever want to support user+client combined auth we'll need to reimplement this
            //if (_knownKeys.TryGetValue(keyId, out clientState))
            //{
            //    logger.Log("Adding auth token for UserClient scope " + userId);
            //    if (AddAuthTokenToRequest(targetRequest, clientState, keyId.Scope, logger))
            //    {
            //        return;
            //    }
            //}

            // If that fails, fall back to trying client + user individually
            if (_userIdentities.ContainsKey(userId))
            {
                logger.Log("Adding auth token for User scope " + userId);
                ClientKeyIdentifier keyId = new ClientKeyIdentifier(ClientAuthenticationScope.User, userId: userId);
                await AddAuthTokenToRequest(targetRequest, keyId, logger, realTime).ConfigureAwait(false);
            }

            if (_clientIdentities.ContainsKey(clientId))
            {
                logger.Log("Adding auth token for Client scope " + clientId);
                ClientKeyIdentifier keyId = new ClientKeyIdentifier(ClientAuthenticationScope.Client, clientId: clientId);
                await AddAuthTokenToRequest(targetRequest, keyId, logger, realTime).ConfigureAwait(false);
            }
        }

        private async Task<bool> AddAuthTokenToRequest(DialogRequest targetRequest, ClientKeyIdentifier keyId, ILogger logger, IRealTimeProvider realTime, TimeSpan? requestExpireTime = null)
        {
            UserClientSecretInfo secretInfo = await _keyStorage.LoadIdentity(keyId).ConfigureAwait(false);

            if (secretInfo.SaltValue == null)
            {
                logger.Log("Security token was not generated for " + keyId.ToString() + " because salt value is null", LogLevel.Err);
                return false;
            }

            BigInteger tokenRed = CryptographyHelpers.GenerateRequestExpireTimeToken(requestExpireTime.GetValueOrDefault(DEFAULT_REQUEST_LIFETIME), realTime);
            BigInteger tokenBlue = _rsaImpl.Encrypt(tokenRed ^ secretInfo.SaltValue, secretInfo.PrivateKey);
            RequestToken authToken = new RequestToken(tokenRed, tokenBlue);
            if (authToken == null || authToken.TokenRed == null || authToken.TokenBlue == null)
            {
                return false;
            }

            SecurityToken returnVal = new SecurityToken();
            returnVal.Red = CryptographyHelpers.SerializeKey(authToken.TokenRed);
            returnVal.Blue = CryptographyHelpers.SerializeKey(authToken.TokenBlue);
            returnVal.Scope = keyId.Scope;
            
            if (targetRequest.AuthTokens == null)
            {
                targetRequest.AuthTokens = new List<SecurityToken>();
            }

            targetRequest.AuthTokens.Add(returnVal);
            return true;
        }
        
        private UserIdentity ConvertSecretInfoToUserIdentity(UserClientSecretInfo secret)
        {
            return new UserIdentity()
            {
                Id = secret.UserId,
                FullName = secret.UserFullName,
                GivenName = secret.UserGivenName,
                Surname = secret.UserSurname,
                Email = secret.UserEmail,
                IconPng = secret.UserIconPng,
                AuthProvider = secret.AuthProvider
            };
        }

        private ClientIdentity ConvertSecretInfoToClientIdentity(UserClientSecretInfo secret)
        {
            return new ClientIdentity()
            {
                Id = secret.ClientId,
                Name = secret.ClientName,
                AuthProvider = secret.AuthProvider
            };
        }

        /// <summary>
        /// Ensures that required fields are not null, and sanitizes fields that should be null in a client key identifier
        /// </summary>
        /// <param name="keyId"></param>
        private static void ValidateKeyId(ClientKeyIdentifier keyId)
        {
            if (keyId.Scope == ClientAuthenticationScope.None)
            {
                throw new ArgumentNullException(nameof(keyId));
            }
            if (keyId.Scope.HasFlag(ClientAuthenticationScope.User) && string.IsNullOrEmpty(keyId.UserId))
            {
                throw new ArgumentNullException("User ID");
            }
            if (keyId.Scope.HasFlag(ClientAuthenticationScope.Client) && string.IsNullOrEmpty(keyId.ClientId))
            {
                throw new ArgumentNullException("Client ID");
            }
        }

        private static ClientKeyIdentifier SanitizeKeyId(ClientKeyIdentifier keyId)
        {
            ClientKeyIdentifier clone = new ClientKeyIdentifier(keyId.Scope);
            if (keyId.Scope.HasFlag(ClientAuthenticationScope.Client))
            {
                clone.ClientId = keyId.ClientId;
            }
            if (keyId.Scope.HasFlag(ClientAuthenticationScope.User))
            {
                clone.UserId = keyId.UserId;
            }

            return clone;
        }

        private static ClientIdentifier SanitizeClientInfo(ClientIdentifier clientInfo, ClientAuthenticationScope keyScope)
        {
            ClientIdentifier clone = new ClientIdentifier();
            if (keyScope.HasFlag(ClientAuthenticationScope.Client))
            {
                clone.ClientId = clientInfo.ClientId;
                clone.ClientName = clientInfo.ClientName;
            }
            if (keyScope.HasFlag(ClientAuthenticationScope.User))
            {
                clone.UserId = clientInfo.UserId;
                clone.UserName = clientInfo.UserName;
            }

            return clone;
        }
    }
}
