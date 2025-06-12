using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Logger;
using Durandal.Common.Ontology;
using Durandal.Common.Utils;
using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteFetchPluginViewDataRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "FetchPluginViewData";

        public override string MethodName => METHOD_NAME;

        public PluginStrongName PluginId { get; set; }

        public string FilePath { get; set; }

        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset? IfModifiedSince { get; set; }
    }
}
