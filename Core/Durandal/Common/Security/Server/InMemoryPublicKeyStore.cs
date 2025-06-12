using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security.Server
{
    public class InMemoryPublicKeyStore : IPublicKeyStore
    {
        private readonly IDictionary<ClientKeyIdentifier, ServerSideAuthenticationState> _storage = new Dictionary<ClientKeyIdentifier, ServerSideAuthenticationState>();
        private bool _alwaysTrust;

        public InMemoryPublicKeyStore(bool alwaysTrust = false)
        {
            _alwaysTrust = alwaysTrust;
        }

        public Task<RetrieveResult<ServerSideAuthenticationState>> GetClientState(ClientKeyIdentifier keyId)
        {
            if (_storage.ContainsKey(keyId))
            {
                return Task.FromResult(new RetrieveResult<ServerSideAuthenticationState>(_storage[keyId]));
            }
            else
            {
                return Task.FromResult(new RetrieveResult<ServerSideAuthenticationState>());
            }
        }

        public Task<bool> UpdateClientState(ServerSideAuthenticationState client)
        {
            ClientKeyIdentifier keyId = client.ClientInfo.GetKeyIdentifier(client.KeyScope);
            if (_storage.ContainsKey(keyId))
            {
                _storage.Remove(keyId);
            }

            // Automatically promote clients if autotrust is on (for test cases, normally)
            if (_alwaysTrust)
            {
                client.Trusted = client.SaltValue != null;
            }

            _storage[keyId] = client;
            return Task.FromResult(true);
        }

        public Task DeleteClientState(ClientKeyIdentifier keyId)
        {
            if (_storage.ContainsKey(keyId))
            {
                _storage.Remove(keyId);
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        /// <summary>
        /// Mostly for unit tests
        /// </summary>
        public void ClearAllClients()
        {
            _storage.Clear();
        }

        /// <summary>
        /// Also used for unit tests only
        /// </summary>
        /// <param name="keyId"></param>
        /// <returns></returns>
        public bool PromoteClient(ClientKeyIdentifier keyId)
        {
            if (_storage.ContainsKey(keyId))
            {
                _storage[keyId].Trusted = true;
                return true;
            }

            return false;
        }
    }
}
