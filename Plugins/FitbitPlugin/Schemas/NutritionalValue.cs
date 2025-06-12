using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class NutritionalValue
    {
        [JsonProperty("biotin")]
        public float Biotin { get; set; }

        [JsonProperty("calcium")]
        public float Calcium { get; set; }

        [JsonProperty("calories")]
        public float Calories { get; set; }

        [JsonProperty("caloriesFromFat")]
        public float CaloriesFromFat { get; set; }

        [JsonProperty("cholesterol")]
        public float Cholesterol { get; set; }

        [JsonProperty("copper")]
        public float Copper { get; set; }

        [JsonProperty("dietaryFiber")]
        public float DietaryFiber { get; set; }

        [JsonProperty("folicAcid")]
        public float FolicAcid { get; set; }

        [JsonProperty("iodine")]
        public float Iodine { get; set; }

        [JsonProperty("iron")]
        public float Iron { get; set; }

        [JsonProperty("magnesium")]
        public float Magnesium { get; set; }

        [JsonProperty("niacin")]
        public float Niacin { get; set; }

        [JsonProperty("pantothenicAcid")]
        public float PantothenicAcid { get; set; }

        [JsonProperty("phosphorus")]
        public float Phosphorus { get; set; }

        [JsonProperty("potassium")]
        public float Potassium { get; set; }

        [JsonProperty("protein")]
        public float Protein { get; set; }

        [JsonProperty("riboflavin")]
        public float Riboflavin { get; set; }

        [JsonProperty("saturatedFat")]
        public float SaturatedFat { get; set; }

        [JsonProperty("sodium")]
        public float Sodium { get; set; }

        [JsonProperty("sugars")]
        public float Sugars { get; set; }

        [JsonProperty("thiamin")]
        public float Thiamin { get; set; }

        [JsonProperty("totalCarbohydrate")]
        public float TotalCarbohydrate { get; set; }

        [JsonProperty("totalFat")]
        public float TotalFat { get; set; }

        [JsonProperty("transFat")]
        public float TransFat { get; set; }

        [JsonProperty("zinc")]
        public float Zinc { get; set; }
    }
}
