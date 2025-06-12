using System;
using System.Collections.Generic;
using System.Globalization;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// Represents the results from a time expression match.
    /// </summary>
    public class TimexMatch
    {
        public TimexMatch(ExtendedDateTime time = null)
        {
            Id = 0;
            ExtendedDateTime = time;
            Index = 0;
            MergedIds = new List<int>();
            Value = string.Empty;
            RuleId = string.Empty;
        }

        /// <summary>
        /// Ordinal number of a match
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Extended DateTime information for a match
        /// </summary>
        public ExtendedDateTime ExtendedDateTime { get; set; }

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

        /// <summary>
        /// List of IDs of all matches (including this one) contributing to this match
        /// </summary>
        public List<int> MergedIds { get; internal set; }

        public TimexMatch Clone()
        {
            return new TimexMatch
            {
                ExtendedDateTime = this.ExtendedDateTime.Reinterpret(this.ExtendedDateTime.Context),
                Id = this.Id,
                Index = this.Index,
                MergedIds = new List<int>(this.MergedIds),
                RuleId = this.RuleId,
                Value = this.Value
            };
        }

        /// <summary>
        /// Returns TimexTag that represents current TimexMatch object
        /// </summary>
        /// <returns>TimexTag object</returns>
        public TimexTag ToTimexTag()
        {
            return new TimexTag
            {
                Id = Id.ToString(CultureInfo.InvariantCulture),
                Text = Value,
                TimexType = ExtendedDateTime.FormatType(),
                Value = ExtendedDateTime.FormatValue(),
                Mod = ExtendedDateTime.FormatMod(),
                Frequency = ExtendedDateTime.FormatFrequency(),
                Quantity = ExtendedDateTime.FormatQuantity(),
                Comment = ExtendedDateTime.FormatComment()
            };
        }
    }
}
