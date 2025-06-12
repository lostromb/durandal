using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class FitnessActivity
    {
        [JsonProperty("activityName")]
        public string ActivityName { get; set; }

        [JsonProperty("activityTypeId")]
        public ulong ActivityTypeId { get; set; }

        [JsonProperty("activityLevel")]
        public List<ActivityDuration> ActivityLevel { get; set; }

        [JsonProperty("calories")]
        public int Calories { get; set; }

        [JsonProperty("distance")]
        public double Distance { get; set; }

        [JsonProperty("distanceUnit")]
        public string DistanceUnit { get; set; }

        [JsonProperty("logId")]
        public ulong LogId { get; set; }

        [JsonProperty("logType")]
        public string LogType { get; set; }

        [JsonProperty("pace")]
        public double Pace { get; set; }

        [JsonProperty("speed")]
        public double Speed { get; set; }

        /// <summary>
        /// The steps field in activity log entries included only for activities that have steps (e.g. "Walking", "Running")
        /// </summary>
        [JsonProperty("steps")]
        public int Steps { get; set; }

        [JsonProperty("startTime")]
        public DateTimeOffset? StartTime { get; set; }

        [JsonProperty("duration")]
        public long Duration { get; set; }

        [JsonProperty("activeDuration")]
        public long ActiveDuration { get; set; }

        [JsonProperty("originalDuration")]
        public long OriginalDuration { get; set; }

        [JsonProperty("originalStartTime")]
        public DateTimeOffset? OriginalStartTime { get; set; }

        [JsonProperty("lastModified")]
        public DateTimeOffset? LastModified { get; set; }

        [JsonProperty("manualValuesSpecified")]
        public Dictionary<string, bool> ManualValuesSpecified { get; set; }
    }

    //{
    //        "activeDuration": 7200000,
    //        "activityLevel": [
    //            {
    //                "minutes": 0,
    //                "name": "sedentary"
    //            },
    //            {
    //                "minutes": 120,
    //                "name": "lightly"
    //            },
    //            {
    //                "minutes": 0,
    //                "name": "fairly"
    //            },
    //            {
    //                "minutes": 0,
    //                "name": "very"
    //            }
    //        ],
    //        "activityName": "Walk",
    //        "activityTypeId": 90013,
    //        "calories": 74,
    //        "distance": 8.04672,
    //        "distanceUnit": "Kilometer",
    //        "duration": 7200000,
    //        "lastModified": "2018-03-18T08:14:57.000Z",
    //        "logId": 12976430371,
    //        "logType": "manual",
    //        "manualValuesSpecified": {
    //            "calories": false,
    //            "distance": true,
    //            "steps": false
    //        },
    //        "originalDuration": 7200000,
    //        "originalStartTime": "2018-03-18T01:00:00.000-07:00",
    //        "pace": 894.7745168217608,
    //        "speed": 4.02336,
    //        "startTime": "2018-03-18T01:00:00.000-07:00",
    //        "steps": 10601
    //    }
}
