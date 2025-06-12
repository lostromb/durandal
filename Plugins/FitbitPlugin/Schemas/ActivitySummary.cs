using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class ActivitySummary
    {
        [JsonProperty("activeScore")]
        public double ActiveScore { get; set; }

        /// <summary>
        /// The number of calories burned during the day for periods of time when the user was active above sedentary level.
        /// </summary>
        [JsonProperty("activityCalories")]
        public int ActivityCalories { get; set; }

        /// <summary>
        ///  Only BMR (base metabolic rate) calories. In other words, "resting" calories.
        /// </summary>
        [JsonProperty("caloriesBMR")]
        public int CaloriesBMR { get; set; }

        /// <summary>
        /// The top level time series for calories burned inclusive of BMR, tracked activity, and manually logged activities.
        /// </summary>
        [JsonProperty("caloriesOut")]
        public int CaloriesOut { get; set; }

        [JsonProperty("marginalCalories")]
        public int MarginalCalories { get; set; }

        [JsonProperty("steps")]
        public int Steps { get; set; }

        /// <summary>
        /// Daily summary data and daily goals for elevation (elevation, floors) only included for users with a device with an altimeter.
        /// </summary>
        [JsonProperty("floors")]
        public int Floors { get; set; }

        [JsonProperty("distances")]
        public List<DistanceActivity> Distances { get; set; }

        [JsonProperty("fairlyActiveMinutes")]
        public int FairlyActiveMinutes { get; set; }

        [JsonProperty("lightlyActiveMinutes")]
        public int LightlyActiveMinutes { get; set; }
        
        [JsonProperty("sedentaryMinutes")]
        public int SedentaryMinutes { get; set; }

        [JsonProperty("veryActiveMinutes")]
        public int VeryActiveMinutes { get; set; }
    }
}
