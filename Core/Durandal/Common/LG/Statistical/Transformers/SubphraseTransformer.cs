using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.LG.Statistical.Transformers
{
    public class SubphraseTransformer : ISlotTransformer
    {
        public string SubphraseModelName;

        public SubphraseTransformer(string phraseName)
        {
            SubphraseModelName = phraseName;
        }

        public string Name
        {
            get
            {
                return "Subphrase";
            }
        }

        public string Apply(object input, LanguageCode locale, ILogger logger, NLPTools nlTools, StatisticalLGPattern pattern)
        {
            throw new InvalidOperationException("SubphraseTransformer cannot be applied like a normal transformer");
        }

        public string OriginalText
        {
           get
            {
                return Name + "(" + SubphraseModelName + ")";
            }
        }
    }
}
