using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Durandal.Common.Time.Timex.Enums;
using Durandal.Common.Time.Timex.Resources;
using Durandal.Common.Time.Timex.Actions;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// Extracts time expressions from given text
    /// </summary>
    public class TimexMatcher
    {
        private readonly TimexOptions _timexOptions;

        private readonly IDictionary<string, RegexResource> _regexResources = new Dictionary<string, RegexResource>();
        private readonly IDictionary<string, NormalizationResource> _normalizationResources = new Dictionary<string, NormalizationResource>();

        private readonly IList<RuleResource> _ruleResources = new List<RuleResource>();
        private readonly IList<RuleResource> _negativeRuleResources = new List<RuleResource>();
        
        private readonly IActionProvider _tagActionProvider;

        private readonly RegexOptions _positiveRegexFlags;
        private readonly RegexOptions _negativeRegexFlags;

        private TimexContext _grammarSpecificContext;

        /// <summary>
        /// Returns the default context that is specified by the currently loaded grammar.
        /// This allows the grammar to have some locale-specific controls over what types of inference it can perform.
        /// Proper matcher implementation should use this object as a base when building their resolution context.
        /// </summary>
        public TimexContext GrammarSpecificContext
        {
            get
            {
                return new TimexContext(_grammarSpecificContext);
            }
        }
        
        /// <summary>
        /// TimexMatcher constructor
        /// </summary>
        /// <param name="stream">Stream with grammar file</param>
        /// <param name="options">Options to control timex regular expression processing.  See <see cref="TimexOptions"/></param>
        public TimexMatcher(Stream stream, TimexOptions options = TimexOptions.None)
        {

#if COMPILED_SCRIPTS
            _tagActionProvider = new CompiledActionProvider();
#else
            _tagActionProvider = new InterpretedActionProvider();
#endif
            
            _timexOptions = options;

            if (_timexOptions.HasFlag(TimexOptions.CaseSensitive))
            {
                _positiveRegexFlags = RegexOptions.ExplicitCapture;
                _negativeRegexFlags = RegexOptions.None;
            }
            else
            {
                _positiveRegexFlags = RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase;
                _negativeRegexFlags = RegexOptions.IgnoreCase;
            }

#if NETFRAMEWORK
            if (_timexOptions.HasFlag(TimexOptions.Compiled))
            {
                _positiveRegexFlags |= RegexOptions.Compiled;
                _negativeRegexFlags |= RegexOptions.Compiled;
            }
#endif

            _grammarSpecificContext = new TimexContext();

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

                                var normalizationResource = new NormalizationResource(_timexOptions.HasFlag(TimexOptions.CaseSensitive));
                                normalizationResource.Parse(normalizationXElement);

                                _normalizationResources.Add(normalizationResource.Id, normalizationResource);

                                break;
                            }
                        case "rule":
                            {
                                var ruleXElement = XNode.ReadFrom(reader) as XElement;

                                var regexOptions = _positiveRegexFlags;

                                var ruleResource = new RuleResource(_regexResources, regexOptions, _tagActionProvider);
                                ruleResource.Parse(ruleXElement);

                                _ruleResources.Add(ruleResource);

                                break;
                            }
                        case "negative_rule":
                            {
                                var negativeRuleXElement = XNode.ReadFrom(reader) as XElement;

                                var regexOptions = _negativeRegexFlags;

                                var negativeRuleResource = new RuleResource(_regexResources, regexOptions, _tagActionProvider);
                                negativeRuleResource.Parse(negativeRuleXElement);

                                _negativeRuleResources.Add(negativeRuleResource);

                                break;
                            }
                        case "meta":
                            {
                                // Extract locale-specific context clues from the grammar file.
                                // These keys are then stored in the Timex's local grammarSpecificContext object.
                                var metaTag = XNode.ReadFrom(reader) as XElement;

                                // Silently ignore invalid tags
                                if (metaTag == null ||
                                    metaTag.Attribute("name") == null ||
                                    metaTag.Attribute("content") == null ||
                                    string.IsNullOrEmpty(metaTag.Attribute("name").Value) ||
                                    string.IsNullOrEmpty(metaTag.Attribute("content").Value))
                                    break;

                                if (metaTag.Attribute("name").Value.Equals("WeekdayLogic"))
                                {
                                    WeekdayLogic logic;
                                    if (EnumExtensions.TryParse(metaTag.Attribute("content").Value, out logic))
                                    {
                                        _grammarSpecificContext.WeekdayLogicType = logic;
                                    }
                                }
                                else if (metaTag.Attribute("name").Value.Equals("AmPmInferenceCutoff"))
                                {
                                    int cutoff;
                                    if (int.TryParse(metaTag.Attribute("content").Value, out cutoff))
                                    {
                                        _grammarSpecificContext.AmPmInferenceCutoff = cutoff;
                                    }
                                }
                                else if (metaTag.Attribute("name").Value.Equals("IncludeCurrentTimeInPastOrFuture"))
                                {
                                    bool flag;
                                    if (bool.TryParse(metaTag.Attribute("content").Value, out flag))
                                    {
                                        _grammarSpecificContext.IncludeCurrentTimeInPastOrFuture = flag;
                                    }
                                }

                                break;
                            }
                        }
                    }
                }
            }

            // Now that the grammar is loaded, compile all its tag scripts into a single assembly.
            _tagActionProvider.Compile(_normalizationResources);
        }

        /// <summary>
        /// Checks if the whole text contains a time expression and returns first possible interpretation using given context
        /// </summary>
        /// <param name="text">Text to match time expressions in</param>
        /// <param name="context">Context for extraction</param>
        /// <returns>TimexMatch object if match successfull; otherwise, null</returns>
        public TimexMatch Match(string text, TimexContext context)
        {
            if (text == null || context == null)
            {
                return null;
            }

            foreach (RuleResource ruleResource in _ruleResources)
            {
                if (ruleResource.Scope != ResourceScope.Public)
                {
                    continue;
                }

                if (context.TemporalType.HasFlag(ruleResource.RuleType))
                {
                    // Make a rule match from the rule
                    RuleMatch ruleMatch = ruleResource.RuleMatch(text);
                    if (ruleMatch == null)
                    {
                        continue;
                    }

                    // And make a timexmatch from that, ensuring that it is valid
                    ExtendedDateTime time = ExtendedDateTime.Create(ruleMatch.Rule.RuleType,
                                                               ruleMatch.TimexDictionary,
                                                               context);

                    if (time != null && !time.InputDateWasInvalid)
                    {
                        return new TimexMatch
                        {
                            ExtendedDateTime = time,
                            Index = ruleMatch.Index,
                            Value = ruleMatch.Value,
                            RuleId = ruleMatch.Rule.Id
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Searches the specified input string for all occurrences of time expressions and returns their interpretation using given context
        /// </summary>
        /// <param name="text">Text to match time expressions in</param>
        /// <param name="context">Context for extraction</param>
        /// <returns>TimexMatch object if match successfull; otherwise, null</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "method is more readable in that way")]
        public IList<TimexMatch> Matches(string text, TimexContext context)
        {
            var timexMatches = new List<TimexMatch>();

            var ruleMatches = _ruleResources
                .Where(ruleResource => ruleResource.Scope == ResourceScope.Public)
                .Where(ruleResource => context.TemporalType.HasFlag(ruleResource.RuleType))
                .Select(ruleResource => ruleResource.RuleMatches(text))
                .Where(ruleMatchList => ruleMatchList != null)
                .SelectMany(ruleMatchList => ruleMatchList)
                .OrderBy(ruleMatch => ruleMatch.Index);

            // disambiguate
            RuleMatch lastRuleMatch = null;
            TimexMatch lastTimexMatch = null;
            foreach (var ruleMatch in ruleMatches)
            {
                if (lastTimexMatch == null ||
                    ruleMatch.Index > lastTimexMatch.Index + lastTimexMatch.Value.Length)
                {
                    var timexMatch = new TimexMatch
                    {
                        ExtendedDateTime = ExtendedDateTime.Create(ruleMatch.Rule.RuleType,
                                                                   ruleMatch.TimexDictionary,
                                                                   context),
                        Index = ruleMatch.Index,
                        Value = ruleMatch.Value,
                        RuleId = ruleMatch.Rule.Id
                    };

                    if (!timexMatch.ExtendedDateTime.InputDateWasInvalid)
                    {
                        timexMatches.Add(timexMatch);
                    }

                    lastRuleMatch = ruleMatch;
                    lastTimexMatch = timexMatch;
                }
                else if (ruleMatch.Index == lastTimexMatch.Index &&
                         (ruleMatch.Value.Length > lastTimexMatch.Value.Length ||
                          ((ruleMatch.Value.Length == lastTimexMatch.Value.Length &&
                            ruleMatch.TimexDictionary.Count > lastRuleMatch.TimexDictionary.Count))))
                {
                    var timexMatch = new TimexMatch
                    {
                        ExtendedDateTime = ExtendedDateTime.Create(ruleMatch.Rule.RuleType,
                                                                   ruleMatch.TimexDictionary,
                                                                   context),
                        Index = ruleMatch.Index,
                        Value = ruleMatch.Value,
                        RuleId = ruleMatch.Rule.Id
                    };

                    if (!timexMatch.ExtendedDateTime.InputDateWasInvalid)
                    {
                        timexMatches.Remove(lastTimexMatch);
                        timexMatches.Add(timexMatch);
                    }

                    lastRuleMatch = ruleMatch;
                    lastTimexMatch = timexMatch;
                }
            }

            // remove invalid matches
            var negativeRuleMatches = _negativeRuleResources
                .Where(ruleResource => ruleResource.Scope == ResourceScope.Public)
                .Where(ruleResource => context.TemporalType.HasFlag(ruleResource.RuleType))
                .Select(ruleResource => ruleResource.RuleMatches(text))
                .Where(ruleMatchList => ruleMatchList != null)
                .SelectMany(ruleMatchList => ruleMatchList)
                .OrderBy(ruleMatch => ruleMatch.Index);

            foreach (var negativeRuleMatch in negativeRuleMatches)
            {
                for (int i = timexMatches.Count - 1; i >= 0; i--)
                {
                    // A negative rule match will trigger if the entire negative rule match encloses the positive one
                    if (timexMatches[i].Index >= negativeRuleMatch.Index &&
                        timexMatches[i].Index + timexMatches[i].Value.Length <= negativeRuleMatch.Index + negativeRuleMatch.Value.Length)
                    {
                        timexMatches.RemoveAt(i);
                    }
                }
            }

            // set ids
            for (int i = 0; i < timexMatches.Count; i++)
            {
                timexMatches[i].Id = i;
            }

            return timexMatches;
        }
    }
}
