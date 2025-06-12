using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Time.Timex.Calendar;

namespace Durandal.Common.Time.Timex
{
    /// <summary>
    /// General time helper methods, mostly related to ISO conversion
    /// </summary>
    public static class TimexHelpers
    {
        /// <summary>
        /// Given a C# DateTime, calculate ISO 8601 week of year.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <param name="isoWeekYear">The ISO year that the returned week value is part of. NOT the same as gregorian year.</param>
        /// <returns>ISO 8601 week of year. NOTE THAT the week # is not necessarily within the same Gregorian calendar year.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1704")]
        [SuppressMessage("Microsoft.Design", "CA1021")]
        public static int GetIso8601WeekOfYear(DateTime dateTime, out int isoWeekYear)
        {
            var dayOfWeek = GregorianCalendar.GetDayOfWeek(dateTime);
            if (dayOfWeek >= System.DayOfWeek.Monday && dayOfWeek <= System.DayOfWeek.Wednesday)
            {
                dateTime = dateTime.AddDays(3);
            }

            int isoWeek = GregorianCalendar.GetISOWeekOfYear(dateTime);

            isoWeekYear = GetIso8601WeekYear(dateTime.Year, dateTime.Month, isoWeek);

            return isoWeek;
        }

        /// <summary>
        /// Returns ISO 8601 day of week number for the given date.
        /// Monday = 1, Sunday = 7 in ISO schema.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns>ISO 8601 day of week number</returns>
        [SuppressMessage("Microsoft.Naming", "CA1704", MessageId = "Iso")]
        public static int GetIso8601DayOfWeek(DateTime dateTime)
        {
            return (int)dateTime.DayOfWeek == 0 ? 7 : (int)dateTime.DayOfWeek;
        }

        /// <summary>
        /// Calculates year for specified date and week.
        /// This method is needed because the ISO and Gregorian calendar do not always
        /// have the same year value at year boundaries. Sometimes, January 1 2015 is actually
        /// ISO Week 53 Day 6 of year 2014, since ISO does not allow weeks to span different years.
        /// This method will calculate the correct year given a Gregorian year, Gregorian month, and ISO week value.
        /// </summary>
        /// <param name="gregorianYear">The Gregorian year</param>
        /// <param name="gregorianMonth">The Gregorian month</param>
        /// <param name="isoWeek">The ISO week</param>
        /// <returns>ISO 8601 year for week</returns>
        [SuppressMessage("Microsoft.Naming", "CA1704")]
        public static int GetIso8601WeekYear(int gregorianYear, int gregorianMonth, int isoWeek)
        {
            int weekYear;
            if (isoWeek >= 52 && gregorianMonth == 1)
                weekYear = gregorianYear - 1;
            else if (isoWeek == 1 && gregorianMonth == 12)
                weekYear = gregorianYear + 1;
            else
                weekYear = gregorianYear;

            return weekYear;
        }

        /// <summary>
        /// Given an ISO year + week # pair, return a C# DateTime set to the first day (MONDAY) of that week.
        /// !!! MAKE SURE YOU ARE USING ISO YEAR INSTEAD OF GREGORIAN YEAR! USE GetIso8601WeekYear() IF YOU NEED TO !!!
        /// </summary>
        /// <param name="isoYear">The ISO YEAR value (Not gregorian year!)</param>
        /// <param name="isoWeek">The ISO WEEK value</param>
        /// <returns>C# DateTime set to the first day (MONDAY) of a week</returns>
        [SuppressMessage("Microsoft.Naming", "CA1704")]
        public static DateTime GetFirstDayOfIsoWeek(int isoYear, int isoWeek)
        {
            // Start in the middle of the Gregorian year
            DateTime returnVal = new DateTime(isoYear, 6, 15);

            // Zoom to Monday
            int dayOfWeek = GetIso8601DayOfWeek(returnVal);
            returnVal = returnVal.AddDays(1 - dayOfWeek);

            // Then zoom to the correct ISO week
            int newIsoYear;
            int weekOfYear = GetIso8601WeekOfYear(returnVal, out newIsoYear);
            returnVal = returnVal.AddDays(7 * (isoWeek - weekOfYear));

            return returnVal;
        }
    }
}
