using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class ComputationResult
    {
        [JsonProperty("expression")]
        public string Expression { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }
}