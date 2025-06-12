using Durandal.Common.Utils;
using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteFileStat
    {
        public bool Exists { get; set; }

        public bool IsDirectory { get; set; }

        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? LastWriteTime { get; set; }

        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? CreationTime { get; set; }

        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? LastAccessTime { get; set; }
        
        public long Size { get; set; }
    }
}
