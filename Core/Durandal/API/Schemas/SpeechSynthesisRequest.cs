using Durandal.Common.IO.Json;
using Durandal.Common.NLP.Language;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    /// <summary>
    /// Represents a request to synthesize speech from any implementation of a text-to-speech synthesizer
    /// </summary>
    public class SpeechSynthesisRequest
    {
        /// <summary>
        /// The SSML markup string to be rendered into speech. This is the preferred input to a synthesizer.
        /// </summary>
        public string Ssml { get; set; }

        /// <summary>
        /// The plaintext string to be rendered into speech. Only used if SSML is not specified.
        /// </summary>
        public string Plaintext { get; set; }

        /// <summary>
        /// The requested voice gender to use, which could be "unspecified" to use the default of the engine.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public VoiceGender VoiceGender { get; set; }

        /// <summary>
        /// The primary locale of the request (though the SSML may potentially contain interjections from an alternative locale)
        /// </summary>
        [JsonConverter(typeof(JsonLanguageCodeConverter))]
        public LanguageCode Locale { get; set; }
    }
}
