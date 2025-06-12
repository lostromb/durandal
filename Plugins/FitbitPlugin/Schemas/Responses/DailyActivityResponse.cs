using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas.Responses
{
    public class DailyActivityResponse
    {
        [JsonProperty("activities")]
        public List<FitnessActivity> Activities { get; set; }

        /// <summary>
        /// Goals are included to the response only for today and 21 days in the past.
        /// </summary>
        [JsonProperty("goals")]
        public FitnessGoals Goals { get; set; }

        [JsonProperty("summary")]
        public ActivitySummary Summary { get; set; }
    }
}
