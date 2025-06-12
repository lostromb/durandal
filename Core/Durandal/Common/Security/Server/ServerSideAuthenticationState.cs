using Durandal.API;
using Durandal.Common.MathExt;
using System;

namespace Durandal.Common.Security.Server
{
    /// <summary>
    /// A structure for a server to store information about each client's authentication information
    /// </summary>
    public class ServerSideAuthenticationState
    {
        public ClientIdentifier ClientInfo;
        public ClientAuthenticationScope KeyScope;
        public PublicKey PubKey;
        public BigInteger SaltValue;
        public bool Trusted;
    }
}
