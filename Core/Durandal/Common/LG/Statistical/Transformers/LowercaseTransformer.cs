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
    public class LowercaseTransformer : ISlotTransformer
    {
        public LowercaseTransformer()
        {
        }
        public string Name
        {
            get
            {
                return "Lowercase";
            }
        }

        public string Apply(object input, LanguageCode locale, ILogger logger, NLPTools nlTools, StatisticalLGPattern pattern)
        {
            if (input is string)
            {
                return ((string)input).ToLowerInvariant();
            }
            else
            {
                return input.ToString().ToLowerInvariant();
            }
        }

        public string OriginalText
        {
            get
            {
                return Name;
            }
        }
    }
}
