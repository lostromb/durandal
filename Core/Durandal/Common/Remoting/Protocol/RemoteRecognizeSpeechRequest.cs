using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.IO.Json;
using Durandal.Common.NLP.Language;
using Durandal.Common.Ontology;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteRecognizeSpeechRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "RecognizeSpeech";

        public override string MethodName => METHOD_NAME;

        [JsonConverter(typeof(JsonLanguageCodeConverter))]
        public LanguageCode Locale { get; set; }
        public AudioData Audio { get; set; }
    }
}
