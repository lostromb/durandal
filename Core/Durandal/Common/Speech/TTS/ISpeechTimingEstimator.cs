using Durandal.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Speech.TTS
{
    public interface ISpeechTimingEstimator
    {
        IList<SynthesizedWord> EstimatePhraseWeights(Sentence words, string ssml, TimeSpan totalSpeechTime);
    }
}
