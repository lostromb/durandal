using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.NLP.Feature;
using System.Text.RegularExpressions;
using Durandal.Common.NLP.ApproxString;
using Durandal.API;

namespace Durandal.Common.NLP.Language.English
{
    public class EnglishWholeWordApproxStringFeatureExtractor : IApproxStringFeatureExtractor
    {
        private static readonly Regex WRITTEN_MATCHER = new Regex("([0-9]+)|([a-z']+)");
        private static readonly Regex SPOKEN_MATCHER = new Regex("\\S+");

        public IList<string> ExtractFeatures(LexicalString input)
        {
            IList<string> returnVal = new List<string>();
            MatchCollection matches;

            if (!string.IsNullOrEmpty(input.WrittenForm))
            {
                matches = WRITTEN_MATCHER.Matches(input.WrittenForm.ToLowerInvariant());

                foreach (Match m in matches)
                {
                    returnVal.Add("w:" + m.Value);
                }
            }

            if (!string.IsNullOrEmpty(input.SpokenForm))
            {
                matches = SPOKEN_MATCHER.Matches(input.SpokenForm);

                foreach (Match m in matches)
                {
                    returnVal.Add("s:" + m.Value);
                }
            }

            return returnVal;
        }
    }
}
