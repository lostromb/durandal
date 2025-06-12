using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas.Responses
{
    public class FoodLogGetResponse
    {
        [JsonProperty("foods")]
        public List<Food> Foods { get; set; }

        [JsonProperty("summary")]
        public FoodSummary Summary { get; set; }

        [JsonProperty("goals")]
        public FoodGoals Goals { get; set; }
    }
}

//{
//    "foods": [
//    {
//        "isFavorite": false,
//        "logDate": "2018-04-23",
//        "logId": 14188884063,
//        "loggedFood": {
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
//        },
//        "nutritionalValues": {
//            "calories": 512,
//            "carbs": 40,
//            "fat": 27.36,
//            "fiber": 0,
//            "protein": 25.83,
//            "sodium": 824.04
//        }
//    }
//    ],
//    "summary": {
//        "calories": 512,
//        "carbs": 40,
//        "fat": 27.36,
//        "fiber": 0,
//        "protein": 25.83,
//        "sodium": 824.04,
//        "water": 0
//    },
//    "goals":{
//        "calories": 2286
//    }
//}

