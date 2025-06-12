using Durandal.API;
using Durandal.Common.NLP.Language;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.TTS
{
    /// <summary>
    /// Represents a voice which can be used for speech synthesis
    /// </summary>
    public class Voice
    {
        public Voice(LanguageCode locale, VoiceGender gender, string name)
        {
            Locale = locale;
            Gender = gender;
            Name = name;
        }

        public LanguageCode Locale { get; private set; }

        public VoiceGender Gender { get; private set; }

        public string Name { get; private set; }
    }
}
