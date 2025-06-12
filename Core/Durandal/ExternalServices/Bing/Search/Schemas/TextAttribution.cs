using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class TextAttribution
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }
}
