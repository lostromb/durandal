namespace Durandal.Common.Time.Timex.Actions.Interpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.Common.Time.Timex.Resources;

    /// <summary>
    /// The other simplest type of expression. Echoes the input value
    /// </summary>
    public class ValueExpression : IExpression
    {
        private static readonly Regex parser = new Regex("^\\s*value\\s*$");

        internal ValueExpression() { }

        public string Evaluate(IDictionary<string, NormalizationResource> normalizationResources, IDictionary<string, string> timexDict, string input)
        {
            return input;
        }

        public static ValueExpression TryParse(string thisExpression, string ruleId)
        {
            if (parser.Match(thisExpression).Success)
            {
                return new ValueExpression();
            }

            return null;
        }
    }
}
