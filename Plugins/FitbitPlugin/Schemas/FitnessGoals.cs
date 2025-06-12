using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class FitnessGoals
    {
        [JsonProperty("activeMinutes")]
        public int? ActiveMinutes { get; set; }

        /// <summary>
        /// Calorie burn goal (caloriesOut) represents either dynamic daily target from the premium trainer plan or manual calorie burn goal. 
        /// </summary>
        [JsonProperty("caloriesOut")]
        public int? CaloriesOut { get; set; }

        [JsonProperty("distance")]
        public float? Distance { get; set; }

        [JsonProperty("steps")]
        public int? Steps { get; set; }

        [JsonProperty("floors")]
        public int? Floors { get; set; }
    }
}
