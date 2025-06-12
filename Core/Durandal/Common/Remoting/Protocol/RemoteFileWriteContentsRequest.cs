using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.File;
using Durandal.Common.Logger;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteFileWriteContentsRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "FileWriteContents";

        public override string MethodName => METHOD_NAME;

        public string FilePath { get; set; }
        public ArraySegment<byte> NewContents { get; set; }
    }
}
