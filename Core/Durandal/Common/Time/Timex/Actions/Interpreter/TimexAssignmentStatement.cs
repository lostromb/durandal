

namespace Durandal.Common.Time.Timex.Actions.Interpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.Common.Time.Timex.Resources;

    public class TimexAssignmentStatement : IStatement
    {
        private static readonly Regex parser = new Regex("^\\s*timex\\[\\s*\"(.+?)\"\\s*\\]\\s*=\\s*(.+)\\s*$");

        private readonly string _dictKey;
        private readonly IExpression _expression;

        private TimexAssignmentStatement(string targetTimexKey, IExpression rhs)
        {
            _dictKey = targetTimexKey;
            _expression = rhs;
        }

        public void Evaluate(IDictionary<string, NormalizationResource> normalizationResources, IDictionary<string, string> timexDict, string inputValue)
        {
            string rhsResult = _expression.Evaluate(normalizationResources, timexDict, inputValue);
            timexDict[_dictKey] = rhsResult;
        }

        public static IStatement TryParse(string thisExpression, string ruleId)
        {
            Match m = parser.Match(thisExpression);
            if (m.Success)
            {
                string key = m.Groups[1].Value;
                string rhs = m.Groups[2].Value;

                IExpression rhsExpression = ScriptParser.ParseExpression(rhs, ruleId);

                return new TimexAssignmentStatement(key, rhsExpression);
            }

            return null;
        }
    }
}
