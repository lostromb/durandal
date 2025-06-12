using Durandal.Common.IO.Json;
using Durandal.Common.NLP.Language;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT.Inputs
{
    /// <summary>
    /// A functional validation test input which means "Execute the dialog action which was passed to client actions in a previous turn"
    /// </summary>
    public class ClientDialogActionInput : AbstractFunctionalTestInput
    {
        public static readonly string INPUT_TYPE = "ClientDialogAction";

        [JsonProperty("Type")]
        public override string Type => INPUT_TYPE;

        /// <summary>
        /// The ordinal of the turn response to read the dialog action from. This defaults to the previous turn.
        /// </summary>
        [JsonProperty("SourceTurn")]
        public int? SourceTurn { get; set; }

        /// <summary>
        /// The locale of the client, "xx-xx"
        /// </summary>
        [JsonProperty("Locale")]
        [JsonConverter(typeof(JsonLanguageCodeConverter))]
        public LanguageCode Locale { get; set; }

        /// <summary>
        /// Extra client context to send in the request
        /// </summary>
        [JsonProperty("ClientContext")]
        public InputClientContext ClientContext { get; set; }
    }
}
