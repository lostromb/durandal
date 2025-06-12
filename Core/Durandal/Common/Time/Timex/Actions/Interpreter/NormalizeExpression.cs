namespace Durandal.Common.Time.Timex.Actions.Interpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.Common.Time.Timex.Resources;

    /// <summary>
    /// Represents the operation Normalize("#normRule", value)
    /// </summary>
    public class NormalizeExpression : IExpression
    {
        private static readonly Regex parser = new Regex("^\\s*Normalize\\(\\\"?#?([^\\\"]+)\\\"?,(.+?)\\)\\s*$");

        // For debugging
        private readonly string _ruleId;

        private readonly string _resourceId;
        private readonly IExpression _rhs;

        private NormalizeExpression(string resourceId, IExpression rhs, string ruleId)
        {
            _resourceId = resourceId;
            _rhs = rhs;
            _ruleId = ruleId;
        }

        public string Evaluate(IDictionary<string, NormalizationResource> normalizationResources, IDictionary<string, string> timexDict, string input)
        {
            string rightEval = _rhs.Evaluate(normalizationResources, timexDict, input);

            if (normalizationResources.ContainsKey(_resourceId))
            {
                return normalizationResources[_resourceId].Normalize(rightEval);
            }

            throw new TimexException("A script referenced an unknown normalization rule \"" + _resourceId + "\"", _ruleId);
        }

        public static IExpression TryParse(string thisExpression, string ruleId)
        {
            Match m = parser.Match(thisExpression);
            if (m.Success)
            {
                string resourceId = m.Groups[1].Value;
                string rhs = m.Groups[2].Value;
                IExpression compiledRhs = ScriptParser.ParseExpression(rhs, ruleId);
                return new NormalizeExpression(resourceId, compiledRhs, ruleId);
            }

            return null;
        }
    }
}
