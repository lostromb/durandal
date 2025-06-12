using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Time.Timex.Calendar
{
    /// <summary>
    /// Gregorian calendar methods ripped out of .NetCore because that API is
    /// not actually supported in all platforms (i.e. PCL)
    /// </summary>
    public static class GregorianCalendar
    {
        // Number of 100ns (10E-7 second) ticks per time unit
        private const long TicksPerMillisecond = 10000;
        private const long TicksPerSecond = TicksPerMillisecond * 1000;
        private const long TicksPerMinute = TicksPerSecond * 60;
        private const long TicksPerHour = TicksPerMinute * 60;
        private const long TicksPerDay = TicksPerHour * 24;

        // Number of milliseconds per time unit
        private const int MillisPerSecond = 1000;
        private const int MillisPerMinute = MillisPerSecond * 60;
        private const int MillisPerHour = MillisPerMinute * 60;
        private const int MillisPerDay = MillisPerHour * 24;

        // Number of days in a non-leap year
        private const int DaysPerYear = 365;
        // Number of days in 4 years
        private const int DaysPer4Years = DaysPerYear * 4 + 1;
        // Number of days in 100 years
        private const int DaysPer100Years = DaysPer4Years * 25 - 1;
        // Number of days in 400 years
        private const int DaysPer400Years = DaysPer100Years * 4 + 1;

        // Number of days from 1/1/0001 to 1/1/10000
        private const int DaysTo10000 = DaysPer400Years * 25 - 366;

        private const long MaxMillis = (long)DaysTo10000 * MillisPerDay;

        private const int DatePartYear = 0;
        private const int DatePartDayOfYear = 1;
        private const int DatePartMonth = 2;
        private const int DatePartDay = 3;

        private static readonly int[] DaysToMonth365 =
        {
            0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365
        };

        private static readonly int[] DaysToMonth366 =
        {
            0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366
        };

        /// <summary>
        /// Returns the day-of-year part of the specified DateTime. The returned value
        /// is an integer between 1 and 366.
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static int GetDayOfYear(DateTime time)
        {
            return (GetDatePart(time.Ticks, DatePartDayOfYear));
        }

        /// <summary>
        /// Returns the day of week of the specified datetime in the Gregorian calendar
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public static DayOfWeek GetDayOfWeek(DateTime time)
        {
            return ((DayOfWeek)((int)(time.Ticks / TicksPerDay + 1) % 7));
        }

        [SuppressMessage("Microsoft.Naming", "CA1709", MessageId = "ISO")]
        public static int GetISOWeekOfYear(DateTime time)
        {
            return GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        /// <summary>
        /// Returns the week of year according to the specified rule.
        /// For ISO week you will want (CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)
        /// </summary>
        /// <param name="time"></param>
        /// <param name="rule"></param>
        /// <param name="firstDayOfWeek"></param>
        /// <returns></returns>
        public static int GetWeekOfYear(DateTime time, CalendarWeekRule rule, DayOfWeek firstDayOfWeek)
        {
            if ((int)firstDayOfWeek < 0 || (int)firstDayOfWeek > 6)
            {
                throw new ArgumentOutOfRangeException("firstDayOfWeek", "Day of week must be a valid day");
            }

            switch (rule)
            {
                case CalendarWeekRule.FirstDay:
                    return (GetFirstDayWeekOfYear(time, (int)firstDayOfWeek));
                case CalendarWeekRule.FirstFullWeek:
                    return (GetWeekOfYearFullDays(time, (int)firstDayOfWeek, 7));
                case CalendarWeekRule.FirstFourDayWeek:
                    return (GetWeekOfYearFullDays(time, (int)firstDayOfWeek, 4));
            }

            throw new ArgumentOutOfRangeException("rule", "Invalid calendar week rule");
        }

        // The minimum supported DateTime range for the calendar.
        private static DateTime MinSupportedDateTime
        {
            get
            {
                return (DateTime.MinValue);
            }
        }

        // it would be nice to make this abstract but we can't since that would break previous implementations
        private static int DaysInYearBeforeMinSupportedYear
        {
            get
            {
                return 365;
            }
        }
        
        /*=================================GetFirstDayWeekOfYear==========================
        **Action: Get the week of year using the FirstDay rule.
        **Returns:  the week of year.
        **Arguments:
        **  time
        **  firstDayOfWeek  the first day of week (0=Sunday, 1=Monday, ... 6=Saturday)
        **Notes:
        **  The CalendarWeekRule.FirstDay rule: Week 1 begins on the first day of the year.
        **  Assume f is the specifed firstDayOfWeek,
        **  and n is the day of week for January 1 of the specified year.
        **  Assign offset = n - f;
        **  Case 1: offset = 0
        **      E.g.
        **                     f=1
        **          weekday 0  1  2  3  4  5  6  0  1
        **          date       1/1
        **          week#      1                    2
        **      then week of year = (GetDayOfYear(time) - 1) / 7 + 1
        **
        **  Case 2: offset < 0
        **      e.g.
        **                     n=1   f=3
        **          weekday 0  1  2  3  4  5  6  0
        **          date       1/1
        **          week#      1     2
        **      This means that the first week actually starts 5 days before 1/1.
        **      So week of year = (GetDayOfYear(time) + (7 + offset) - 1) / 7 + 1
        **  Case 3: offset > 0
        **      e.g.
        **                  f=0   n=2
        **          weekday 0  1  2  3  4  5  6  0  1  2
        **          date          1/1
        **          week#         1                    2
        **      This means that the first week actually starts 2 days before 1/1.
        **      So Week of year = (GetDayOfYear(time) + offset - 1) / 7 + 1
        ============================================================================*/
        private static int GetFirstDayWeekOfYear(DateTime time, int firstDayOfWeek)
        {
            int dayOfYear = GetDayOfYear(time) - 1;   // Make the day of year to be 0-based, so that 1/1 is day 0.
            // Calculate the day of week for the first day of the year.
            // dayOfWeek - (dayOfYear % 7) is the day of week for the first day of this year.  Note that
            // this value can be less than 0.  It's fine since we are making it positive again in calculating offset.
            int dayForJan1 = (int)GetDayOfWeek(time) - (dayOfYear % 7);
            int offset = (dayForJan1 - firstDayOfWeek + 14) % 7;
            return ((dayOfYear + offset) / 7 + 1);
        }

        // Returns a given date part of this DateTime. This method is used
        // to compute the year, day-of-year, month, or day part.
        private static int GetDatePart(long ticks, int part)
        {
            // n = number of days since 1/1/0001
            int n = (int)(ticks / TicksPerDay);
            // y400 = number of whole 400-year periods since 1/1/0001
            int y400 = n / DaysPer400Years;
            // n = day number within 400-year period
            n -= y400 * DaysPer400Years;
            // y100 = number of whole 100-year periods within 400-year period
            int y100 = n / DaysPer100Years;
            // Last 100-year period has an extra day, so decrement result if 4
            if (y100 == 4) y100 = 3;
            // n = day number within 100-year period
            n -= y100 * DaysPer100Years;
            // y4 = number of whole 4-year periods within 100-year period
            int y4 = n / DaysPer4Years;
            // n = day number within 4-year period
            n -= y4 * DaysPer4Years;
            // y1 = number of whole years within 4-year period
            int y1 = n / DaysPerYear;
            // Last year has an extra day, so decrement result if 4
            if (y1 == 4) y1 = 3;
            // If year was requested, compute and return it
            if (part == DatePartYear)
            {
                return (y400 * 400 + y100 * 100 + y4 * 4 + y1 + 1);
            }
            // n = day number within year
            n -= y1 * DaysPerYear;
            // If day-of-year was requested, return it
            if (part == DatePartDayOfYear)
            {
                return (n + 1);
            }
            // Leap year calculation looks different from IsLeapYear since y1, y4,
            // and y100 are relative to year 1, not year 0
            bool leapYear = (y1 == 3 && (y4 != 24 || y100 == 3));
            int[] days = leapYear ? DaysToMonth366 : DaysToMonth365;
            // All months have less than 32 days, so n >> 5 is a good conservative
            // estimate for the month
            int m = n >> 5 + 1;
            // m = 1-based month number
            while (n >= days[m]) m++;
            // If month was requested, return it
            if (part == DatePartMonth) return (m);
            // Return 1-based day-of-month
            return (n - days[m - 1] + 1);
        }

        private static int GetWeekOfYearOfMinSupportedDateTime(int firstDayOfWeek, int minimumDaysInFirstWeek)
        {
            int dayOfYear = GetDayOfYear(MinSupportedDateTime) - 1;  // Make the day of year to be 0-based, so that 1/1 is day 0.
            int dayOfWeekOfFirstOfYear = (int)GetDayOfWeek(MinSupportedDateTime) - dayOfYear % 7;

            // Calculate the offset (how many days from the start of the year to the start of the week)
            int offset = (firstDayOfWeek + 7 - dayOfWeekOfFirstOfYear) % 7;
            if (offset == 0 || offset >= minimumDaysInFirstWeek)
            {
                // First of year falls in the first week of the year
                return 1;
            }

            int daysInYearBeforeMinSupportedYear = DaysInYearBeforeMinSupportedYear - 1; // Make the day of year to be 0-based, so that 1/1 is day 0.
            int dayOfWeekOfFirstOfPreviousYear = dayOfWeekOfFirstOfYear - 1 - (daysInYearBeforeMinSupportedYear % 7);

            // starting from first day of the year, how many days do you have to go forward
            // before getting to the first day of the week?
            int daysInInitialPartialWeek = (firstDayOfWeek - dayOfWeekOfFirstOfPreviousYear + 14) % 7;
            int day = daysInYearBeforeMinSupportedYear - daysInInitialPartialWeek;
            if (daysInInitialPartialWeek >= minimumDaysInFirstWeek)
            {
                // If the offset is greater than the minimum Days in the first week, it means that
                // First of year is part of the first week of the year even though it is only a partial week
                // add another week
                day += 7;
            }

            return (day / 7 + 1);
        }

        private static int GetWeekOfYearFullDays(DateTime time, int firstDayOfWeek, int fullDays)
        {
            int dayForJan1;
            int offset;
            int day;

            int dayOfYear = GetDayOfYear(time) - 1;

            // Make the day of year to be 0 - based, so that 1 / 1 is day 0.
            //
            // Calculate the number of days between the first day of year (1/1) and the first day of the week.
            // This value will be a positive value from 0 ~ 6.  We call this value as "offset".
            //
            // If offset is 0, it means that the 1/1 is the start of the first week.
            //     Assume the first day of the week is Monday, it will look like this:
            //     Sun      Mon     Tue     Wed     Thu     Fri     Sat
            //     12/31    1/1     1/2     1/3     1/4     1/5     1/6
            //              +--> First week starts here.
            //
            // If offset is 1, it means that the first day of the week is 1 day ahead of 1/1.
            //     Assume the first day of the week is Monday, it will look like this:
            //     Sun      Mon     Tue     Wed     Thu     Fri     Sat
            //     1/1      1/2     1/3     1/4     1/5     1/6     1/7
            //              +--> First week starts here.
            //
            // If offset is 2, it means that the first day of the week is 2 days ahead of 1/1.
            //     Assume the first day of the week is Monday, it will look like this:
            //     Sat      Sun     Mon     Tue     Wed     Thu     Fri     Sat
            //     1/1      1/2     1/3     1/4     1/5     1/6     1/7     1/8
            //                      +--> First week starts here.

            // Day of week is 0-based.
            // Get the day of week for 1/1.  This can be derived from the day of week of the target day.
            // Note that we can get a negative value.  It's ok since we are going to make it a positive value when calculating the offset.
            dayForJan1 = (int)GetDayOfWeek(time) - (dayOfYear % 7);

            // Now, calculate the offset.  Subtract the first day of week from the dayForJan1.  And make it a positive value.
            offset = (firstDayOfWeek - dayForJan1 + 14) % 7;
            if (offset != 0 && offset >= fullDays)
            {
                //
                // If the offset is greater than the value of fullDays, it means that
                // the first week of the year starts on the week where Jan/1 falls on.
                //
                offset -= 7;
            }
            //
            // Calculate the day of year for specified time by taking offset into account.
            //
            day = dayOfYear - offset;
            if (day >= 0)
            {
                //
                // If the day of year value is greater than zero, get the week of year.
                //
                return (day / 7 + 1);
            }
            //
            // Otherwise, the specified time falls on the week of previous year.
            // Call this method again by passing the last day of previous year.
            //
            // the last day of the previous year may "underflow" to no longer be a valid date time for
            // this calendar if we just subtract so we need the subclass to provide us with 
            // that information
            if (time <= MinSupportedDateTime.AddDays(dayOfYear))
            {
                return GetWeekOfYearOfMinSupportedDateTime(firstDayOfWeek, fullDays);
            }

            return (GetWeekOfYearFullDays(time.AddDays(-(dayOfYear + 1)), firstDayOfWeek, fullDays));
        }
    }
}
