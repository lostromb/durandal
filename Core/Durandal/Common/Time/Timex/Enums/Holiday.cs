using System;
using System.Collections.Generic;

namespace Durandal.Common.Time.Timex.Enums
{
    public enum Holiday
    {
        EasterSunday,
        ChineseNewYear,
        Diwali,
        Passover,
        Hanukkah,
        RoshHashanah,
    }

    public static class ComplexHolidays
    {
        // Values for various global holidays from 2000 to 2050
        // These are interleaved arrays. Channel 0 is the month, channel 1 is the day
        private static readonly ushort[] M_EASTER = 
            {4,23, 4,15, 3,31, 4,20, 4,11, 3,27, 4,16, 4,8,  3,23, 4,12, // 2000 to 2009
		     4,4,  4,24, 4,8,  3,31, 4,20, 4,5,  3,27, 4,16, 4,1,  4,21, // 2010 to 2019
		     4,12, 4,4,  4,17, 4,9,  3,31, 4,20, 4,5,  3,28, 4,16, 4,1,  // 2020 to 2029
		     4,21, 4,13, 3,28, 4,17, 4,9,  3,25, 4,13, 4,5,  4,25, 4,10, // 2030 to 2039
		     4,1,  4,21, 4,6,  3,29, 4,17, 4,9,  3,25, 4,14, 4,5,  4,18, // 2040 to 2049
		     4,10}; // 2050

        private static readonly ushort[] M_CHINESE_NEW_YEAR =
		    {2,5,  1,24, 2,12, 2,1,  1,22, 2,9,  1,29, 2,18, 2,7,  1,26, // 2000 to 2009
		     2,14, 2,3,  2,23, 2,10, 1,31, 2,19, 2,8,  1,28, 2,16, 2,5,  // 2010 to 2019
		     1,25, 2,12, 2,1,  1,22, 2,10, 1,29, 2,16, 2,6,  1,26, 2,13, // 2020 to 2029
		     2,3,  1,23, 2,11, 2,1,  2,19, 2,8,  1,28, 2,15, 2,4,  1,24, // 2030 to 2039
		     2,12, 2,1,  1,22, 2,10, 1,30, 2,17, 2,6,  1,26, 2,14,  2,2, // 2040 to 2049
		     1,23}; // 2050

        private static readonly ushort[] M_PASSOVER =
		    {4,20, 4,8,  3,28, 4,17, 4,6,  4,24, 4,13, 4,3,  4,20, 4,9,  // 2000 to 2009
		     3,30, 4,19, 4,7,  3,26, 4,15, 4,4,  4,23, 4,11, 3,31, 4,20, // 2010 to 2019
		     4,9,  3,28, 4,16, 4,6,  4,23, 4,13, 4,2,  4,22, 4,11, 3,31, // 2020 to 2029
		     4,18, 4,8,  3,27, 4,14, 4,4,  4,24, 4,12, 3,31, 4,21, 4,9,  // 2030 to 2039
		     3,29, 4,16, 4,5,  4,25, 4,12, 4,2,  4,21, 4,11, 3,29, 4,17, // 2040 to 2049
		     4,7}; // 2050

        private static readonly ushort[] M_ROSH_HASHANAH =
		    {9,30, 9,18, 9,7,  9,27, 9,16, 10,4, 9,23, 9,13, 9,30, 9,19, // 2000 to 2009
		     9,9,  9,29, 9,17, 9,5,  9,25, 9,14, 10,3, 9,21, 9,10, 9,30, // 2010 to 2019
		     9,19, 9,7,  9,26, 9,16, 10,3, 9,23, 9,12, 10,2, 9,21, 9,10, // 2020 to 2029
		     9,28, 9,18, 9,6,  9,24, 9,14, 10,4, 9,22, 9,10, 9,30, 9,19, // 2030 to 2039
		     9,8,  9,26, 9,15, 10,5, 9,22, 9,12, 10,1, 9,21, 9,8,  9,27, // 2040 to 2049
		     9,17}; // 2050

        private static readonly ushort[] M_HANUKKAH =
		    {12,22, 12,10, 11,30, 12,20, 12,8,  12,26, 12,16, 12,5,  12,22, 12,12, // 2000 to 2009
		     12,2,  12,21, 12,9,  11,28, 12,17, 12,7,  12,25, 12,13, 12,3,  12,23, // 2010 to 2019
		     12,11, 11,29, 12,19, 12,8,  12,26, 12,15, 12,5,  12,25, 12,13, 12,2,  // 2020 to 2029
		     12,21, 12,10, 11,28, 12,17, 12,7,  12,26, 12,14, 12,3,  12,22, 12,12, // 2030 to 2039
		     11,30, 12,18, 12,8,  12,27, 12,15, 12,4,  12,24, 12,13, 11,30, 12,20, // 2040 to 2049
		     12,10}; // 2050

        private static readonly ushort[] M_DIWALI =
		    {10,26, 11,14, 11,4,  10,25, 11,12, 11,1,  10,21, 11,9,  10,28, 10,17, // 2000 to 2009
		     11,5,  10,26, 11,13, 11,3,  10,23, 11,11, 10,30, 10,19, 11,7,  10,27, // 2010 to 2019
		     11,14, 11,4,  10,24, 11,12, 11,1,  10,21, 11,8,  10,29, 10,17, 11,5,  // 2020 to 2029
		     10,26, 11,14, 11,2,  10,22, 11,10, 10,30, 10,18, 11,7,  10,27, 11,15, // 2030 to 2039
		     11,4,  10,24, 11,12, 11,1,  10,20, 11,8,  10,29, 10,18, 11,5,  10,26, // 2040 to 2049
		     11,14}; // 2050

        private static ushort[] MapHoliday(Holiday toMap)
        {
            switch (toMap)
            {
                case Holiday.EasterSunday:
                    return M_EASTER;
                case Holiday.ChineseNewYear:
                    return M_CHINESE_NEW_YEAR;
                case Holiday.Hanukkah:
                    return M_HANUKKAH;
                case Holiday.Diwali:
                    return M_DIWALI;
                case Holiday.RoshHashanah:
                    return M_ROSH_HASHANAH;
                case Holiday.Passover:
                    return M_PASSOVER;
                default:
                    return M_EASTER;
            }
        }

        /// <summary>
        /// Resolves a complex holiday and returns it as a single datetime value (or null on error)
        /// </summary>
        /// <param name="holiday">The holiday to be resolved</param>
        /// <param name="currentDateTime">The current date and time</param>
        /// <param name="context">The current normalization context</param>
        /// <param name="yearIsFixed">Indicates that the year value cannot change, i.e. something like "Easter 2012" was specified</param>
        /// <param name="offsetDays">Many holidays are relative to others, and are expressed in a form like "EasterSunday + 49days". For inference to work
        ///		properly on these values, the value of the date offset (if any) should be passed here.</param>
        /// <returns>A DateTime referring to proper holiday, applying inference rules if necessary. If inference failed, this method returns null</returns>
        public static DateTime? ResolveComplexHoliday(
            Holiday holiday,
            DateTime currentDateTime,
            TimexContext context,
            bool yearIsFixed,
            int offsetDays)
        {
            // Check input arguments
            if (currentDateTime.Year < 2000 ||
                context == null ||
                context.ReferenceDateTime.Year < 2000)
            {
                return null;
            }

            int targetYear = yearIsFixed ?
                currentDateTime.Year :
                context.ReferenceDateTime.Year;

            DateTime returnVal = new DateTime(currentDateTime.Ticks);

            // Get the pointer to the interleaved array containing the month and day information
            ushort[] targetArray = MapHoliday(holiday);

            // Attempt inference up to 3 times. Retries can occur if the holiday has already passed for the current year.
            // If it fails 3 times, return an error value.
            bool normalizationOK = yearIsFixed;
            int attempts = 0;
            do
            {
                // Check array bounds
                uint arrayIndex = (uint)(targetYear - 2000);
                if ((arrayIndex * 2) + 1 > targetArray.Length)
                {
                    return null;
                }

                // Set the month/day values
                ushort targetMonth = targetArray[arrayIndex * 2];
                ushort targetDay = targetArray[(arrayIndex * 2) + 1];

                // Sanity check: make sure they are within bounds
                // assert(targetMonth >= 1 && targetMonth <= 12 && targetDay >= 1 && targetDay <= 31);

                // Augment the return value date
                returnVal = returnVal.AddYears(targetYear - returnVal.Year);
                returnVal = returnVal.AddMonths(targetMonth - returnVal.Month);
                returnVal = returnVal.AddDays(targetDay - returnVal.Day);

                // Also account for the offset value in the inference, but do not change the returned value
                // This is needed to determine if things like "7 days before easter" has already passed
                returnVal = returnVal.AddDays(offsetDays);

                // Apply normalization here to determine which easter we are referring to.
                // This normalization is applied even if useInference is disabled in the context.
                // The reason for this is that things like "easter" without an associated year cannot be expressed unambiguously.
                // In this case, we just make a best guess based on the current context.
                if (context.ReferenceDateTime < returnVal &&
                    context.Normalization == Normalization.Past)
                {
                    // Subtract a year and try again
                    targetYear -= 1;
                }
                else if (context.ReferenceDateTime > returnVal &&
                    context.Normalization == Normalization.Future)
                {
                    // Add a year and try again.
                    targetYear += 1;
                }
                else
                {
                    normalizationOK = true;
                }

                // Undo the offset check before returning
                returnVal = returnVal.AddDays(0 - offsetDays);
            }
            while (!normalizationOK && attempts++ < 3);

            if (!normalizationOK)
                return null;

            return returnVal;
        }
    }
}
