using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class WeightLog
    {
        [JsonProperty("weight")]
        public double Weight { get; set; }

        [JsonProperty("bmi")]
        public double BMI { get; set; }

        [JsonProperty("dateTime")]
        public DateTimeOffset DateTime { get; set; }

        [JsonProperty("logId")]
        public ulong LogId { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }
    }
}
