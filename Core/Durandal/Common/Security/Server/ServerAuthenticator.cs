using System;
using System.Collections.Generic;

namespace Durandal.Common.Security.Server
{
    using Durandal.Common.Logger;
    using Durandal.Common.File;
    using System.Threading.Tasks;
    using Durandal.Common.Utils;
    using Durandal.Common.Time;
    using Durandal.API;
    using Durandal.Common.Tasks;
    using Durandal.Common.MathExt;
    using System.Diagnostics;
    using Durandal.Common.Instrumentation;

    public class ServerAuthenticator
    {
        private readonly ILogger _logger;
        private IPublicKeyStore _publicKeyStorage;
        private IRSADelegates _rsaImpl;

        /// <summary>
        /// Creates a new ServerAuthenticator
        /// </summary>
        /// <param name="logger">A logger for global messages</param>
        /// <param name="publicKeyStorage">The mechanism for storing user + client public keys</param>
        /// <param name="rsaImpl">An implementation of the RSA algorithm</param>
        public ServerAuthenticator(
            ILogger logger,
            IPublicKeyStore publicKeyStorage,
            IRSADelegates rsaImpl)
        {
            _logger = logger;
            _publicKeyStorage = publicKeyStorage;
            _rsaImpl = rsaImpl;
        }

        //public async Task<bool> RegisterNewClient(ClientIdentifier clientInfo, ClientAuthenticationScope keyScope, PublicKey key)
        //{
        //    if (clientInfo == null)
        //    {
        //        throw new ArgumentNullException("Client info is null");
        //    }

        //    if (keyScope == ClientAuthenticationScope.None)
        //    {
        //        throw new ArgumentNullException("Key scope is null");
        //    }

        //    if (key == null)
        //    {
        //        throw new ArgumentNullException("Public key is null");
        //    }
            
        //    clientInfo = SanitizeClientInfo(clientInfo, keyScope);

        //    // Reuse existing client information if it is known. In this case, only update the client name + user name (none of the IDs)
        //    ServerSideAuthenticationState newInformation = await _clientStateStorage.GetClientState(clientInfo.GetKeyIdentifier(keyScope));

        //    if (newInformation == null)
        //    {
        //        newInformation = new ServerSideAuthenticationState();
        //    }

        //    newInformation.ClientInfo = clientInfo;
        //    newInformation.PubKey = key;
        //    newInformation.KeyScope = keyScope;

        //    // Remove fields that are not relevant to the scope
        //    if (!keyScope.HasFlag(ClientAuthenticationScope.Client))
        //    {
        //        newInformation.ClientInfo.ClientId = null;
        //        newInformation.ClientInfo.ClientName = null;
        //    }

        //    if (!keyScope.HasFlag(ClientAuthenticationScope.User))
        //    {
        //        newInformation.ClientInfo.UserId = null;
        //        newInformation.ClientInfo.UserName = null;
        //    }

        //    await _clientStateStorage.UpdateClientState(newInformation);
        //    return true;
        //}

        //public async Task<bool> IsClientVerified(ClientKeyIdentifier keyId)
        //{
        //    ValidateKeyId(keyId);
        //    keyId = SanitizeKeyId(keyId);

        //    ServerSideAuthenticationState info = await _clientStateStorage.GetClientState(keyId);
        //    return info != null && info.SaltValue != null && info.Trusted;
        //}

        //public async Task<BigInteger> GenerateChallengeToken(ClientKeyIdentifier keyId)
        //{
        //    ValidateKeyId(keyId);
        //    keyId = SanitizeKeyId(keyId);

        //    ServerSideAuthenticationState info = await _clientStateStorage.GetClientState(keyId);
        //    if (info == null)
        //    {
        //        return null;
        //    }

        //    // A challenge will always invalidate the current shared secret
        //    info.SharedSecretToken = null;
        //    info.LastChallengeToken = CryptographyHelpers.GenerateRandomToken(info.PubKey.N);
        //    await _clientStateStorage.UpdateClientState(info);

        //    return _rsaImpl.Decrypt(info.LastChallengeToken, info.PubKey);
        //}

        //public async Task<bool> VerifyChallengeToken(ClientKeyIdentifier keyId, BigInteger challengeAnswer)
        //{
        //    ValidateKeyId(keyId);
        //    keyId = SanitizeKeyId(keyId);

        //    if (challengeAnswer == null)
        //    {
        //        return false;
        //    }

        //    ServerSideAuthenticationState info = await _clientStateStorage.GetClientState(keyId);
        //    if (info == null || info.LastChallengeToken == null)
        //    {
        //        return false;
        //    }

        //    return challengeAnswer.Equals(info.LastChallengeToken);
        //}

        //public async Task<BigInteger> GenerateSharedSecret(ClientKeyIdentifier keyId)
        //{
        //    ValidateKeyId(keyId);
        //    keyId = SanitizeKeyId(keyId);

        //    ServerSideAuthenticationState info = await _clientStateStorage.GetClientState(keyId);
        //    if (info == null)
        //    {
        //        return null;
        //    }

        //    info.SharedSecretToken = CryptographyHelpers.GenerateRandomToken(info.PubKey.N);
        //    info.SharedSecretExpireTime = _realTime.Time.UtcDateTime.Add(_sharedSecretLifetime);
        //    await _clientStateStorage.UpdateClientState(info);

        //    return _rsaImpl.Decrypt(info.SharedSecretToken, info.PubKey);
        //}

        //public async Task<BigInteger> RetrieveSharedSecret(ClientKeyIdentifier keyId)
        //{
        //    ServerSideAuthenticationState info = await _clientStateStorage.GetClientState(keyId);

        //    // If token is expired, return null so we will re-challenge the client
        //    if (info == null || info.SharedSecretExpireTime < _realTime.Time.UtcDateTime)
        //    {
        //        return null;
        //    }

        //    return _rsaImpl.Decrypt(info.SharedSecretToken, info.PubKey);
        //}

        public async Task<AuthLevel> VerifyRequestToken(ClientKeyIdentifier keyId, RequestToken token, ILogger queryLogger, IRealTimeProvider realTime)
        {
            if (token == null || token.TokenBlue == null || token.TokenRed == null)
            {
                queryLogger.Log("Client sent null token to server authenticator", LogLevel.Wrn);
                return AuthLevel.Unknown;
            }

            ValidateKeyId(keyId);
            keyId = SanitizeKeyId(keyId);
            
            Stopwatch authVerifyTimer = Stopwatch.StartNew();
            RetrieveResult<ServerSideAuthenticationState> publicKeyRetrieveResult = await _publicKeyStorage.GetClientState(keyId).ConfigureAwait(false);
            authVerifyTimer.Stop();
            // FIXME if client sends multiple tokens, this same value could be instrumented multiple times per trace
            queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Store_PublicKeyRead, authVerifyTimer), LogLevel.Ins);
            authVerifyTimer.Restart();

            if (!publicKeyRetrieveResult.Success || publicKeyRetrieveResult.Result == null)
            {
                return AuthLevel.Unknown;
            }

            ServerSideAuthenticationState info = publicKeyRetrieveResult.Result;
            bool isVerified = info.Trusted;
            BigInteger blue = new BigInteger(token.TokenBlue);
            BigInteger serverCombinedToken = info.SaltValue ^ token.TokenRed;
            BigInteger decryptedToken = _rsaImpl.Decrypt(blue, info.PubKey);

            authVerifyTimer.Stop();
            queryLogger.Log(CommonInstrumentation.GenerateLatencyEntry(CommonInstrumentation.Key_Latency_Dialog_VerifyAuthToken, authVerifyTimer), LogLevel.Ins);

            // Verify the client's signature
            if (!serverCombinedToken.Equals(decryptedToken))
            {
                _logger.Log("Could not validate client: Invalid RSA signature", LogLevel.Err);
                return AuthLevel.Unauthorized;
            }

            // Verify that the expire time has not passed
            DateTimeOffset requestExpireTime = CryptographyHelpers.ParseRequestExpireTime(token.TokenRed);
            if (requestExpireTime < realTime.Time)
            {
                _logger.Log("Could not validate client: Request token has expired (Token expire time is " + requestExpireTime.ToString("yyyy-MM-ddTHH:mm:ss") + " server time is " + realTime.Time.ToString("yyyy-MM-ddTHH:mm:ss") + ")", LogLevel.Err);
                return AuthLevel.RequestExpired;
            }

            // RSA all checks out. Now, are they verified or not?
            if (isVerified)
                return AuthLevel.Authorized;
            else
                return AuthLevel.Unverified;
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
