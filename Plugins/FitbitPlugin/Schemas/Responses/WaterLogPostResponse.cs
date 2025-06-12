using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas.Responses
{
    public class WaterLogPostResponse
    {
        [JsonProperty("waterLog")]
        public WaterLog WaterLog { get; set; }
    }
}
