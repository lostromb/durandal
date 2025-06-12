using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security.Server
{
    public interface IPublicKeyStore
    {
        /// <summary>
        /// Updates a specific client's information (or one row of the database)
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        Task<bool> UpdateClientState(ServerSideAuthenticationState client);

        /// <summary>
        /// Gets the information for a specific user/client combination
        /// </summary>
        /// <returns></returns>
        Task<RetrieveResult<ServerSideAuthenticationState>> GetClientState(ClientKeyIdentifier keyId);

        Task DeleteClientState(ClientKeyIdentifier keyId);
    }
}
