namespace Durandal.Common.Time.Timex.Actions.Interpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.Common.Time.Timex.Resources;

    /// <summary>
    /// Represents the operation Substring("#normRule", offset(, length))
    /// </summary>
    public class SubstringExpression : IExpression
    {
        private static readonly Regex threeParameterParser = new Regex("^\\s*Substring\\((.+),\\s*([0-9]+?)\\s*,\\s*([0-9]+?)\\s*\\)\\s*$");
        //private static readonly Regex twoParameterParser = new Regex("^\\s*Substring\\((.+),\\s*([0-9]+?)\\s*\\)\\s*$");

        // For debugging
        private readonly string _ruleId;

        private readonly IExpression _lhs;
        private readonly int _startIdx;
        private readonly int _length;

        private SubstringExpression(IExpression lhs, int startIdx, int length, string ruleId)
        {
            _lhs = lhs;
            _startIdx = startIdx;
            _length = length;
            _ruleId = ruleId;
        }

        public string Evaluate(IDictionary<string, NormalizationResource> normalizationResources, IDictionary<string, string> timexDict, string input)
        {
            string leftEval = _lhs.Evaluate(normalizationResources, timexDict, input);

            if (_startIdx >= leftEval.Length)
            {
                throw new TimexException("Invalid bounds on Substring(" + leftEval + "," + _startIdx + ")", _ruleId);
            }

            if (_length >= 0)
            {
                return leftEval.Substring(_startIdx, _length);
            }

            return leftEval.Substring(_startIdx);
        }

        public static IExpression TryParse(string thisExpression, string ruleId)
        {
            int start, length;
            Match m = threeParameterParser.Match(thisExpression);
            if (m.Success &&
                int.TryParse(m.Groups[2].Value, out start) &&
                int.TryParse(m.Groups[3].Value, out length))
            {
                string lhs = m.Groups[1].Value;
                IExpression compiledLhs = ScriptParser.ParseExpression(lhs, ruleId);
                return new SubstringExpression(compiledLhs, start, length, ruleId);
            }

            /*m = twoParameterParser.Match(thisExpression);
            if (m.Success &&
                int.TryParse(m.Groups[2].Value, out start))
            {
                string lhs = m.Groups[1].Value;
                IExpression compiledLhs = ScriptParser.ParseExpression(lhs);
                return new SubstringExpression(compiledLhs, start);
            }*/

            return null;
        }
    }
}
