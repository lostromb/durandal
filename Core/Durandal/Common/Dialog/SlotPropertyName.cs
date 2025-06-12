namespace Durandal.Common.Dialog
{
    /// <summary>
    /// Static holder class for common slot property (annotation) keys.
    /// </summary>
    public static class SlotPropertyName
    {
        /// <summary>
        /// The start index of this slot within the entire utterance
        /// </summary>
        public static readonly string StartIndex = "StartIndex";

        /// <summary>
        /// The string length of this slot value
        /// </summary>
        public static readonly string StringLength = "StringLength";

        /// <summary>
        /// Any timex match information included in this value.
        /// Expressed in a list as TimexMatch0, TimexMatch1, etc.
        /// </summary>
        public static readonly string TimexMatch = "TimexMatch";

        /// <summary>
        /// Any possible ordinal reference (like "the first") that was made within this expression
        /// </summary>
        public static readonly string Ordinal = "Ordinal";

        /// <summary>
        /// Numbers contained in this expression
        /// </summary>
        public static readonly string Number = "Number";

        /// <summary>
        /// Spell correction suggestions for this slot value
        /// </summary>
        public static readonly string SpellSuggestions = "SpellSuggestions";
        
        /// <summary>
        /// Resolved ontological entities referred to by this slot
        /// </summary>
        public static readonly string Entity = "Entity";

        /// <summary>
        /// If canonicalization triggered, the raw value will be kept here
        /// </summary>
        public static readonly string NonCanonicalValue = "NonCanonicalValue";

        /// <summary>
        /// If a plugin augments the query and returns it to the client, augmented slot values are stored here
        /// </summary>
        public static readonly string AugmentedValue = "AugmentedValue";
    }
}
