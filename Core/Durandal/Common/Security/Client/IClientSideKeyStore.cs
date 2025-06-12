using Durandal.Common.Client;
using Durandal.Common.Security.Login;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security.Client
{
    public interface IClientSideKeyStore
    {
        /// <summary>
        /// Saves the given private key to persistent client storage (e.g. cert store)
        /// </summary>
        /// <param name="identity"></param>
        /// <returns></returns>
        Task<bool> StoreIdentity(UserClientSecretInfo identity);

        /// <summary>
        /// Loads the given private key from persistent client storage (e.g. cert store). If key does not exist, this will throw.
        /// </summary>
        /// <param name="keyId"></param>
        /// <returns></returns>
        Task<UserClientSecretInfo> LoadIdentity(ClientKeyIdentifier keyId);

        Task<bool> DeleteIdentity(ClientKeyIdentifier keyId);

        Task<List<UserIdentity>> GetUserIdentities();

        Task<List<ClientIdentity>> GetClientIdentities();
    }
}
