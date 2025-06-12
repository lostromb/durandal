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
    public class TranslateTransformer : ISlotTransformer
    {
        private string _translationTableName;

        public TranslateTransformer(string tableName)
        {
            _translationTableName = tableName;
        }

        public string Name
        {
            get
            {
                return "Translate";
            }
        }

        public string Apply(object input, LanguageCode locale, ILogger logger, NLPTools nlTools, StatisticalLGPattern pattern)
        {
            string stringVal;
            if (input is string)
            {
                stringVal = ((string)input);
            }
            else
            {
                stringVal = input.ToString();
            }

            Dictionary<string, string> translationTable = pattern.GetTranslationTable(_translationTableName);
            if (translationTable == null)
            {
                return stringVal;
            }

            string returnVal;
            if (translationTable.TryGetValue(stringVal, out returnVal))
            {
                return returnVal;
            }

            return stringVal;
        }

        public string OriginalText
        {
            get
            {
                return Name + "(" + _translationTableName + ")";
            }
        }
    }
}
