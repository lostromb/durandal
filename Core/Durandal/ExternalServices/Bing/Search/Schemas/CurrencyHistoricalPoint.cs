using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class CurrencyHistoricalPoint
    {
        [JsonProperty("toValue")]
        public double ToValue { get; set; }

        [JsonProperty("date")]
        public DateTimeOffset Date { get; set; }
    }
}
