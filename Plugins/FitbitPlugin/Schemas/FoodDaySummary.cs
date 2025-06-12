using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class FoodDaySummary
    {
        [JsonProperty("date")]
        public string Date { get; set; }

        [JsonProperty("summary")]
        public FoodSummary Summary { get; set; }
    }
}
