using Durandal.Common.Utils;
using Durandal.Common.IO.Json;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Darksky
{
    /// <summary>
    /// The alerts array contains objects representing the severe weather warnings issued for the requested location by a governmental authority
    /// </summary>
    public class DarkskyWeatherAlert
    {
        /// <summary>
        /// A detailed description of the alert.
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// The UNIX time at which the alert will expire.
        /// </summary>
        [JsonProperty("expires")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset Expires { get; set; }

        /// <summary>
        /// An array of strings representing the names of the regions covered by this weather alert.
        /// </summary>
        [JsonProperty("regions")]
        public IList<string> Regions { get; set; }

        /// <summary>
        /// The severity of the weather alert. Will take one of the following values:
        /// "advisory" (an individual should be aware of potentially severe weather),
        /// "watch" (an individual should prepare for potentially severe weather), or
        /// "warning" (an individual should take immediate action to protect themselves and others from potentially severe weather).
        /// </summary>
        [JsonProperty("severity")]
        public IList<string> Severity { get; set; }

        /// <summary>
        /// The UNIX time at which the alert was issued.
        /// </summary>
        [JsonProperty("time")]
        [JsonConverter(typeof(JsonEpochTimeConverter))]
        public DateTimeOffset Time { get; set; }

        /// <summary>
        /// A brief description of the alert.
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; }

        /// <summary>
        /// An HTTP(S) URI that one may refer to for detailed information about the alert.
        /// </summary>
        [JsonProperty("uri")]
        public Uri Uri { get; set; }
    }
}
