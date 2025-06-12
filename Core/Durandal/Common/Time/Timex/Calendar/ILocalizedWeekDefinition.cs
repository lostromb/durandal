using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Time.Timex.Calendar
{
    public interface ILocalizedWeekDefinition
    {
        /// <summary>
        /// The first day of the "localized" week relative to the ISO calendar.
        /// If the week starts on Sunday then this generally is set to -1
        /// </summary>
        int FirstDayOfWeek { get; }

        /// <summary>
        /// The first day of the "localized" weekend relative to the ISO calendar.
        /// Saturday == 6 in ISO
        /// </summary>
        int FirstDayOfWeekend { get; }

        /// <summary>
        /// The number of inclusive days to count in the weekend
        /// </summary>
        int WeekendLength { get; }
    }
}
