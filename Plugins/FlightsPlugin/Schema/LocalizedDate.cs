using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Flights.Schema
{
    public class LocalizedDate
    {
        [JsonProperty("dateLocal")]
        public DateTimeOffset DateLocal { get; set; }

        [JsonProperty("dateUtc")]
        public DateTimeOffset DateUtc { get; set; }
    }
}
