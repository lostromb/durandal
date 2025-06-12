using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class WeightLogInternal
    {
        [JsonProperty("weight")]
        public double Weight { get; set; }

        [JsonProperty("bmi")]
        public double BMI { get; set; }

        [JsonProperty("date")]
        public string Date { get; set; }

        [JsonProperty("logId")]
        public ulong LogId { get; set; }

        [JsonProperty("time")]
        public string Time { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }
    }
}
