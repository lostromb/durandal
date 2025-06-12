using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT
{
    public class FunctionalTest
    {
        [JsonProperty("Metadata")]
        public FunctionalTestMetadata Metadata { get; set; }

        [JsonProperty("Users")]
        public List<FunctionalTestFeatureConstraints> Users { get; set; }

        [JsonProperty("Clients")]
        public List<FunctionalTestFeatureConstraints> Clients { get; set; }

        [JsonProperty("Turns")]
        public List<FunctionalTestTurn> Turns { get; set; }
    }
}
