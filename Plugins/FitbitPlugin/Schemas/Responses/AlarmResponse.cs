using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas.Responses
{
    public class AlarmResponse
    {
        [JsonProperty("trackerAlarms")]
        public List<Alarm> TrackerAlarms { get; set; }
    }

//{
//    "trackerAlarms": [
//        {
//            "alarmId": 11426382,
//            "deleted": false,
//            "enabled": true,
//            "recurring": true,
//            "snoozeCount": 3,
//            "snoozeLength": 9,
//            "syncedToDevice": false,
//            "time": "07:15-08:00",
//            "vibe": "DEFAULT",
//            "weekDays": [
//                "MONDAY",
//                "TUESDAY"
//            ]
//}
//    ]
//}
}
