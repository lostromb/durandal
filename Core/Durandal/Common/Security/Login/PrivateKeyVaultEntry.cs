using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Security.Login
{
    public class PrivateKeyVaultEntry
    {
        public DateTimeOffset LastLoginTime;
        // for MSA: this is the random state key used to identify a single login
        // for adhoc: this is not used
        public string LoginState;
        public bool LoginInProgress;
        public UserClientSecretInfo VaultEntry;
    }
}
