#pragma warning disable CS0649

using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Speech.SR.Azure
{
    internal class SpeechHypothesis
    {
        public string Text;
        public long? Offset;
        public long? Duration;
    }
}


#pragma warning restore CS0649