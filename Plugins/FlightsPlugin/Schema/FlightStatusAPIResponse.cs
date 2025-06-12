using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Flights.Schema
{
    public class FlightStatusAPIResponse
    {
        [JsonProperty("request")]
        public FlightStatusRequest Request { get; set; }

        [JsonProperty("appendix")]
        public Appendix Appendix { get; set; }

        [JsonProperty("flightStatuses")]
        public IList<FlightStatus> FlightStatuses { get; set; }

        [JsonProperty("error")]
        public Error Error { get; set; }
    }
}
