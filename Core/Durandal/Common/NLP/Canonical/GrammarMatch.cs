namespace Durandal.Common.NLP.Canonical
{
    /// <summary>
    /// Represents the results from a time expression match.
    /// </summary>
    public class GrammarMatch
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
        /// The RuleID that generated this match
        /// </summary>
        public string RuleId { get; set; }

        public string NormalizedValue { get; set; }
    }
}
