using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT
{
    public class FunctionalTestTurn
    {
        [JsonProperty("User")]
        public int? User { get; set; }

        [JsonProperty("Client")]
        public int? Client { get; set; }

        [JsonProperty("PreDelay")]
        [JsonConverter(typeof(JsonTimeSpanStringConverter))]
        public TimeSpan? PreDelay { get; set; }

        [JsonProperty("Timeout")]
        [JsonConverter(typeof(JsonTimeSpanStringConverter))]
        public TimeSpan? Timeout { get; set; }

        // Types of inputs:
        // Plain text (dialog request)
        // Simple or advanced speech (dialog request)
        // Direct dialog action (dialog request) - some client actions do this (greet...)
        // Action URI (dialog request) - some client actions do this (weather/refresh)
        // SPA PUT (dictionary in + out) - (nobody uses this today)

        /// <summary>
        /// Polymorphic field; should be a subclass of <see cref="AbstractFunctionalTestInput" />.
        /// </summary>
        [JsonProperty("Input")]
        public JObject Input { get; set; }

        /// <summary>
        /// Polymorphic field; should be a subclass of <see cref="AbstractFunctionalTestValidator" />.
        /// </summary>
        [JsonProperty("Validations")]
        public List<JObject> Validations { get; set; }
    }
}
