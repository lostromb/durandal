using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Darksky
{
    /// <summary>
    /// A data block object represents the various weather phenomena occurring over a period of time.
    /// </summary>
    public class DarkskyDataBlock
    {
        /// <summary>
        /// An array of data points, ordered by time, which together describe the weather conditions at the requested location over time.
        /// </summary>
        [JsonProperty("data")]
        public IList<DarkskyWeatherDataPoint> Data { get; set; }

        /// <summary>
        /// A human-readable summary of this data block.
        /// </summary>
        [JsonProperty("summary")]
        public string Summary { get; set; }

        /// <summary>
        /// A machine-readable text summary of this data block. (May take on the same values as the icon property of data points.)
        /// </summary>
        [JsonProperty("icon")]
        public string Icon { get; set; }
    }
}
