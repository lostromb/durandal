using System;
using System.Diagnostics.CodeAnalysis;

namespace Durandal.Common.Time.Timex.Enums
{
    public enum PartOfDay
    {
        None,
        Morning,
        [SuppressMessage("Microsoft.Naming", "CA1702")]
        MidDay,
        Afternoon,
        Evening,
        Night,
        [SuppressMessage("Microsoft.Naming", "CA1709", MessageId = "Pm")]
        Pm,
        [SuppressMessage("Microsoft.Naming", "CA1702")]
        DayTime,
        Noon,
        Midnight
    }

    public static class PartOfDayExtensions
    {
        /// <summary>
        /// Resolves a PartOfDay into an hour that roughly corresponds to that part of day.
        /// This is used for making fairly clear-cut inferences, such as "5 PM is not part of the morning".
        /// It shouldn't be used for anything very precise.
        /// </summary>
        /// <param name="part">The PartOfDay to convert</param>
        /// <returns>An hour value, in 24H format - 3 pm is 15, noon is 12, etc.</returns>
        public static int ToApproximateHour(this PartOfDay part)
        {
            switch (part)
            {
                case PartOfDay.Morning:
                    return 8;
                case PartOfDay.MidDay:
                    return 12;
                case PartOfDay.Noon:
                    return 12;
                case PartOfDay.DayTime:
                    return 14;
                case PartOfDay.Afternoon:
                    return 15;
                case PartOfDay.Pm:
                    return 17;
                case PartOfDay.Evening:
                    return 18;
                case PartOfDay.Night:
                    return 20;
                case PartOfDay.Midnight:
                    return 24;
                default:
                    return 12;
            }
        }
    }
}
