using Durandal.Common.Logger;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Durandal.Common.NLP.Canonical
{
    /// <summary>
    /// Extracts matched expressions from given text and triggers substitution logic
    /// </summary>
    public class Grammar
    {
        private readonly IDictionary<string, RegexResource> _regexResources = new Dictionary<string, RegexResource>();
        private readonly IDictionary<string, NormalizationResource> _normalizationResources = new Dictionary<string, NormalizationResource>();

        private readonly IList<RuleResource> _ruleResources = new List<RuleResource>();
        private readonly IList<RuleResource> _negativeRuleResources = new List<RuleResource>();
        
        /// <summary>
        /// CrfTemporalTokenParser constructor
        /// </summary>
        /// <param name="stream">Stream with grammar file</param>
        public Grammar(Stream stream)
        {
            ReadResources(stream);
        }

        /// <summary>
        /// Reads regular expressions, rules and normalization resources from grammar file
        /// </summary>
        private void ReadResources(Stream stream)
        {
            using (var reader = XmlReader.Create(stream))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        // parse only known elements, ignore everything else
                        switch (reader.Name)
                        {
                            case "regex":
                                {
                                    var regexXElement = XNode.ReadFrom(reader) as XElement;

                                    var regexResource = new RegexResource();
                                    regexResource.Parse(regexXElement);

                                    _regexResources.Add(regexResource.Id, regexResource);

                                    break;
                                }
                            case "normalization_rule":
                                {
                                    var normalizationXElement = XNode.ReadFrom(reader) as XElement;

                                    var normalizationResource = new NormalizationResource();
                                    normalizationResource.Parse(normalizationXElement);

                                    _normalizationResources.Add(normalizationResource.Id, normalizationResource);

                                    break;
                                }
                            case "rule":
                                {
                                    var ruleXElement = XNode.ReadFrom(reader) as XElement;

                                    var regexOptions = RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase;

                                    var ruleResource = new RuleResource(_regexResources, _normalizationResources, regexOptions);
                                    ruleResource.Parse(ruleXElement);

                                    _ruleResources.Add(ruleResource);

                                    break;
                                }
                            case "negative_rule":
                                {
                                    var negativeRuleXElement = XNode.ReadFrom(reader) as XElement;

                                    var regexOptions = RegexOptions.IgnoreCase;

                                    var negativeRuleResource = new RuleResource(_regexResources, _normalizationResources, regexOptions);
                                    negativeRuleResource.Parse(negativeRuleXElement);

                                    _negativeRuleResources.Add(negativeRuleResource);

                                    break;
                                }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the whole text is a time expression and returns first possible interpretation using given context
        /// </summary>
        /// <param name="text">Text to match time expressions in</param>
        /// <param name="queryLogger">A logger</param>
        /// <returns>GrammarMatch object if match successfull; otherwise, null</returns>
        public GrammarMatch Match(string text, ILogger queryLogger)
        {
            if (text == null)
            {
                return null;
            }

            foreach (RuleResource ruleResource in _ruleResources)
            {
                
                // Make a rule match from the rule
                RuleMatch ruleMatch = ruleResource.RuleMatch(text, queryLogger);
                if (ruleMatch == null)
                {
                    continue;
                }

                return new GrammarMatch
                {
                    Index = ruleMatch.Index,
                    Value = ruleMatch.Value,
                    RuleId = ruleMatch.Rule.Id,
                    NormalizedValue = ruleMatch.NormalizedTagValue
                };
            }

            return null;
        }

        public IList<GrammarMatch> Matches(string text, ILogger queryLogger)
        {
            var grammarMatches = new List<GrammarMatch>();

            var ruleMatches = _ruleResources
                .Select(ruleResource => ruleResource.RuleMatches(text, queryLogger))
                .Where(ruleMatchList => ruleMatchList != null)
                .SelectMany(ruleMatchList => ruleMatchList)
                .OrderBy(ruleMatch => ruleMatch.Index);

            // disambiguate
            GrammarMatch lastGrammarMatch = null;
            foreach (var ruleMatch in ruleMatches)
            {
                if (lastGrammarMatch == null ||
                    ruleMatch.Index > lastGrammarMatch.Index + lastGrammarMatch.Value.Length)
                {
                    var grammarMatch = new GrammarMatch
                    {
                        Index = ruleMatch.Index,
                        Value = ruleMatch.Value,
                        RuleId = ruleMatch.Rule.Id,
                        NormalizedValue = ruleMatch.NormalizedTagValue
                    };

                    grammarMatches.Add(grammarMatch);
                    lastGrammarMatch = grammarMatch;
                }
                else if (ruleMatch.Index == lastGrammarMatch.Index &&
                         (ruleMatch.Value.Length > lastGrammarMatch.Value.Length))
                {
                    var grammarMatch = new GrammarMatch
                    {
                        Index = ruleMatch.Index,
                        Value = ruleMatch.Value,
                        RuleId = ruleMatch.Rule.Id,
                        NormalizedValue = ruleMatch.NormalizedTagValue
                    };

                    grammarMatches.Remove(lastGrammarMatch);
                    grammarMatches.Add(grammarMatch);
                    lastGrammarMatch = grammarMatch;
                }
            }

            // remove invalid matches
            var negativeRuleMatches = _negativeRuleResources
                .Select(ruleResource => ruleResource.RuleMatches(text, queryLogger))
                .Where(ruleMatchList => ruleMatchList != null)
                .SelectMany(ruleMatchList => ruleMatchList)
                .OrderBy(ruleMatch => ruleMatch.Index);

            foreach (var negativeRuleMatch in negativeRuleMatches)
            {
                for (int i = grammarMatches.Count - 1; i >= 0; i--)
                {
                    // A negative rule match will trigger if the entire negative rule match encloses the positive one
                    if (grammarMatches[i].Index >= negativeRuleMatch.Index &&
                        grammarMatches[i].Index + grammarMatches[i].Value.Length <= negativeRuleMatch.Index + negativeRuleMatch.Value.Length)
                    {
                        grammarMatches.RemoveAt(i);
                    }
                }
            }

            return grammarMatches;
        }
    }
}
