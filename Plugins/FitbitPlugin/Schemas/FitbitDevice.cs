using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class FitbitDevice
    {
        [JsonProperty("battery")]
        public BatteryLevel Battery { get; set; }

        [JsonProperty("batteryLevel")]
        public int BatteryLevel { get; set; }

        [JsonProperty("deviceVersion")]
        public string DeviceVersion { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("lastSyncTime")]
        public DateTime? LastSyncTime { get; set; }

        //[JsonProperty("mac")]
        //public string Mac { get; set; }

        //[
        //    {
        //        "battery": "High",
        //        "deviceVersion": "Charge HR",
        //        "id": "27072629",
        //        "lastSyncTime": "2015-07-27T17:01:39.313",
        //        "type": "TRACKER"
        //    },
        //    {
        //        "battery": "High",
        //        "deviceVersion": "Aria",
        //        "id": "Y1PFEJZGGX8QFYTV",
        //        "lastSyncTime": "2015-07-27T07:14:34.000",
        //        "type": "SCALE"
        //    }
        //]

//        [
//    {
//        "battery": "Low",
//        "batteryLevel": 24,
//        "deviceVersion": "Flex",
//        "features": [],
//        "id": "604055163",
//        "lastSyncTime": "2018-06-13T14:28:37.832",
//        "mac": "3D5FAA6AFFF6",
//        "type": "TRACKER"
//    }
//]
    }
}
