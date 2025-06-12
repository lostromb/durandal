using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteHttpRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "HttpRequest";

        public override string MethodName => METHOD_NAME;

        public string TargetHost { get; set; }
        public int TargetPort { get; set; }
        public bool UseSSL { get; set; }
        public ArraySegment<byte> WireRequest { get; set; }
    }
}
