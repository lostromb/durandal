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
    /// <summary>
    /// This signal should only be recognized on debug builds of code; it's intended only for unit testing
    /// </summary>
    public class RemoteCrashContainerRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "CrashContainer";

        public override string MethodName => METHOD_NAME;
    }
}
