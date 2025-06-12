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
    public class TrimRightTransformer : ISlotTransformer
    {
        public char[] _chars;

        public TrimRightTransformer(string chars)
        {
            _chars = chars.ToCharArray();
        }

        public string Name
        {
            get
            {
                return "TrimRight";
            }
        }

        public string Apply(object input, LanguageCode locale, ILogger logger, NLPTools nlTools, StatisticalLGPattern pattern)
        {
            if (input is string)
            {
                return ((string)input).TrimEnd(_chars);
            }
            else
            {
                return input.ToString().TrimEnd(_chars);
            }
        }

        public string OriginalText
        {
            get
            {
                return Name + "(\'" + string.Join("\', \'", _chars) + "\')";
            }
        }
    }
}
