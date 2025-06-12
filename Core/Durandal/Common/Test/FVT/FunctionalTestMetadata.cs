using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT
{
    public class FunctionalTestMetadata
    {
        [JsonProperty("TestName")]
        public string TestName { get; set; }

        [JsonProperty("Author")]
        public string Author { get; set; }

        [JsonProperty("SuggestedTestInterval")]
        [JsonConverter(typeof(JsonTimeSpanStringConverter))]
        public TimeSpan? SuggestedTestInterval { get; set; }

        // TestSuiteName
        // PassRateThreshold
        // LatencyThresholdPerTurn
    }
}
