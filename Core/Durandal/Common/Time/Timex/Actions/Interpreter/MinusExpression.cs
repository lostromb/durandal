namespace Durandal.Common.Time.Timex.Actions.Interpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.Common.Time.Timex.Resources;

    /// <summary>
    /// Represents the operation Minus(value1, value2) or just Minus(value)
    /// </summary>
    public class MinusExpression : IExpression
    {
        private static readonly Regex parser = new Regex("^\\s*Minus\\((.+)\\)\\s*$");

        // For debugging
        private readonly string _ruleId;

        private readonly IExpression _lhs;
        private readonly IExpression _rhs;

        private MinusExpression(IExpression lhs, IExpression rhs, string ruleId)
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
                return (l - r).ToString();
            }

            throw new TimexException("Could not parse inputs to Minus(" + lhsResult + "," + rhsResult + ")", _ruleId);
        }

        public static IExpression TryParse(string thisExpression, string ruleId)
        {
            Match m = parser.Match(thisExpression);
            if (m.Success)
            {
                string parameters = m.Groups[1].Value;
                IExpression compiledLhs;
                IExpression compiledRhs;

                // Minus is weird because it has two signatures - one parameter and two parameters
                if (parameters.Contains(","))
                {
                    // Two-parameter form
                    int parameterSeparator = ScriptParser.SplitTwoParametersWithMatchedParentheses(parameters);
                    if (parameterSeparator < 0)
                    {
                        // Never mind, it's actually 1 parameter
                        compiledLhs = new ConstExpression("0");
                        compiledRhs = ScriptParser.ParseExpression(parameters, ruleId);
                    }
                    else
                    {
                        string lhs = parameters.Substring(0, parameterSeparator);
                        string rhs = parameters.Substring(parameterSeparator + 1);
                        compiledLhs = ScriptParser.ParseExpression(lhs, ruleId);
                        compiledRhs = ScriptParser.ParseExpression(rhs, ruleId);
                    }
                }
                else
                {
                    // One-parameter form - it's just negation
                    compiledLhs = new ConstExpression("0");
                    compiledRhs = ScriptParser.ParseExpression(parameters, ruleId);
                }

                return new MinusExpression(compiledLhs, compiledRhs, ruleId);
            }

            return null;
        }
    }
}
