using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT.Inputs
{
    /// <summary>
    /// Contains optional client context parameters which can be set in a dialog input.
    /// </summary>
    public class InputClientContext
    {
        [JsonProperty("Latitude")]
        public double? Latitude { get; set; }

        [JsonProperty("Longitude")]
        public double? Longitude { get; set; }

        [JsonProperty("LocationAccuracy")]
        public double? LocationAccuracy { get; set; }

        [JsonProperty("TimeZone")]
        public string TimeZone { get; set; }

        [JsonProperty("SupportedClientActions")]
        public ISet<string> SupportedClientActions { get; set; }
    }
}
