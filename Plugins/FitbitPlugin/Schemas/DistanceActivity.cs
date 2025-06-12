using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class DistanceActivity
    {
        [JsonProperty("activity")]
        public string Activity { get; set; }

        [JsonProperty("distance")]
        public double Distance { get; set; }
    }
}
