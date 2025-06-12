using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Speech.SR.Azure
{
    //{"RecognitionStatus":"Success","Offset":5900000,"Duration":12100000,"NBest":[{"Confidence":0.80988121032714844,"Lexical":"das ist ein test","ITN":"das ist ein Test","MaskedITN":"das ist ein Test","Display":"Das ist ein Test."}]}

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated with reflection")]
    internal class SpeechResponse
    {
        public string RecognitionStatus { get; set; }
        public long? Offset { get; set; }
        public long? Duration { get; set; }
        public IList<AzureSpeechNBestResult> NBest { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated with reflection")]
    internal class AzureSpeechNBestResult
    {
        public float Confidence { get; set; }
        public string Lexical { get; set; }
        public string ITN { get; set; }
        public string MaskedITN { get; set; }
        public string Display { get; set; }
    }
}
