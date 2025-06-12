using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class FitbitUser
    {
        [JsonProperty("age")]
        public int Age { get; set; }

        [JsonProperty("avatar")]
        public Uri Avatar { get; set; }

        [JsonProperty("avatar150")]
        public Uri Avatar150 { get; set; }

        [JsonProperty("avatar640")]
        public Uri Avatar640 { get; set; }

        [JsonProperty("averageDailySteps")]
        public int AverageDailySteps { get; set; }

        [JsonProperty("clockTimeDisplayFormat")]
        public string ClockTimeDisplayFormat { get; set; } //"12hour"

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("dateOfBirth")]
        public string DateOfBirth { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("displayNameSetting")]
        public string DisplayNameSetting { get; set; } //"name"

        [JsonProperty("distanceUnit")]
        public string DistanceUnit { get; set; } //"METRIC" - this can be a string enum

        [JsonProperty("encodedId")]
        public string EncodedId { get; set; }

        [JsonProperty("firstName")]
        public string FirstName { get; set; }

        [JsonProperty("locale")]
        public string Locale { get; set; } //"en_US"

        [JsonProperty("lastName")]
        public string LastName { get; set; }

        [JsonProperty("heightUnit")]
        public string HeightUnit { get; set; } //"METRIC"

        [JsonProperty("gender")]
        public string Gender { get; set; } //"MALE"

        [JsonProperty("height")]
        public double Height { get; set; }

        [JsonProperty("offsetFromUTCMillis")]
        public long OffsetFromUTCMillis { get; set; }

        [JsonProperty("startDayOfWeek")]
        public string StartDayOfWeek { get; set; }

        [JsonProperty("waterUnit")]
        public string WaterUnit { get; set; }

        [JsonProperty("weight")]
        public float Weight { get; set; }

        [JsonProperty("weightUnit")]
        public string WeightUnit { get; set; } //"METRIC"
    
    }
}
