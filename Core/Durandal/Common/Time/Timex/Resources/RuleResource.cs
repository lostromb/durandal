

namespace Durandal.Common.Time.Timex.Resources
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Security;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;

    using Durandal.Common.Time.Timex.Actions;
    using Durandal.Common.Time.Timex.Enums;

    /// <summary>
    /// Represents rule and negative rule resources from grammar file
    /// </summary>
    public class RuleResource : GrammarResource
    {
        #region Fields

        private const string BeginningOfStringRegex = @"^";
        private const string EndOfStringRegex = @"$";
        private const string WhiteSpaceRegex = @"\s*";
        private const string RegexPartTemplate = @"Part{0}";
        
        private static readonly char[] TrimmedChars = new [] {' ','\t','\n','\r','\v','\f',','};

        private readonly RegexOptions _regexOptions;

        private string _expression;
        private Regex _compiledExpression;

        private string _exactExpression;
        private Regex _compiledExactExpression;

        private int _markedPartsCount;
        private readonly IList<Tuple<string, string>> _tagScripts;
        private IDictionary<string, TagAction> _tagActions;
        private readonly IDictionary<string, string> _posRestrictions;
        private readonly IList<string> _includeRestrictions;

        private readonly IDictionary<string, RegexResource> _regexResources;
        private readonly IActionProvider _tagActionProvider;

        #endregion

        public TemporalType RuleType { get; set; }

        public RuleResource(IDictionary<string, RegexResource> regexResources,
                            RegexOptions regexOptions,
                            IActionProvider scriptProvider)
        {
            _regexResources = regexResources;
            _regexOptions = regexOptions;

            _markedPartsCount = 0;
            _tagScripts = new List<Tuple<string, string>>();
            _posRestrictions = new Dictionary<string, string>();
            _includeRestrictions = new List<string>();
            _tagActionProvider = scriptProvider;
        }

        /// <summary>
        /// Extracts repeat attribute of XElement in format suitable for regular expressions
        /// </summary>
        /// <param name="element">XElement from which repeat attribute is extracted</param>
        /// <returns>Repeat attribute</returns>
        private static string ExtractRepeatAttribute(XElement element)
        {
            var repeat = string.Empty;
            var repeatAttribute = element.Attribute(GrammarElements.RepeatAttribute);
            if (repeatAttribute != null)
            {
                var repeatAttributeValue = repeatAttribute.Value;
                var repeatAttributeValueArray = repeatAttributeValue.Split('-');

                if (repeatAttributeValueArray.Length == 2)
                {
                    repeat = string.Format("{{{0},{1}}}",
                        repeatAttributeValueArray[0],
                        repeatAttributeValueArray[1]);
                }
            }

            return repeat;
        }

        /// <summary>
        /// Extracts and removes child tag element of XElement
        /// </summary>
        /// <param name="element">XElement from which tag element is extracted</param>
        /// <returns>Tag element</returns>
        private static XElement ExtractAndRemoveTagElement(XElement element)
        {
            var tagElement = element.Element(GrammarElements.Tag);
            if (tagElement != null)
                tagElement.Remove();

            return tagElement;
        }

        /// <summary>
        /// Converts ruleref element to the regular expression
        /// </summary>
        /// <param name="ruleref">Ruleref element to convert</param>
        /// <param name="expressionBuilder">StringBuilder to put converted expression to</param>
        /// <returns>true if element is successfully converted; otherwise, false</returns>
        private void ParseRuleref(XElement ruleref, StringBuilder expressionBuilder)
        {
            // extract uri attribute
            var uriAttribute = ruleref.Attribute(GrammarElements.UriAttribute);
            if (uriAttribute == null)
                throw new TimexException(string.Format("Incorrect format. Rule {0}. Ruleref uri is not specified", Id));

            var ruleId = uriAttribute.Value.StartsWith("#", StringComparison.Ordinal) ?
                uriAttribute.Value.Substring(1) :
                uriAttribute.Value;

            if (!_regexResources.ContainsKey(ruleId))
                throw new TimexException(string.Format("Incorrect format. Rule {0}. Ruleref uri is not valid", Id));

            var regexResource = _regexResources[ruleId];
            expressionBuilder.AppendFormat("({0})", regexResource.Expression);
        }

        /// <summary>
        /// Converts item element to the regular expression
        /// </summary>
        /// <param name="item">Item element to convert</param>
        /// <param name="expressionBuilder">StringBuilder to put converted expression to</param>
        /// <returns>true if element is successfully converted; otherwise, false</returns>
        private void ParseItem(XElement item, StringBuilder expressionBuilder)
        {
            var repeat = ExtractRepeatAttribute(item);
            var include = item.Attribute(GrammarElements.IncludeAttribute);
            var pos = item.Attribute(GrammarElements.PosAttribute);
            
            var itemTag = ExtractAndRemoveTagElement(item);

            // generate regular expression
            if (itemTag != null || include != null || pos != null || !string.IsNullOrEmpty(repeat))
            {
                var partName = string.Format(RegexPartTemplate, _markedPartsCount);

                expressionBuilder.AppendFormat("(?<{0}>", partName);

                if (itemTag != null)
                {
                    var key = partName;
                    var script = itemTag.Value;

                    _tagScripts.Add(new Tuple<string, string>(key, script));
                }

                if (include != null)
                {
                    bool includeValue;
                    if (bool.TryParse(include.Value, out includeValue))
                    {
                        if (!includeValue)
                        {
                            _includeRestrictions.Add(partName);
                        }
                    }
                }

                if (pos != null)
                {
                    _posRestrictions.Add(partName, pos.Value);
                }

                _markedPartsCount++;
            }

            var itemElements = item.Elements().ToList();
            if (itemElements.Count > 0)
            {
                foreach (var itemElement in itemElements)
                {
                    switch (itemElement.Name.LocalName)
                    {
                        case GrammarElements.Item:
                            {
                                itemElement.Remove();
                                ParseItem(itemElement, expressionBuilder);

                                break;
                            }
                        case GrammarElements.OneOf:
                            {
                                itemElement.Remove();
                                ParseOneOf(itemElement, expressionBuilder);

                                break;
                            }
                        case GrammarElements.Ruleref:
                            {
                                itemElement.Remove();
                                ParseRuleref(itemElement, expressionBuilder);

                                break;
                            }
                    }
                    expressionBuilder.Append(WhiteSpaceRegex);
                }
                expressionBuilder.Length -= WhiteSpaceRegex.Length; // remove last WhiteSpaceRegex
            }

            var itemValue = item.Value;
            if (!string.IsNullOrWhiteSpace(itemValue))
            {
                expressionBuilder.AppendFormat("({0})", Regex.Escape(itemValue.Trim()));
            }

            if (itemTag != null || include != null || pos != null || !string.IsNullOrEmpty(repeat))
                expressionBuilder.Append(")");

            expressionBuilder.Append(repeat);
        }

        /// <summary>
        /// Converts one-of element to the regular expression
        /// </summary>
        /// <param name="oneOf">One-of element to convert</param>
        /// <param name="expressionBuilder">StringBuilder to put converted expression to</param>
        /// <returns>true if element is successfully converted; otherwise, false</returns>
        private void ParseOneOf(XElement oneOf, StringBuilder expressionBuilder)
        {
            var repeat = ExtractRepeatAttribute(oneOf);
            var include = oneOf.Attribute(GrammarElements.IncludeAttribute);
            var pos = oneOf.Attribute(GrammarElements.PosAttribute);

            var oneOfTag = ExtractAndRemoveTagElement(oneOf);

            if (oneOfTag != null || include != null || pos != null || !string.IsNullOrEmpty(repeat))
            {
                var partName = string.Format(RegexPartTemplate, _markedPartsCount);

                expressionBuilder.AppendFormat("(?<{0}>", partName);

                if (oneOfTag != null)
                {
                    var key = partName;
                    var script = oneOfTag.Value;

                    _tagScripts.Add(new Tuple<string, string>(key, script));
                }

                if (include != null)
                {
                    bool includeValue;
                    if (bool.TryParse(include.Value, out includeValue))
                    {
                        if (!includeValue)
                        {
                            _includeRestrictions.Add(partName);
                        }
                    }
                }

                if (pos != null)
                {
                    _posRestrictions.Add(partName, pos.Value);
                }

                _markedPartsCount++;
            }

            expressionBuilder.Append("(");                

            var oneOfElements = oneOf.Elements().ToList();
            if (oneOfElements.Count > 0)
            {
                foreach (var oneOfElement in oneOfElements)
                {
                    switch (oneOfElement.Name.LocalName)
                    {
                        case GrammarElements.Item:
                            {
                                ParseItem(oneOfElement, expressionBuilder);

                                break;
                            }
                        case GrammarElements.OneOf:
                            {
                                ParseOneOf(oneOfElement, expressionBuilder);

                                break;
                            }
                        case GrammarElements.Ruleref:
                            {
                                ParseRuleref(oneOfElement, expressionBuilder);

                                break;
                            }
                    }
                    expressionBuilder.Append('|');
                }
                expressionBuilder.Length -= 1; // remove last '|' delimiter
            }

            expressionBuilder.Append(")");

            if (oneOfTag != null || include != null || pos != null || !string.IsNullOrEmpty(repeat))
                expressionBuilder.Append(")");

            expressionBuilder.Append(repeat);
        }

        /// <summary>
        /// Converts given rule resource element to regular expression and extracts required actions/additional restrictions
        /// </summary>
        /// <param name="resource">Resource element to convert</param>
        /// <returns>true if element is successfully converted; otherwise, false</returns>
        public override void Parse(XElement resource)
        {
            if (resource == null)
                throw new ArgumentNullException("resource");

            base.Parse(resource);

            var typeAttribute = resource.Attribute(GrammarElements.TypeAttribute);
            if (typeAttribute == null)
                throw new TimexException(string.Format("Incorrect format. Rule {0}. Rule type is not specified", Id));

            TemporalType ruleType;
            if (!Enum.TryParse(typeAttribute.Value, true, out ruleType))
                throw new TimexException(string.Format("Incorrect format. Rule {0}. Rule type is not valid", Id));

            RuleType = ruleType;

            // 1. create and compile combined regular expression
            var expressionBuilder = new StringBuilder();

            var ruleElements = resource.Elements().ToList();
            if (ruleElements.Count > 0)
            {
                foreach (var ruleElement in ruleElements)
                {
                    switch (ruleElement.Name.LocalName)
                    {
                        case GrammarElements.Item:
                            {
                                ParseItem(ruleElement, expressionBuilder);

                                break;
                            }
                        case GrammarElements.OneOf:
                            {
                                ParseOneOf(ruleElement, expressionBuilder);

                                break;
                            }
                        case GrammarElements.Ruleref:
                            {
                                ParseRuleref(ruleElement, expressionBuilder);

                                break;
                            }
                    }
                    expressionBuilder.Append(WhiteSpaceRegex);
                }
                expressionBuilder.Length -= WhiteSpaceRegex.Length; // remove last WhiteSpaceRegex
            }

            _expression = expressionBuilder.ToString();
            _compiledExpression = new Regex(_expression, _regexOptions);

            _exactExpression = BeginningOfStringRegex + _expression + EndOfStringRegex;
            _compiledExactExpression = new Regex(_exactExpression, _regexOptions);

            var exampleAttribute = resource.Attribute(GrammarElements.ExampleAttribute);
            if (exampleAttribute == null)
                throw new TimexException(string.Format("Incorrect format. Rule {0}. Rule example is not specified", Id));

            if (!_compiledExactExpression.IsMatch(exampleAttribute.Value) ||
                !_compiledExpression.IsMatch(exampleAttribute.Value))
                throw new TimexException(string.Format("Incorrect format. Rule {0}. Rule does not match specified example", Id));
            

            // 2. compile combined action list
            foreach (var tagScript in _tagScripts)
            {
                _tagActionProvider.AppendMethod(Id, tagScript.Item1, tagScript.Item2);
            }
        }

        /// <summary>
        /// Matches the whole text to rule regular expression and executes appropriate actions/applies restrictions
        /// </summary>
        /// <param name="text">Text to match</param>
        /// <returns>RuleMatch object that contains matched expression and extracted values</returns>
        public RuleMatch RuleMatch(string text)
        {
            if (text == null)
            {
                return null;
            }

            RuleMatch ruleMatch = null;

            // apply exact compiled expression
            var match = _compiledExactExpression.Match(text);
            if (match.Success)
            {
                ruleMatch = ToRuleMatch(match);
            }

            return ruleMatch;
        }

        /// <summary>
        /// Searches the specified input text for all occurrences of a rule regular expression and executes appropriate actions/applies restrictions
        /// </summary>
        /// <param name="text">Text to search for time expressions</param>
        /// <returns>RuleMatch object that contains matched expression and extracted values</returns>
        public IList<RuleMatch> RuleMatches(string text)
        {
            if (text == null)
                return null;

            var ruleMatches = new List<RuleMatch>();

            // apply compiled expression
            var matches = _compiledExpression.Matches(text);
            foreach (Match match in matches)
            {
                var ruleMatch = ToRuleMatch(match);
                ruleMatches.Add(ruleMatch);
            }

            return ruleMatches;
        }

        /// <summary>
        /// Executes appropriate actions/applies restrictions for the match found and creates RuleMatch object
        /// </summary>
        /// <param name="match">Regex match</param>
        /// <returns>RuleMatch object</returns>
        private RuleMatch ToRuleMatch(Match match)
        {
            if (match == null)
                return null;

            // Use lazy instantiation of the tag actions list
            // This makes the assumption that, somewhere between parsing the grammar and calling RuleMatch(),
            // the _scriptProvider for this resource was compiled so that it can provide binary methods immediately.
            if (_tagActions == null)
            {
                lock (this)
                {
                    if (_tagActions == null)
                    {
                        Dictionary<string, TagAction> newTagActions = new Dictionary<string, TagAction>();
                        foreach (var tagScriptKey in _tagScripts)
                        {
                            // Take the compiled methods from the scriptProvider and load them as a set of delegates.
                            // These delegate methods are what actually executes in order to process the timex match.
                            var tagAction = _tagActionProvider.GetMethod(Id, tagScriptKey.Item1);
                            newTagActions.Add(tagScriptKey.Item1, tagAction);
                        }

                        _tagActions = newTagActions;
                    }
                }
            }

            var ruleMatch = new RuleMatch
                {
                    Value = match.Value,
                    Index = match.Index,
                    Rule = this
                };

            // execute corresponding action for each matched group
            foreach (var tagAction in _tagActions)
            {
                var matchedGroup = match.Groups[tagAction.Key];
                if (matchedGroup.Success)
                {
                    var value = matchedGroup.Value;

                    if (!string.IsNullOrEmpty(value))
                    {
                        tagAction.Value(ruleMatch.TimexDictionary, value);
                    }
                }
            }

            // remove fragments that needs to be removed by include restrictions
            int index = 0;
            int length = 0;
            for (int i = _includeRestrictions.Count - 1; i >= 0; i--)
            {
                var includeRestriction = _includeRestrictions[i];
                
                var group = match.Groups[includeRestriction];
                if (group.Success)
                {
                    var groupLength = group.Length;
                    var groupIndex = group.Index - match.Index;

                    var spaceLength = 0;
                    var possibleSpaceIndex = groupIndex + groupLength;
                    var possibleSpaceLength = index - possibleSpaceIndex;
                    if (possibleSpaceLength > 0)
                    {
                        var possibleSpace = ruleMatch.Value.Substring(possibleSpaceIndex, possibleSpaceLength);

                        if (possibleSpace.Trim(TrimmedChars).Length == 0)
                            spaceLength = possibleSpaceLength;
                    }

                    if (index == 0 && length == 0)
                    {
                        length = groupLength;
                        index = groupIndex;
                    }
                    else if (groupIndex + groupLength + spaceLength >= index)
                    {
                        length = index + length - groupIndex;
                        index = groupIndex;
                    }
                    else
                    {
                        ruleMatch.Value = ruleMatch.Value.Remove(index, length);
                        if (index == 0)
                            ruleMatch.Index += length;

                        index = groupIndex;
                        length = groupLength;
                    }
                }
            }
            ruleMatch.Value = ruleMatch.Value.Remove(index, length);
            if (index == 0)
                ruleMatch.Index += length;

            // trim
            var trimmedValue = ruleMatch.Value.Trim(TrimmedChars);
            var trimmedIndex = ruleMatch.Value.IndexOf(trimmedValue, StringComparison.Ordinal);
            
            ruleMatch.Value = trimmedValue;
            if (trimmedIndex > 0)
                ruleMatch.Index += trimmedIndex;

            // TODO: apply pos restrictions for each matched group

            return ruleMatch;
        }
    }
}
