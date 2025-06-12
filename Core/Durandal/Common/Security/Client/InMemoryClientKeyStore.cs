using Durandal.API;
using Durandal.Common.Logger;
using Durandal.Common.File;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Security.Login;
using Durandal.Common.Client;

namespace Durandal.Common.Security.Client
{
    public class InMemoryClientKeyStore : IClientSideKeyStore
    {
        private readonly IDictionary<ClientKeyIdentifier, UserClientSecretInfo> _identities;

        public InMemoryClientKeyStore()
        {
            _identities = new Dictionary<ClientKeyIdentifier, UserClientSecretInfo>();
        }

        public Task<bool> StoreIdentity(UserClientSecretInfo identity)
        {
            ClientKeyIdentifier key = identity.GetKeyId();
            _identities[key] = identity;
            return Task.FromResult(true);
        }

        public Task<UserClientSecretInfo> LoadIdentity(ClientKeyIdentifier keyId)
        {
            if (_identities.ContainsKey(keyId))
            {
                return Task.FromResult(_identities[keyId]);
            }

            throw new KeyNotFoundException("No private key found for key ID " + keyId.ToString());
        }

        public Task<bool> DeleteIdentity(ClientKeyIdentifier keyId)
        {
            if (_identities.ContainsKey(keyId))
            {
                _identities.Remove(keyId);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<List<UserIdentity>> GetUserIdentities()
        {
            List<UserIdentity> returnVal = new List<UserIdentity>();
            foreach (UserClientSecretInfo identity in _identities.Values)
            {
                if (!string.IsNullOrEmpty(identity.UserId))
                {
                    returnVal.Add(new UserIdentity()
                    {
                        AuthProvider = identity.AuthProvider,
                        Id = identity.UserId,
                        FullName = identity.UserFullName,
                        GivenName = identity.UserGivenName,
                        Surname = identity.UserSurname,
                        Email = identity.UserEmail,
                        IconPng = identity.UserIconPng
                    });
                }
            }

            return Task.FromResult(returnVal);
        }

        public Task<List<ClientIdentity>> GetClientIdentities()
        {
            List<ClientIdentity> returnVal = new List<ClientIdentity>();
            foreach (UserClientSecretInfo identity in _identities.Values)
            {
                if (!string.IsNullOrEmpty(identity.ClientId))
                {
                    returnVal.Add(new ClientIdentity()
                    {
                        AuthProvider = identity.AuthProvider,
                        Id = identity.ClientId,
                        Name = identity.ClientName,
                    });
                    break;
                }
            }
            
            return Task.FromResult(returnVal);
        }
    }
}
