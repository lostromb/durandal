using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class ActivityDuration
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("minutes")]
        public int Minutes { get; set; }
    }
}
