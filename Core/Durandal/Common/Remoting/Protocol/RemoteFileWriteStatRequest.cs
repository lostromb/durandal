using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteFileWriteStatRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "FileWriteStat";

        public override string MethodName => METHOD_NAME;

        public string TargetPath { get; set; }

        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? NewCreationTime { get; set; }

        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? NewModificationTime { get; set; }
    }
}
