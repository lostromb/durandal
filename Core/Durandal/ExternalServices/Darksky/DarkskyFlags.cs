using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Darksky
{
    /// <summary>
    /// The flags object contains various metadata information related to the request.
    /// </summary>
    public class DarkskyFlags
    {
        /// <summary>
        /// The presence of this property indicates that the Dark Sky data source supports the given location, but a temporary error (such as a radar station being down for maintenance) has made the data unavailable.
        /// </summary>
        [JsonProperty("darksky-unavailable")]
        public double DarkskyUnavailable { get; set; }

        /// <summary>
        /// The distance to the nearest weather station that contributed data to this response.
        /// Note, however, that many other stations may have also been used; this value is primarily for debugging purposes.
        /// This property's value is in miles (if US units are selected) or kilometers (if SI units are selected).
        /// </summary>
        [JsonProperty("nearest-station")]
        public double NearestStationDistance { get; set; }

        /// <summary>
        /// This property contains an array of IDs for each data source utilized in servicing this request.
        /// </summary>
        [JsonProperty("sources")]
        public IList<string> Sources { get; set; }

        /// <summary>
        /// Indicates the units which were used for the data in this request.
        /// </summary>
        [JsonProperty("units")]
        public string Units { get; set; }
    }
}
