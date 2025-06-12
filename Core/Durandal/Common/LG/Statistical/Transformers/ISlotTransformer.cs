using Durandal.Common.Utils;
using Durandal.Common.Logger;
using Durandal.Common.NLP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.NLP.Language;

namespace Durandal.Common.LG.Statistical.Transformers
{
    public interface ISlotTransformer
    {
        string Name { get; }
        string Apply(object input, LanguageCode locale, ILogger logger, NLPTools nlTools, StatisticalLGPattern pattern);
        string OriginalText { get; }
    }
}
