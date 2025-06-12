using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class DataAttribution
    {
        [JsonProperty("providerDisplayName")]
        public string ProviderDisplayName { get; set; }

        [JsonProperty("copyrightMessage")]
        public string CopyrightMessage { get; set; }
    }
}
