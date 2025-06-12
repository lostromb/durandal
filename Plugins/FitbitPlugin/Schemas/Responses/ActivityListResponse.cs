using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas.Responses
{
    public class ActivityListResponse
    {
        [JsonProperty("activities")]
        public List<FitnessActivity> Activities { get; set; }

        [JsonProperty("pagination")]
        public Pagination Pagination { get; set; }
    }
}
