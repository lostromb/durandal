using Durandal.API;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security.Login
{
    public class InMemoryPrivateKeyStore : IPrivateKeyStore
    {
        private IDictionary<ClientKeyIdentifier, PrivateKeyVaultEntry> _entries = new Dictionary<ClientKeyIdentifier, PrivateKeyVaultEntry>();

        public Task<RetrieveResult<PrivateKeyVaultEntry>> GetUserInfoByStateKey(string stateKey)
        {
            foreach (var entry in _entries.Values)
            {
                if (string.Equals(entry.LoginState, stateKey))
                {
                    return Task.FromResult(new RetrieveResult<PrivateKeyVaultEntry>(entry));
                }
            }

            return Task.FromResult(new RetrieveResult<PrivateKeyVaultEntry>());
        }

        public Task<RetrieveResult<PrivateKeyVaultEntry>> GetUserInfoById(ClientKeyIdentifier clientId)
        {
            if (_entries.ContainsKey(clientId))
            {
                return Task.FromResult(new RetrieveResult<PrivateKeyVaultEntry>(_entries[clientId]));
            }

            return Task.FromResult(new RetrieveResult<PrivateKeyVaultEntry>());
        }

        public Task UpdateLoggedInUserInfo(PrivateKeyVaultEntry info)
        {
            ClientAuthenticationScope scope = ClientAuthenticationScope.None;
            string userId = null;
            string clientId = null;
            if (!string.IsNullOrEmpty(info.VaultEntry.UserId))
            {
                scope |= ClientAuthenticationScope.User;
                userId = info.VaultEntry.UserId;
            }
            if (!string.IsNullOrEmpty(info.VaultEntry.ClientId))
            {
                scope |= ClientAuthenticationScope.Client;
                clientId = info.VaultEntry.ClientId;
            }

            ClientKeyIdentifier id = new ClientKeyIdentifier(scope, userId, clientId);
            if (_entries.ContainsKey(id))
            {
                _entries.Remove(id);
            }

            _entries[id] = info;
            return DurandalTaskExtensions.NoOpTask;
        }
        
        public Task DeleteLoggedInUserInfo(ClientKeyIdentifier id)
        {
            if (_entries.ContainsKey(id))
            {
                _entries.Remove(id);
            }

            return DurandalTaskExtensions.NoOpTask;
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
