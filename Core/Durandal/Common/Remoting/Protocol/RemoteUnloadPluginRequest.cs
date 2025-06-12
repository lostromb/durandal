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
    public class RemoteUnloadPluginRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "UnloadPlugin";

        public override string MethodName => METHOD_NAME;

        public PluginStrongName PluginId { get; set; }
    }
}
