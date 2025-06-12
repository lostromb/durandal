using Durandal.Common.Utils;
using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteFileStreamOpenResult
    {
        public string StreamId { get; set; }
        public bool CanRead { get; set; }
        public bool CanWrite { get; set; }
        public bool CanSeek { get; set; }
        public long? InitialFileLength { get; set; }
    }
}
