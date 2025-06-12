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
    public class RemoteCrossDomainResponseRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "CrossDomainResponse";

        public override string MethodName => METHOD_NAME;

        public PluginStrongName PluginId { get; set; }

        public CrossDomainContext Context { get; set; }

        public string TraceId { get; set; }

        public int ValidLogLevels { get; set; }

        public InMemoryDataStore SessionStore { get; set; }
    }
}
