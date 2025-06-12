using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit.Schemas
{
    public class Pagination
    {
        [JsonProperty("beforeDate")]
        public string BeforeDate { get; set; }

        [JsonProperty("afterDate")]
        public string AfterDate { get; set; }

        /// <summary>
        /// Max number of items per page
        /// </summary>
        [JsonProperty("limit")]
        public int Limit { get; set; }

        /// <summary>
        /// Absolute item offset for pagination
        /// </summary>
        [JsonProperty("offset")]
        public int Offset { get; set; }

        /// <summary>
        /// URL to fetch the next page, if one exists
        /// </summary>
        [JsonProperty("next")]
        public string Next { get; set; }

        /// <summary>
        /// URL to fetch the previous page, if one exists
        /// </summary>
        [JsonProperty("previous")]
        public string Previous { get; set; }

        /// <summary>
        /// Sort, either "asc" or "desc"
        /// </summary>
        [JsonProperty("sort")]
        public string Sort { get; set; }
    }
}
