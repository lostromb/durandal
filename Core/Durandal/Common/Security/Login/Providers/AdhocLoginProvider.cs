using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.API;
using Durandal.Common.Security.Client;
using Durandal.Common.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.Net.Http;
using Newtonsoft.Json;
using Durandal.Common.Time;
using Durandal.Common.Security.Server;
using Durandal.Common.Utils;
using System.Threading;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Security.Login.Providers
{
    public class AdhocLoginProvider : ILoginProvider
    {
        // COMMON
        private readonly ILogger _logger;
        private int _disposed = 0;

        // CLIENT-ONLY PARAMETERS
        private readonly IHttpClient _durandalTokenHttpClient;
        private readonly Uri _durandalTokenServiceHost;

        // SERVER-ONLY PARAMETERS
        private readonly IRSADelegates _rsaDelegates;
        private readonly IPrivateKeyStore _privateKeyVault;
        private readonly IPublicKeyStore _publicKeyVault;
        private readonly int _keySize;

        public string ProviderName => "adhoc";
        public string UserIdScheme => null;
        public ClientAuthenticationScope SupportedScopes => ClientAuthenticationScope.UserClient;
        public bool AllowMultiTenancy => false;

        // Client constructor
        private AdhocLoginProvider(IHttpClientFactory clientFactory, ILogger logger, Uri durandalTokenServiceHost)
        {
            _logger = logger;
            if (durandalTokenServiceHost != null)
            {
                if (!string.Equals(durandalTokenServiceHost.Scheme, "https"))
                {
                    _logger.Log("Durandal token service URL \"" + durandalTokenServiceHost.AbsoluteUri + "\" does not use HTTPS! This is highly discouraged for what should be obvious reasons", LogLevel.Err);
                }

                _durandalTokenHttpClient = clientFactory.CreateHttpClient(durandalTokenServiceHost, _logger);
            }
            else
            {
                _durandalTokenHttpClient = new NullHttpClient();
            }

            _durandalTokenServiceHost = durandalTokenServiceHost;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        private AdhocLoginProvider(ILogger logger, IPrivateKeyStore privateKeyVault, IPublicKeyStore publicKeyVault, IRSADelegates rsaImpl, int keySize)
        {
            _logger = logger;
            _rsaDelegates = rsaImpl;
            _privateKeyVault = privateKeyVault;
            _publicKeyVault = publicKeyVault;
            _keySize = keySize;
            if (_keySize < 512)
            {
                throw new ArgumentOutOfRangeException("RSA key size cannot be smaller than 512 bits");
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        public static AdhocLoginProvider BuildForClient(IHttpClientFactory clientFactory, ILogger logger, Uri durandalTokenServiceHost)
        {
            return new AdhocLoginProvider(clientFactory, logger, durandalTokenServiceHost);
        }

        public static AdhocLoginProvider BuildForServer(ILogger logger, IPrivateKeyStore privateKeyVault, IPublicKeyStore publicKeyVault, IRSADelegates rsaImpl = null, int keySize = 1024)
        {
            return new AdhocLoginProvider(logger, privateKeyVault, publicKeyVault, rsaImpl ?? new StandardRSADelegates(), keySize);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AdhocLoginProvider()
        {
            Dispose(false);
        }
#endif

        // ExternalToken is JSON-serialized UserClientSecretInfo without the private key
        public async Task<RetrieveResult<UserClientSecretInfo>> GetSecretUserInfo(ClientAuthenticationScope keyScope, ILogger logger, string externalToken, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            logger = logger ?? _logger;
            string scopeString = string.Empty;
            if (keyScope == ClientAuthenticationScope.User)
            {
                scopeString = "user";
            }
            else if (keyScope == ClientAuthenticationScope.Client)
            {
                scopeString = "client";
            }
            else if (keyScope == ClientAuthenticationScope.UserClient)
            {
                scopeString = "userclient";
            }
            else
            {
                throw new ArgumentException("Invalid key scope " + keyScope.ToString());
            }

            using (HttpRequest getSecretInfoRequest = HttpRequest.CreateOutgoing("/auth/login/credentials/" + scopeString + "/adhoc", "GET"))
            {
                getSecretInfoRequest.RequestHeaders.Add("Authorization", "Dummy " + externalToken);
                logger.Log("Requesting adhoc " + scopeString + " credentials...");
                using (HttpResponse verifyResponse = await _durandalTokenHttpClient.SendRequestAsync(getSecretInfoRequest, cancelToken, realTime, logger).ConfigureAwait(false))
                {
                    try
                    {
                        if (verifyResponse == null)
                        {
                            throw new Exception("Null response from auth service");
                        }

                        if (verifyResponse.ResponseCode != 200)
                        {
                            throw new Exception("Non-success response from auth service: " + verifyResponse.ResponseCode + " " + (await verifyResponse.ReadContentAsStringAsync(cancelToken, realTime).ConfigureAwait(false)));
                        }

                        logger.Log("Got " + scopeString + " credentials successfully");
                        UserClientSecretInfo secretInfo = await verifyResponse.ReadContentAsJsonObjectAsync<UserClientSecretInfo>(cancelToken, realTime).ConfigureAwait(false);
                        return new RetrieveResult<UserClientSecretInfo>(secretInfo);
                    }

                    finally
                    {
                        if (verifyResponse != null)
                        {
                            await verifyResponse.FinishAsync(cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        public async Task<UserClientSecretInfo> VerifyExternalToken(ClientAuthenticationScope keyScope, ILogger logger, string externalToken, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            logger.Log("Verifying adhoc credentials");
            if (string.IsNullOrEmpty(externalToken))
            {
                logger.Log("External token is null!", LogLevel.Err);
                throw new ArgumentNullException(nameof(externalToken));
            }

            UserClientSecretInfo clientCreatedCredentials = JsonConvert.DeserializeObject<UserClientSecretInfo>(externalToken);
            
            logger.Log("Generating private key");
            ClientKeyIdentifier clientKey = new ClientKeyIdentifier(keyScope, userId: clientCreatedCredentials.UserId, clientId: clientCreatedCredentials.ClientId);
            PrivateKey newPrivateKey = _rsaDelegates.GenerateRSAKey(_keySize);
            clientCreatedCredentials.PrivateKey = newPrivateKey;
            clientCreatedCredentials.AuthProvider = ProviderName;
            clientCreatedCredentials.SaltValue = CryptographyHelpers.GenerateRandomToken(newPrivateKey.N, 128);

            // Does this adhoc user already exist? If so, fail out
            //PrivateKeyVaultEntry existingEntry = await _privateKeyVault.GetUserInfoById(clientKey);
            //if (existingEntry != null)
            //{
            //    throw new InvalidOperationException("Cannot create adhoc credentials because the user ID already exists");
            //}

            // There's no need to even touch the private key vault
            //PrivateKeyVaultEntry newKeyVaultEntry = new PrivateKeyVaultEntry();
            //newKeyVaultEntry.LastLoginTime = _realTime.Time;
            //newKeyVaultEntry.VaultEntry = clientCreatedCredentials;
            //await _privateKeyVault.UpdateLoggedInUserInfo(newKeyVaultEntry); // This will overwrite any existing private key already generated, which should "theoretically" not cause any problems

            logger.Log("Updating public key");
            // Also update the public key database
            ServerSideAuthenticationState publicKeyState = new ServerSideAuthenticationState();
            publicKeyState.ClientInfo = new ClientIdentifier(clientCreatedCredentials.UserId, clientCreatedCredentials.UserFullName, clientCreatedCredentials.ClientId, clientCreatedCredentials.ClientName);
            publicKeyState.KeyScope = keyScope;
            publicKeyState.PubKey = newPrivateKey.GetPublicKey();
            publicKeyState.SaltValue = clientCreatedCredentials.SaltValue;
            publicKeyState.Trusted = false;
            await _publicKeyVault.UpdateClientState(publicKeyState).ConfigureAwait(false);
            
            return clientCreatedCredentials;
        }

        public Task HandleOAuthCallback(HttpRequest callbackRequest, ILogger logger, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new InvalidOperationException("This is not an OAuth-enabled provider");
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
            }
        }
    }
}
