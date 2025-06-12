using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas.Responses
{
    public class FoodLogPostResponse
    {
        [JsonProperty("foodDay")]
        public FoodDaySummary FoodDay { get; set; }

        [JsonProperty("foodLog")]
        public FoodLog FoodLog { get; set; }
    }
}
