namespace Durandal.Common.Time.Timex.Actions.Interpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.Common.Time.Timex.Resources;

    /// <summary>
    /// Represents the Sum(val1, val2) functor, which always implies numerical operation
    /// </summary>
    public class SumExpression : IExpression
    {
        private static readonly Regex parser = new Regex("^\\s*Sum\\((.+)\\)\\s*$");

        // For debugging
        private readonly string _ruleId;

        private readonly IExpression _lhs;
        private readonly IExpression _rhs;

        private SumExpression(IExpression lhs, IExpression rhs, string ruleId)
        {
            _lhs = lhs;
            _rhs = rhs;
            _ruleId = ruleId;
        }

        public string Evaluate(IDictionary<string, NormalizationResource> normalizationResources, IDictionary<string, string> timexDict, string input)
        {
            string lhsResult = _lhs.Evaluate(normalizationResources, timexDict, input);
            string rhsResult = _rhs.Evaluate(normalizationResources, timexDict, input);

            long l, r;
            if (long.TryParse(lhsResult, out l) && long.TryParse(rhsResult, out r))
            {
                return (l + r).ToString();
            }

            throw new TimexException("Cannot add non-numerical values " + lhsResult + " and " + rhsResult, _ruleId);
        }

        public static IExpression TryParse(string thisExpression, string ruleId)
        {
            Match m = parser.Match(thisExpression);
            if (m.Success)
            {
                string parameters = m.Groups[1].Value;
                int parameterSeparator = ScriptParser.SplitTwoParametersWithMatchedParentheses(parameters);
                if (parameterSeparator < 0)
                {
                    throw new TimexException(ruleId + ": Could not parse parameter set:" + parameters);
                }

                string lhs = parameters.Substring(0, parameterSeparator);
                string rhs = parameters.Substring(parameterSeparator + 1);
                IExpression compiledLhs = ScriptParser.ParseExpression(lhs, ruleId);
                IExpression compiledRhs = ScriptParser.ParseExpression(rhs, ruleId);
                return new SumExpression(compiledLhs, compiledRhs, ruleId);
            }

            return null;
        }
    }
}
