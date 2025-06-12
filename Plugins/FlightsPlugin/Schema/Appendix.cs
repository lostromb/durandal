using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Flights.Schema
{
    public class Appendix
    {
        [JsonProperty("airports")]
        public IList<Airport> Airports { get; set; }
    }
}
