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
    public class CapitalizeTransformer : ISlotTransformer
    {
        public CapitalizeTransformer()
        {
        }
        public string Name
        {
            get
            {
                return "Capitalize";
            }
        }

        public string Apply(object input, LanguageCode locale, ILogger logger, NLPTools nlTools, StatisticalLGPattern pattern)
        {
            if (input == null)
            {
                return "NULL";
            }

            string stringVal;
            if (input is string)
            {
                stringVal = ((string)input);
            }
            else
            {
                stringVal = input.ToString();
            }


            if (string.IsNullOrEmpty(stringVal))
            {
                return stringVal;
            }

            if (stringVal.Length == 1)
            {
                return stringVal.ToUpperInvariant();
            }

            return char.ToUpperInvariant(stringVal[0]) + stringVal.Substring(1);
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
