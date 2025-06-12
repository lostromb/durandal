using Durandal.API;
using Durandal.Common.Security;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT
{
    public class FunctionalTestIdentity
    {
        public string UserId { get; set; }
        public string ClientId { get; set; }
        public ClientAuthenticationScope AuthScope { get; set; }
        public PrivateKey Key { get; set; }
        public HashSet<string> Features { get; set; }
    }
}
