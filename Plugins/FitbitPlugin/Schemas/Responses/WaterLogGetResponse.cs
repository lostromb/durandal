using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas.Responses
{
    public class WaterLogGetResponse
    {
        //[JsonProperty("foods")]
        //public List<Food> Foods { get; set; }

        [JsonProperty("summary")]
        public WaterSummary Summary { get; set; }

        //[JsonProperty("goals")]
        //public FoodGoals Goals { get; set; }
    }
}

