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
    public class RemoteFileStreamWriteRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "FileStreamWrite";

        public override string MethodName => METHOD_NAME;

        public string StreamId { get; set; }
        public long Position { get; set; }
        public ArraySegment<byte> Data { get; set; }
    }
}
