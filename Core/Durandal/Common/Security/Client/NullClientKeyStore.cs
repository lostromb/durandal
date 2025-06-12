using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Security.Login;
using Durandal.Common.Client;

namespace Durandal.Common.Security.Client
{
    public class NullClientKeyStore : IClientSideKeyStore
    {
        public Task<bool> DeleteIdentity(ClientKeyIdentifier keyId)
        {
            return Task.FromResult(true);
        }

        public Task<bool> StoreIdentity(UserClientSecretInfo identity)
        {
            return Task.FromResult(false);
        }

        public Task<UserClientSecretInfo> LoadIdentity(ClientKeyIdentifier keyId)
        {
            throw new KeyNotFoundException("No private key file found for key ID " + keyId.ToString());
        }

        public Task<List<UserIdentity>> GetUserIdentities()
        {
            return Task.FromResult(new List<UserIdentity>());
        }

        public Task<List<ClientIdentity>> GetClientIdentities()
        {
            return Task.FromResult(new List<ClientIdentity>());
        }
    }
}
