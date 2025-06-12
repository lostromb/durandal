namespace Durandal.Common.Time.Timex.Actions.Interpreter
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.Common.Time.Timex.Resources;

    /// <summary>
    /// Returns a value from the timex dictionary. If the value is not found at runtime, this throws an exception
    /// </summary>
    public class ArrayLookupExpression : IExpression
    {
        private static readonly Regex parser = new Regex("^\\s*timex\\[\\s*\"(.+?)\"\\s*\\]\\s*$");

        // For debugging
        private readonly string _ruleId;

        private readonly string _key;

        private ArrayLookupExpression(string key, string ruleId)
        {
            _key = key;
            _ruleId = ruleId;
        }

        public string Evaluate(IDictionary<string, NormalizationResource> normalizationResources, IDictionary<string, string> timexDict, string input)
        {
            string returnVal;
            if (timexDict.TryGetValue(_key, out returnVal))
            {
                return returnVal;
            }

            throw new TimexException("Attempted to retrieve nonexistent value \"" + _key + "\" from timex dictionary", _ruleId);
        }

        public static IExpression TryParse(string thisExpression, string ruleId)
        {
            Match m = parser.Match(thisExpression);
            if (m.Success)
            {
                string key = m.Groups[1].Value;
                return new ArrayLookupExpression(key, ruleId);
            }

            return null;
        }
    }
}
