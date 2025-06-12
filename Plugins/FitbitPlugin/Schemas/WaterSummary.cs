using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class WaterSummary
    {
        [JsonProperty("water")]
        public float Water;
    }
}



//{
//    "summary":{
//       "water":800;
//    },
//    "water":[
//        {"amount":500,"logId":950},
//        {"amount":200,"logId":951},
//        {"amount":100,"logId":952}
//    ]
//}

