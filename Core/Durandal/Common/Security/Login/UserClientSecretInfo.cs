using Durandal.API;
using Durandal.Common.Utils;
using Durandal.Common.IO.Json;
using Durandal.Common.MathExt;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Security.Login
{
    public class UserClientSecretInfo
    {
        // Key info
        public PrivateKey PrivateKey;
        public BigInteger SaltValue;
        public string AuthProvider;

        // User details
        public string UserId;
        public string UserFullName;
        public string UserGivenName;
        public string UserSurname;
        public string UserEmail;

        [JsonConverter(typeof(JsonByteArrayConverter))]
        public byte[] UserIconPng;

        // Client details
        public string ClientId;
        public string ClientName;

        /// <summary>
        /// Gets a key identifier which can identify this identity (.....)
        /// </summary>
        /// <returns></returns>
        public ClientKeyIdentifier GetKeyId()
        {
            ClientAuthenticationScope scope = ClientAuthenticationScope.None;
            string clientId = null;
            string userId = null;
            if (!string.IsNullOrEmpty(UserId))
            {
                scope |= ClientAuthenticationScope.User;
                userId = UserId;
            }
            if (!string.IsNullOrEmpty(ClientId))
            {
                scope |= ClientAuthenticationScope.Client;
                clientId = ClientId;
            }

            return new ClientKeyIdentifier(scope, userId: userId, clientId: clientId);
        }
    }
}
