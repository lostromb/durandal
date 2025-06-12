using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class WaterLog
    {
        [JsonProperty("logId")]
        public ulong LogId { get; set; }

        [JsonProperty("amount")]
        public float Amount { get; set; }
    }
}
