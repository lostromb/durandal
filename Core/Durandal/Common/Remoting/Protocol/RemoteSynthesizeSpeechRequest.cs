using Durandal.API;
using Durandal.Common.Dialog;
using Durandal.Common.Dialog.Services;
using Durandal.Common.Ontology;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Remoting.Protocol
{
    public class RemoteSynthesizeSpeechRequest : RemoteProcedureRequest
    {
        public static readonly string METHOD_NAME = "SynthesizeSpeech";

        public override string MethodName => METHOD_NAME;

        public SpeechSynthesisRequest SynthRequest { get; set; }
    }
}
