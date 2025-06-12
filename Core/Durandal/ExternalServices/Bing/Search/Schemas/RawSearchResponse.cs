using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class RawSearchResponse
    {
        [JsonProperty("entities")]
        public EntityList Entities { get; set; }
        
        [JsonProperty("places")]
        public EntityList Places { get; set; }
        
        [JsonProperty("facts")]
        public FactCollection Facts { get; set; }
        
        [JsonProperty("computation")]
        public ComputationResult Computation { get; set; }

        [JsonProperty("currency")]
        public CurrencyResult Currency { get; set; }
    }
}
