using Durandal.API;
using Durandal.Common.Audio;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.API
{
    public class SynthesizedSpeech
    {
        public AudioData Audio { get; set; }
        public string Locale { get; set; }
        public string Ssml { get; set; }
        public string PlainText { get; set; }
        public IList<SynthesizedWord> Words { get; set; }
    }
}
