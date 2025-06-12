using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security.Server
{
    public class NullPublicKeyStore : IPublicKeyStore
    {
        public Task DeleteClientState(ClientKeyIdentifier keyId)
        {
            return DurandalTaskExtensions.NoOpTask;
        }

        public Task<RetrieveResult<ServerSideAuthenticationState>> GetClientState(ClientKeyIdentifier keyId)
        {
            return Task.FromResult(new RetrieveResult<ServerSideAuthenticationState>());
        }

        public Task<bool> UpdateClientState(ServerSideAuthenticationState client)
        {
            return Task.FromResult(true);
        }
    }
}
