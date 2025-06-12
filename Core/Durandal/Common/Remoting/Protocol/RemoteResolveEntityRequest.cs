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
    public class RemoteResolveEntityRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "ResolveEntity";

        public override string MethodName => METHOD_NAME;

        public LexicalString Input { get; set; }

        public List<LexicalNamedEntity> Possibilities { get; set; }

        public string Locale { get; set; }
    }
}
