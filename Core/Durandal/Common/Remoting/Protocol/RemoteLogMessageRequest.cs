using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteLogMessageRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "LogMessages";

        public override string MethodName => METHOD_NAME;

        public InstrumentationEventList LogEvents { get; set; }
    }
}
