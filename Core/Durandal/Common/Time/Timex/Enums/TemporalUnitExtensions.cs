using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Time.Timex.Enums
{
    public static class TemporalUnitExtensions
    {
        // These are the hardcoded duration values that are associated with each temporalUnit internally
        private static long SECOND = 1L;
        private static long MINUTE = SECOND * 60L;
        private static long HOUR = MINUTE * 60L;
        private static long DAY = HOUR * 24L;
        private static long WEEK = DAY * 7L;
        private static long FORTNIGHT = DAY * 14L;
        private static long MONTH = DAY * 30L; // A little more controversial: Each month is 30 days exactly.
        private static long QUARTER = MONTH * 3L; // A quarter is 3 months
        private static long YEAR = DAY * 365L; // Each year is always 365 days. Don't account for leap years or anything
        private static long DECADE = YEAR * 10L;
        private static long CENTURY = YEAR * 100L;

        /// <summary>
        /// Returns true if this temporal unit is a day, specific or otherwise.
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        public static bool IsDay(this TemporalUnit? unit)
        {
            if (unit == null)
                return false;

            return unit == TemporalUnit.Day || IsWeekday(unit);
        }

        /// <summary>
        /// Returns true if this temporal unit is a day of the week
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        public static bool IsWeekday(this TemporalUnit? unit)
        {
            if (unit == null)
                return false;

            return unit == TemporalUnit.Monday ||
                unit == TemporalUnit.Tuesday ||
                unit == TemporalUnit.Wednesday ||
                unit == TemporalUnit.Thursday ||
                unit == TemporalUnit.Friday ||
                unit == TemporalUnit.Saturday ||
                unit == TemporalUnit.Sunday;
        }

        /// <summary>
        /// If this temporal unit represents a day of the week, return the actual DayOfWeek value that represents it
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        public static DayOfWeek ConvertIntoDayOfWeek(this TemporalUnit unit)
        {
            switch (unit)
            {
                case TemporalUnit.Monday:
                    return DayOfWeek.Monday;
                case TemporalUnit.Tuesday:
                    return DayOfWeek.Tuesday;
                case TemporalUnit.Wednesday:
                    return DayOfWeek.Wednesday;
                case TemporalUnit.Thursday:
                    return DayOfWeek.Thursday;
                case TemporalUnit.Friday:
                    return DayOfWeek.Friday;
                case TemporalUnit.Saturday:
                    return DayOfWeek.Saturday;
                case TemporalUnit.Sunday:
                    return DayOfWeek.Sunday;
            }

            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Cannot convert temporal unit {0} into a day of week!", unit.ToString()));
        }

        /// <summary>
        /// Accepts a numerical day of week (monday == 1) and parses that into the corresponding TemporalUnit for that weekday
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Naming", "CA1704")]
        [SuppressMessage("Microsoft.Usage", "CA2233")]
        public static TemporalUnit ParseDayOfWeekNum(int num)
        {
            switch (num)
            {
                case 1:
                    return TemporalUnit.Monday;
                case 2:
                    return TemporalUnit.Tuesday;
                case 3:
                    return TemporalUnit.Wednesday;
                case 4:
                    return TemporalUnit.Thursday;
                case 5:
                    return TemporalUnit.Friday;
                case 6:
                    return TemporalUnit.Saturday;
                case 7:
                    return TemporalUnit.Sunday;
            }

            throw new ArgumentException("Cannot convert numerical day-of-week " + num + " into a temporal unit!", nameof(num));
        }

        /// <summary>
        /// Converts a nullable temporal unit into the duration that it spans, in seconds
        /// "Minute" => 60, "Hour" => 3600, etc.
        /// </summary>
        public static long ToDuration(this TemporalUnit? unit)
        {
            if (!unit.HasValue)
            {
                throw new ArgumentNullException(nameof(unit), "Null temporal unit cannot be converted into duration");
            }

            return unit.Value.ToDuration();
        }

        /// <summary>
        /// Converts a temporal unit into the duration that it spans, in seconds
        /// "Minute" => 60, "Hour" => 3600, etc.
        /// </summary>
        public static long ToDuration(this TemporalUnit unit)
        {
            switch (unit)
            {
                case TemporalUnit.Century:
                    return CENTURY;
                case TemporalUnit.Decade:
                    return DECADE;
                case TemporalUnit.Year:
                    return YEAR;
                case TemporalUnit.Quarter:
                    return QUARTER;
                case TemporalUnit.Month:
                    return MONTH;
                case TemporalUnit.Week:
                    return WEEK;
                case TemporalUnit.Fortnight:
                    return FORTNIGHT;
                case TemporalUnit.BusinessDay:
                case TemporalUnit.Day:
                    return DAY;
                case TemporalUnit.Hour:
                    return HOUR;
                case TemporalUnit.Minute:
                    return MINUTE;
                case TemporalUnit.Second:
                    return SECOND;
            }

            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Cannot produce a duration value; unsupported TemporalUnit {0}", unit.ToString()));
        }
    }
}
