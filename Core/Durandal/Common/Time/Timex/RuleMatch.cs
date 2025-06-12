using System.Collections.Generic;
using Durandal.Common.Time.Timex.Resources;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// Represents the results of a rule match
    /// </summary>
    public class RuleMatch
    {
        private readonly IDictionary<string, string> _timexDictionary =
            new Dictionary<string, string>();

        /// <summary>
        /// The position in the original string where the first character of the captured substring is found.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Captured string
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Rule used to capture string
        /// </summary>
        public RuleResource Rule { get; set; }

        /// <summary>
        /// Extracted timex parts
        /// </summary>
        public IDictionary<string, string> TimexDictionary 
        {
            get { return _timexDictionary; }
        }
    }
}
