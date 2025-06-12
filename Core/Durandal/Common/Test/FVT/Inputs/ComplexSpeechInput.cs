using Durandal.API;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test.FVT.Inputs
{
    /// <summary>
    /// Represents a single speech input to a dialog engine, where the entire speech reco result is fully specified
    /// </summary>
    public class ComplexSpeechInput : AbstractFunctionalTestInput
    {
        public static readonly string INPUT_TYPE = "ComplexSpeech";

        [JsonProperty("Type")]
        public override string Type => INPUT_TYPE;

        [JsonProperty("SpeechRecognitionResult")]
        public SpeechRecognitionResult SpeechRecognitionResult { get; set; }

        [JsonProperty("ClientContext")]
        public InputClientContext ClientContext { get; set; }
    }
}
