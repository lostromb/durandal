using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Durandal.Common.Time.Timex.Constants;

namespace Durandal.Common.Time.Timex.Enums
{
    [SuppressMessage("Microsoft.Naming", "CA1714")]
    [Flags]
    public enum TemporalType
    {
        None = 0x00,

        /// <summary>
        /// Date expressions, e.g. "January 15th, 2001"
        /// </summary>
        Date = 0x01,

        /// <summary>
        /// Time expressions, e.g. "half past noon" 
        /// </summary>
        Time = 0x02,

        /// <summary>
        /// Set expressions, e.g. "twice a week"
        /// </summary>
        Set = 0x04,

        /// <summary>
        /// Duration expressions, e.g. "20 days"
        /// </summary>
        Duration = 0x08,

        /// <summary>
        /// All expressions
        /// </summary>
        All = 0x0F
    }

    public static class TemporalTypeExtensions
    {
        /// <summary>
        /// Returns true if this is either TemporalType.Date or TemporalType.Time
        /// </summary>
        public static bool IsDateOrTime(this TemporalType type)
        {
            return type.HasFlag(TemporalType.Date) || type.HasFlag(TemporalType.Time);
        }

        /// <summary>
        /// Infers a temporal type that would result from a given TimexDictionary
        /// </summary>
        public static TemporalType InferTemporalType(IDictionary<string, string> timexDictionary)
        {
            // Validate arguments
            if (timexDictionary == null)
            {
                return TemporalType.None;
            }

            TemporalType returnVal = TemporalType.Date;
            if (timexDictionary.ContainsKey(Iso8601.Hour) ||
                timexDictionary.ContainsKey(Iso8601.Minute) ||
                timexDictionary.ContainsKey(Iso8601.Second) ||
                timexDictionary.ContainsKey(Iso8601.PartOfDay))
            {
                returnVal = TemporalType.Time;
            }
            if (timexDictionary.ContainsKey(TimexAttributes.OffsetUnit))
            {
                TemporalUnit offsetUnit;
                if (EnumExtensions.TryParse(timexDictionary[TimexAttributes.OffsetUnit], out offsetUnit))
                {
                    if (offsetUnit == TemporalUnit.Hour ||
                        offsetUnit == TemporalUnit.Minute ||
                        offsetUnit == TemporalUnit.Second)
                    {
                        returnVal = TemporalType.Time;
                    }
                }
            }
            if (timexDictionary.ContainsKey(TimexAttributes.Duration) ||
                timexDictionary.ContainsKey(TimexAttributes.DurationUnit))
            {
                returnVal = TemporalType.Duration;
            }
            if (timexDictionary.ContainsKey(TimexAttributes.Frequency) ||
                timexDictionary.ContainsKey(TimexAttributes.Quantity))
            {
                returnVal = TemporalType.Set;
            }

            return returnVal;
        }
    }
}
