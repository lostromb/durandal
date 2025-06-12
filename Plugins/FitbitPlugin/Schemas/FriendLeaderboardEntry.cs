using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class FriendLeaderboardEntry
    {
        [JsonProperty("average")]
        public LeaderboardActivityEntry Average;

        [JsonProperty("rank")]
        public LeaderboardRankEntry Rank;

        [JsonProperty("summary")]
        public LeaderboardActivityEntry Summary;

        [JsonProperty("lastUpdateTime")]
        public DateTimeOffset? LastUpdateTime { get; set; }

        [JsonProperty("user")]
        public FitbitUser User { get; set; }
    }
}
