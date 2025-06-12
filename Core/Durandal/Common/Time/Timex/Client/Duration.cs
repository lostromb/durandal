namespace Durandal.Common.Time.Timex.Client
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    using Durandal.Common.Time.Timex.Enums;

    /// <summary>
    /// Represents a single span of time, which may be defined in vague terms (as in "PXM" for "A few months")
    /// </summary>
    public class Duration : TimexValue
    {
        // These are the hardcoded duration values that are associated with each temporalUnit internally
        private const long SECOND = 1L;
        private const long MINUTE = SECOND * 60L;
        private const long HOUR = MINUTE * 60L;
        private const long DAY = HOUR * 24L;
        private const long WEEK = DAY * 7L;
        private const long MONTH = DAY * 30L; // A little more controversial: Each month is 30 days exactly.
        private const long YEAR = DAY * 365L; // Each year is always 365 days. Don't account for leap years or anything

        /// <summary>
        /// This contains all of the components of the duration as they were originally parsed.
        /// </summary>
        private readonly IDictionary<TemporalUnit, int?> durationComponents = new Dictionary<TemporalUnit, int?>();
        
        public override TemporalType GetTemporalType()
        {
            return TemporalType.Duration;
        }
        
        /// <summary>
        /// Parses an ISO duration value (in the form of "P3D" or "PT5H30M") into a set of values in the durationComponents dictionary.
        /// </summary>
        /// <param name="isoDurationString">The ISO duration value</param>
        internal void ParseDurationValue(string isoDurationString)
        {
            var parsedComponents = DateTimeParserHelpers.ParseIsoDuration(isoDurationString);

            foreach (var component in parsedComponents)
            {
                durationComponents.Add(component.Key, component.Value);
            }
        }

        /// <summary>
        /// Returns all of the components that make up this duration value. Each component is a temporal unit, such as "hour",
        /// combined with either a numerical value, or null, which indicates that the original expression was value. So, for example,
        /// "PT3H" would yield { Hour | 3 }
        /// "PXD" would yield { Day | Null }
        /// And so forth for arbitrary combinations of components.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1006")]
        public IDictionary<TemporalUnit, int?> DurationComponents
        {
            get { return durationComponents; }
        }

        /// <summary>
        /// Determines if this duration value is vaguely defined, meaning that "X" was used to fill in some of its numerical values
        /// </summary>
        /// <returns>True if this duration is vague. If so, it cannot be represented as a regular TimeSpan</returns>
        public bool IsVague()
        {
            foreach (var component in durationComponents)
            {
                if (!component.Value.HasValue)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Applies this duration as a positive offset to the given reference time, and returns the modified time.
        /// Note that if IsVague() == true, this method has undefined behavior (what that really means is it will just skip vaguely defined fields)
        /// </summary>
        /// <param name="referenceDateTime">The reference datetime</param>
        /// <returns>The reference time + offset</returns>
        public DateTime ApplyOffsetToDateTime(DateTime referenceDateTime)
        {
            DateTime returnVal = referenceDateTime;

            foreach (var component in durationComponents)
            {
                if (!component.Value.HasValue)
                {
                    continue;
                }

                switch (component.Key)
                {
                    case TemporalUnit.Year:
                        returnVal = returnVal.AddYears(component.Value.Value);
                        break;
                    case TemporalUnit.Month:
                        returnVal = returnVal.AddMonths(component.Value.Value);
                        break;
                    case TemporalUnit.Week:
                        returnVal = returnVal.AddDays(component.Value.Value * 7);
                        break;
                    case TemporalUnit.Day:
                        returnVal = returnVal.AddDays(component.Value.Value);
                        break;
                    case TemporalUnit.Hour:
                        returnVal = returnVal.AddHours(component.Value.Value);
                        break;
                    case TemporalUnit.Minute:
                        returnVal = returnVal.AddMinutes(component.Value.Value);
                        break;
                    case TemporalUnit.Second:
                        returnVal = returnVal.AddSeconds(component.Value.Value);
                        break;
                }
            }

            return returnVal;
        }

        /// <summary>
        /// Attempts to convert this duration into a C# TimeSpan object.
        /// !!!!WARNING!!!! This method loses accuracy for durations longer than 1 week, because months and years have inconsistent lengths
        /// that depend on context. For the sake of simplicity, "1 month" is always represented as 30 days, and "1 year" is always 365 days.
        /// If you need accurate offset calculations at this level, use ApplyOffsetToDateTime() and provide a reference time.
        /// Alternatively, use DurationComponents directly and apply them yourself. This may be necessary in cases where the actual
        /// units are important, i.e. you need to distinguish between "1 hour" and "60 minutes".
        /// </summary>
        /// <returns>The converted TimeSpan, or null if this duration is vague</returns>
        public TimeSpan? TryConvertIntoCSharpTimeSpan()
        {
            long totalSeconds = 0;

            foreach (var component in durationComponents)
            {
                if (!component.Value.HasValue)
                {
                    return null;
                }
                
                switch (component.Key)
                {
                    case TemporalUnit.Year:
                        totalSeconds += YEAR * component.Value.Value;
                        break;
                    case TemporalUnit.Month:
                        totalSeconds += MONTH * component.Value.Value;
                        break;
                    case TemporalUnit.Week:
                        totalSeconds += WEEK * component.Value.Value;
                        break;
                    case TemporalUnit.Day:
                        totalSeconds += DAY * component.Value.Value;
                        break;
                    case TemporalUnit.Hour:
                        totalSeconds += HOUR * component.Value.Value;
                        break;
                    case TemporalUnit.Minute:
                        totalSeconds += MINUTE * component.Value.Value;
                        break;
                    case TemporalUnit.Second:
                        totalSeconds += SECOND * component.Value.Value;
                        break;
                }
            }

            return new TimeSpan(totalSeconds * 10000000);
        }
    }
}
