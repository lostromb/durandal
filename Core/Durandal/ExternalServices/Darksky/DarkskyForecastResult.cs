using Durandal.Common.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Darksky
{
    public class DarkskyForecastResult
    {
        /// <summary>
        /// The requested latitude.
        /// </summary>
        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        /// <summary>
        /// The requested longitude.
        /// </summary>
        [JsonProperty("longitude")]
        public double Longitude { get; set; }

        /// <summary>
        /// The IANA timezone name for the requested location. This is used for text summaries and for determining when hourly and daily data block objects begin.
        /// </summary>
        [JsonProperty("timezone")]
        public string Timezone { get; set; }

        /// <summary>
        /// A data point containing the current weather conditions at the requested location.
        /// </summary>
        [JsonProperty("currently")]
        public DarkskyWeatherDataPoint Currently { get; set; }
        
        /// <summary>
        ///  data block containing the weather conditions minute-by-minute for the next hour.
        /// </summary>
        [JsonProperty("minutely")]
        public DarkskyDataBlock Minutely { get; set; }

        /// <summary>
        /// A data block containing the weather conditions hour-by-hour for the next two days.
        /// </summary>
        [JsonProperty("hourly")]
        public DarkskyDataBlock Hourly { get; set; }

        /// <summary>
        /// A data block containing the weather conditions day-by-day for the next week.
        /// </summary>
        [JsonProperty("daily")]
        public DarkskyDataBlock Daily { get; set; }

        /// <summary>
        /// An alerts array, which, if present, contains any severe weather alerts pertinent to the requested location.
        /// </summary>
        [JsonProperty("alerts")]
        public IList<DarkskyWeatherAlert> Alerts { get; set; }

        /// <summary>
        /// A flags object containing miscellaneous metadata about the request.
        /// </summary>
        [JsonProperty("flags")]
        public DarkskyFlags Flags { get; set; }
    }
}
