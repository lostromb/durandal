using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security.Login
{
    public interface IPrivateKeyStore
    {
        Task<RetrieveResult<PrivateKeyVaultEntry>> GetUserInfoByStateKey(string stateKey);
        Task<RetrieveResult<PrivateKeyVaultEntry>> GetUserInfoById(ClientKeyIdentifier clientId);
        Task UpdateLoggedInUserInfo(PrivateKeyVaultEntry info);
        Task DeleteLoggedInUserInfo(ClientKeyIdentifier id);
    }
}
