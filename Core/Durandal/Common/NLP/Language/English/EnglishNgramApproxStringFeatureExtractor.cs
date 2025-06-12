using Durandal.API;
using Durandal.Common.NLP.ApproxString;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Language.English
{
    public class EnglishNgramApproxStringFeatureExtractor : IApproxStringFeatureExtractor
    {
        private static readonly Regex WRITTEN_MATCHER = new Regex("([0-9]+)|([a-z']+)");
        private static readonly Regex SPOKEN_MATCHER = new Regex("\\S+");
        private readonly int _writtenOrder;
        private readonly int _ipaOrder;

        public EnglishNgramApproxStringFeatureExtractor(int order = 4)
        {
            _writtenOrder = order;
            _ipaOrder = order;
    }

        public IList<string> ExtractFeatures(LexicalString input)
        {
            IList<string> returnVal = new List<string>();
            MatchCollection matches;

            if (!string.IsNullOrEmpty(input.WrittenForm))
            {
                matches = WRITTEN_MATCHER.Matches(input.WrittenForm.ToLowerInvariant());

                foreach (Match m in matches)
                {
                    string stringVal = m.Value;
                    returnVal.Add(stringVal);

                    // Extract fourgrams from the word, which should be able to give us more accuracy in the presence of misspellings, etc.
                    if (stringVal.Length >= _writtenOrder)
                    {
                        for (int idx = 0; idx < stringVal.Length - _writtenOrder + 1; idx++)
                        {
                            returnVal.Add("w:" + stringVal.Substring(idx, _writtenOrder));
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(input.SpokenForm))
            {
                matches = SPOKEN_MATCHER.Matches(input.SpokenForm);

                foreach (Match m in matches)
                {
                    string stringVal = m.Value;
                    returnVal.Add(stringVal);

                    // Extract trigrams from the IPA
                    if (stringVal.Length >= _ipaOrder)
                    {
                        for (int idx = 0; idx < stringVal.Length - _ipaOrder + 1; idx++)
                        {
                            returnVal.Add("s:" + stringVal.Substring(idx, _ipaOrder));
                        }
                    }
                }
            }

            return returnVal;
        }
    }
}
