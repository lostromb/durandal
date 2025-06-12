using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteGetOAuthTokenRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "GetOAuthToken";

        public override string MethodName => METHOD_NAME;

        public string UserId { get; set; }

        public PluginStrongName PluginId { get; set; }

        public OAuthConfig OAuthConfig { get; set; }
    }
}
