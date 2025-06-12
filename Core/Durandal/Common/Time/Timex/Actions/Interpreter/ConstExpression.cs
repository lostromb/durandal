namespace Durandal.Common.Time.Timex.Actions.Interpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.Common.Time.Timex.Resources;

    /// <summary>
    /// The simplest type of expression. Returns a constant value
    /// </summary>
    public class ConstExpression : IExpression
    {
        private static readonly Regex parser = new Regex("^\\s*\\\"?([^\\\"]+)\\\"?\\s*$");

        private readonly string _value;

        internal ConstExpression(string value)
        {
            _value = value;
        }

        public string Evaluate(IDictionary<string, NormalizationResource> normalizationResources, IDictionary<string, string> timexDict, string input)
        {
            return _value;
        }

        public static IExpression TryParse(string thisExpression, string ruleId)
        {
            Match m = parser.Match(thisExpression);
            if (m.Success)
            {
                string value = m.Groups[1].Value;

                return new ConstExpression(value);
            }

            return null;
        }
    }
}
