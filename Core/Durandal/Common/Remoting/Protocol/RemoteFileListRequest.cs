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
    public class RemoteFileListRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "FileList";

        public override string MethodName => METHOD_NAME;

        /// <summary>
        /// The file path to enumerate
        /// </summary>
        public string SourcePath { get; set; }

        /// <summary>
        /// If listing directories, set to true. If listing files, set to false
        /// </summary>
        public bool ListDirectories { get; set; }
    }
}
