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
    public class RemoteFileStreamOpenRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "FileStreamOpen";

        public override string MethodName => METHOD_NAME;

        public string FilePath { get; set; }
        public RemoteFileStreamOpenMode OpenMode { get; set; }
        public RemoteFileStreamAccessMode AccessMode { get; set; }
        public RemoteFileStreamShareMode ShareMode { get; set; }
    }
}
