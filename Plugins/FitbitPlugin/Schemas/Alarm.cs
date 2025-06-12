using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class Alarm
    {
        [JsonProperty("activity")]
        public ulong AlarmId { get; set; }

        [JsonProperty("deleted")]
        public bool Deleted { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("recurring")]
        public bool Recurring { get; set; }

        [JsonProperty("snoozeCount")]
        public int SnoozeCount { get; set; }

        [JsonProperty("snoozeLength")]
        public int SnoozeLength { get; set; }

        [JsonProperty("syncedToDevice")]
        public bool SyncedToDevice { get; set; }

        [JsonProperty("time")]
        public string Time { get; set; }

        [JsonProperty("vibe")]
        public string Vibe { get; set; }

        [JsonProperty("weekDays")]
        public List<string> WeekDays { get; set; }
    }
}
