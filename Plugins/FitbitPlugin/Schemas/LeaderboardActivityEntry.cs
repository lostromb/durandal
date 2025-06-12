using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class LeaderboardActivityEntry
    {
        [JsonProperty("steps")]
        public int Steps { get; set; }
    }
}
