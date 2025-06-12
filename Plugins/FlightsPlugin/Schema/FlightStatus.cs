using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Flights.Schema
{
    public class FlightStatus
    {
        [JsonProperty("flightId")]
        public long FlightId { get; set; }

        [JsonProperty("carrierFsCode")]
        public string CarrierFsCode { get; set; }

        [JsonProperty("flightNumber")]
        public int FlightNumber { get; set; }

        [JsonProperty("departureAirportFsCode")]
        public string DepartureAirportFsCode { get; set; }

        [JsonProperty("arrivalAirportFsCode")]
        public string ArrivalAirportFsCode { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("departureDate")]
        public LocalizedDate DepartureDate { get; set; }

        [JsonProperty("arrivalDate")]
        public LocalizedDate ArrivalDate { get; set; }
    }
}
