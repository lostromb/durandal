using Durandal.Common.IO.Json;
using Durandal.Common.NLP.Language;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT.Inputs
{
    /// <summary>
    /// Represents a simple speech input to a dialog engine (where the input is a single
    /// string from which a fake reco result will be generated), with optional client context
    /// </summary>
    public class BasicSpeechInput : AbstractFunctionalTestInput
    {
        public static readonly string INPUT_TYPE = "Speech";

        [JsonProperty("Type")]
        public override string Type => INPUT_TYPE;

        [JsonProperty("DisplayText")]
        public string DisplayText { get; set; }

        [JsonProperty("Locale")]
        [JsonConverter(typeof(JsonLanguageCodeConverter))]
        public LanguageCode Locale { get; set; }

        [JsonProperty("ClientContext")]
        public InputClientContext ClientContext { get; set; }
    }
}
