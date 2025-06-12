using Durandal.API;
using Durandal.Common.MathExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Security.Client
{
    /// <summary>
    /// Private key and authentication state for a single user, client, or user+client key.
    /// </summary>
    public class ClientSideAuthenticationState
    {
        public ClientSideAuthenticationState(ClientIdentifier clientId)
        {
            ClientId = clientId;
        }

        public ClientIdentifier ClientId { get; set; }
        public PrivateKey Key { get; set; }
        public BigInteger SaltValue { get; set; }
    }
}
