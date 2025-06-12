using Durandal.Common.Logger;
using Durandal.Common.NLP.Feature;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Durandal.Common.NLP.Language.English
{
    public class EnglishLGFeatureExtractor : ILGFeatureExtractor
    {
        private static readonly Regex NumberEndMatcher = new Regex("[0-9,\\.]+$");
        private static readonly Regex NumberBeginMatcher = new Regex("^[0-9,\\.]+");

        public void ExtractTagFeatures(IDictionary<string, string> tags, IDictionary<int, string> groupToTagMap, int currentGroup, List<string> featuresOut)
        {
            // Calculate what the group / tag geometry looks like
            int prev = -1;
            int next = 99999;
            int min = 99999;
            int max = -1;
            foreach (int grp in groupToTagMap.Keys)
            {
                if (grp > prev && grp <= currentGroup)
                    prev = grp;
                if (grp < next && grp > currentGroup)
                    next = grp;
                if (grp < min)
                    min = grp;
                if (grp > max)
                    max = grp;
            }

            // Find the nearest previous and succeeding tag and extract from those
            if (groupToTagMap.ContainsKey(prev))
            {
                string tagName = groupToTagMap[prev];
                if (tags.ContainsKey(tagName))
                {
                    ExtractFromOneTag("p", tags[tagName], featuresOut);
                }
            }

            if (groupToTagMap.ContainsKey(next))
            {
                string tagName = groupToTagMap[next];
                if (tags.ContainsKey(tagName))
                {
                    ExtractFromOneTag("n", tags[tagName], featuresOut);
                }
            }

            // In addition to getting detailed features from the nearest tags, we also get the basic tag values from all tags in the context.
            // This helps us pick up on a few key features that define the entire phrase, notably empty tokens that are used in variable-length lists
            for (int grp = min; grp <= max; grp++)
            {
                if (grp == next || grp == prev)
                {
                    continue;
                }

                if (groupToTagMap.ContainsKey(grp))
                {
                    string tagName = groupToTagMap[grp];
                    if (tags.ContainsKey(tagName))
                    {
                        // Feature: entire lowercase value of tags that are not the previous or next tags
                        featuresOut.Add("t-" + tagName + "=" + tags[tagName].ToLowerInvariant());
                    }
                }
            }

            for (int grp = min; grp <= max; grp++)
            {
                if (groupToTagMap.ContainsKey(grp))
                {
                    string tagName = groupToTagMap[grp];
                    if (!tags.ContainsKey(tagName) || string.IsNullOrEmpty(tags[tagName]))
                    {
                        // Feature: Any empty tag at any place in the sentence
                        featuresOut.Add("et-" + tagName);
                    }
                    else
                    {
                        // Feature: Any non-empty tag at any place in the sentence
                        featuresOut.Add("net-" + tagName);
                    }
                }
            }
        }

        private static void ExtractFromOneTag(string tagName, string tagValue, List<string> featuresOut)
        {
            // Feature: The exact tag value in lowercase
            featuresOut.Add("t-" + tagName + "=" + tagValue.ToLowerInvariant());

            // Feature: Character-level n-grams, order 3, from start and end of each tag value
            ExtractCharacterNGrams(tagName, tagValue, 1, featuresOut);
            ExtractCharacterNGrams(tagName, tagValue, 2, featuresOut);
            ExtractCharacterNGrams(tagName, tagValue, 3, featuresOut);

            // Feature: String length
            featuresOut.Add("t-" + tagName + "-l=" + tagValue.Length);
            
            int intVal;
            Match numMatch = NumberBeginMatcher.Match(tagValue);
            if (numMatch.Success)
            {
                // Format out comma and decimal  (and decimals too?)
                string numVal = numMatch.Value.Replace(",", "");
                if (int.TryParse(numVal, out intVal))
                {
                    // Feature: Starts with a number
                    featuresOut.Add("t-" + tagName + "-b-num");

                    // Feature: Starts with a "singular" number and not a plural
                    if (intVal == 1 || intVal == -1)
                    {
                        featuresOut.Add("t-" + tagName + "-b-sdigit");
                    }
                }
            }

            numMatch = NumberEndMatcher.Match(tagValue);
            if (numMatch.Success)
            {
                // Format out comma separators (and decimals too?)
                string numVal = numMatch.Value.Replace(",", "");
                if (int.TryParse(numVal, out intVal))
                {
                    // Feature: Ends with a number
                    featuresOut.Add("t-" + tagName + "-e-num");

                    // Feature: Ends with a "singular" number and not a plural
                    if (intVal == 1 || intVal == -1)
                    {
                        featuresOut.Add("t-" + tagName + "-e-sdigit");
                    }

                    // Feature: "ends with a number that is not followed by -th", which greatly improves accuracy for '1st', '2nd', '3rd' etc. phrases
                    featuresOut.Add(GenerateFeatureForNumberPlurality(tagName, intVal));
                }
            }

            // Feature: Begins with a vowel
            if (tagValue.Length > 0)
            {
                char v = char.ToLower(tagValue[0]);
                if (v == 'a' || v == 'e' || v == 'i' || v == 'o' || v == 'u')
                {
                    featuresOut.Add("t-" + tagName + "-svow");
                }
            }
        }

        private static string GenerateFeatureForNumberPlurality(string tagName, int number)
        {
            int lastDigit = number % 10;
            int secondToLastDigit = (number % 100) / 10;
            if (lastDigit == 1 && secondToLastDigit != 1)
            {
                // Feature: "number followed by -st"
                return "t-" + tagName + "-stdigit";
            }
            else if (lastDigit == 2 && secondToLastDigit != 1)
            {
                // Feature: "number followed by -nd"
                return "t-" + tagName + "-nddigit";
            }
            else if (lastDigit == 3 && secondToLastDigit != 1)
            {
                // Feature: "number followed by -rd"
                return "t-" + tagName + "-rddigit";
            }
            else
            {
                // Feature: "number followed by -th"
                return "t-" + tagName + "-thdigit";
            }
        }

        private static void ExtractCharacterNGrams(string tagName, string value, int order, List<string> features)
        {
            if (value.Length >= order)
            {
                features.Add("t-" + tagName + "-c+" + order + "=" + value.Substring(0, order));
                features.Add("t-" + tagName + "-c-" + order + "=" + value.Substring(value.Length - order));
            }
            //else
            //{
            //    string spaces = string.Empty;
            //    for (int x = 0; x < order - value.Length; x++)
            //        spaces += " ";
            //    features.Add("t-" + tagName + "-c+" + order + "=" + spaces + value);
            //    features.Add("t-" + tagName + "-c-" + order + "=" + value + spaces);
            //}
        }
    }
}
