namespace Durandal.Common.Time.Timex.Actions.Interpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.Common.Time.Timex.Resources;

    /// <summary>
    /// Represents a val1 + val2 expression, which in our parlance always means string concatenation
    /// </summary>
    public class ConcatExpression : IExpression
    {
        private static readonly Regex parser = new Regex("^\\s*(.+)\\s*\\+\\s*(.+)\\s*$");

        // For debugging
        private readonly string _ruleId;

        private readonly IExpression _lhs;
        private readonly IExpression _rhs;

        private ConcatExpression(IExpression lhs, IExpression rhs, string ruleId)
        {
            _lhs = lhs;
            _rhs = rhs;
            _ruleId = ruleId;
        }

        public string Evaluate(IDictionary<string, NormalizationResource> normalizationResources, IDictionary<string, string> timexDict, string input)
        {
            string lhsResult = _lhs.Evaluate(normalizationResources, timexDict, input);
            string rhsResult = _rhs.Evaluate(normalizationResources, timexDict, input);
            return lhsResult + rhsResult;
        }

        public static IExpression TryParse(string thisExpression, string ruleId)
        {
            Match m = parser.Match(thisExpression);
            if (m.Success)
            {
                string lhs = m.Groups[1].Value;
                string rhs = m.Groups[2].Value;
                IExpression compiledLhs = ScriptParser.ParseExpression(lhs, ruleId);
                IExpression compiledRhs = ScriptParser.ParseExpression(rhs, ruleId);
                return new ConcatExpression(compiledLhs, compiledRhs, ruleId);
            }

            return null;
        }
    }
}
