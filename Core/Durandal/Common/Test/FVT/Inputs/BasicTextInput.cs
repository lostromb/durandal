using Durandal.Common.IO.Json;
using Durandal.Common.NLP.Language;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT.Inputs
{
    /// <summary>
    /// Represents a simple text input to a dialog engine, with optional client context
    /// </summary>
    public class BasicTextInput : AbstractFunctionalTestInput
    {
        public static readonly string INPUT_TYPE = "Text";

        [JsonProperty("Type")]
        public override string Type => INPUT_TYPE;

        /// <summary>
        /// The input text to send
        /// </summary>
        [JsonProperty("Text")]
        public string Text { get; set; }

        /// <summary>
        /// The locale of the input, "xx-xx"
        /// </summary>
        [JsonProperty("Locale")]
        [JsonConverter(typeof(JsonLanguageCodeConverter))]
        public LanguageCode Locale { get; set; }

        /// <summary>
        /// Optional client context parameters to set
        /// </summary>
        [JsonProperty("ClientContext")]
        public InputClientContext ClientContext { get; set; }
    }
}
