using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class Food
    {
        [JsonProperty("accessLevel")]
        public string AccessLevel { get; set; }

        [JsonProperty("amount")]
        public string Amount { get; set; }

        [JsonProperty("calories")]
        public int Calories { get; set; }

        [JsonProperty("brand")]
        public string Brand { get; set; }

        [JsonProperty("defaultServingSize")]
        public float DefaultServingSize { get; set; }

        [JsonProperty("defaultUnit")]
        public ServingUnit DefaultUnit { get; set; }

        [JsonProperty("unit")]
        public ServingUnit Unit { get; set; }

        [JsonProperty("foodId")]
        public ulong FoodId { get; set; }

        [JsonProperty("locale")]
        public string Locale { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("units")]
        public IList<ulong> Units { get; set; }

        [JsonProperty("nutritionalValues")]
        public NutritionalValue NutritionalValues { get; set; }

        [JsonProperty("servings")]
        public IList<Serving> Servings { get; set; }


//{
//            "accessLevel": "PUBLIC",
//            "amount": 1,
//            "brand": "",
//            "calories": 512,
//            "foodId": 28536,
//            "locale": "en_US",
//            "mealTypeId": 7,
//            "name": "Hamburger, Single Patty",
//            "unit": {
//                "id": 296,
//                "name": "sandwich",
//                "plural": "sandwiches"
//            },
//            "units": [
//                296,
//                226,
//                180,
//                147,
//                389
//            ]
//    }
    }
}
