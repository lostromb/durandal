using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.ExternalServices.Bing.Search.Schemas
{
    public class CurrencyAnswerResponse
    {
        /// <summary>
        /// Currency code for source currency, e.g. "USD"
        /// </summary>
        [JsonProperty("fromCurrency")]
        public string FromCurrency { get; set; }

        /// <summary>
        /// Value of source currency, e.g. 10
        /// </summary>
        [JsonProperty("fromValue")]
        public decimal FromValue { get; set; }

        /// <summary>
        /// Localized name of source currency, e.g. "US Dollar"
        /// </summary>
        [JsonProperty("fromCurrencyName")]
        public string FromCurrencyName { get; set; }

        /// <summary>
        /// Currency code of target currency, e.g. "EUR"
        /// </summary>
        [JsonProperty("toCurrency")]
        public string ToCurrency { get; set; }

        /// <summary>
        /// Value of the target currency, or in other words, the conversion result
        /// </summary>
        [JsonProperty("toValue")]
        public decimal ToValue { get; set; }

        /// <summary>
        /// Localized name of target currency, e.g. "Euro"
        /// </summary>
        [JsonProperty("toCurrencyName")]
        public string ToCurrencyName { get; set; }

        /// <summary>
        /// Conversion rate from source to target
        /// </summary>
        [JsonProperty("forwardConversionRate")]
        public double ForwardConversionRate { get; set; }

        /// <summary>
        /// Conversion rate from target to source
        /// </summary>
        [JsonProperty("backwardConversionRate")]
        public double BackwardConversionRate { get; set; }
    }
}
