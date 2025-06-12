using System.Collections.Generic;

namespace Durandal.Common.NLP.Canonical
{
    /// <summary>
    /// Represents the results of a rule match
    /// </summary>
    public class RuleMatch
    {
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
        /// The extracted tag after normalization
        /// </summary>
        public string NormalizedTagValue { get; set; }
    }
}
