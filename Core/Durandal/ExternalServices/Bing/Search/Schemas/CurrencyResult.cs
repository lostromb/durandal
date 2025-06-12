using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class CurrencyResult
    {
        /// <summary>
        /// Currency conversion result
        /// </summary>
        [JsonProperty("value")]
        public CurrencyAnswerResponse Value { get; set; }

        /// <summary>
        /// Contractual rules associated with the data source
        /// </summary>
        [JsonProperty("contractualRules")]
        public IList<TextAttribution> ContractualRules { get; set; }

        /// <summary>
        /// Data attributions
        /// </summary>
        [JsonProperty("attributions")]
        public IList<DataAttribution> Attributions { get; set; }

        /// <summary>
        /// A list of historical conversion rate trend data
        /// </summary>
        [JsonProperty("historicData")]
        public IList<CurrencyHistoricalPoint> HistoricData { get; set; }

        [JsonProperty("lastUpdated")]
        public DateTimeOffset? LastUpdated { get; set; }
    }
}
