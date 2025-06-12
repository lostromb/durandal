using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Time.Timex.Calendar
{
    public class LocalizedWeekDefinition
    {
        /// <summary>
        /// Represents the week definition used in the majority of the world. Weeks begin on Sunday, weekends are saturday and sunday.
        /// </summary>
        public static readonly LocalizedWeekDefinition StandardWeekDefinition = new LocalizedWeekDefinition(-1, 6, 2);

        /// <summary>
        /// Represents the week definition used predominantly in the Arabian peninsula. Weeks begin on Sunday, weekends are friday and saturday.
        /// </summary>
        public static readonly LocalizedWeekDefinition ArabianWeekDefinition = new LocalizedWeekDefinition(-1, 5, 2);

        public LocalizedWeekDefinition(int firstDayOfWeek, int firstDayOfWeekend, int weekendLength)
        {
            FirstDayOfWeek = firstDayOfWeek;
            FirstDayOfWeekend = firstDayOfWeekend;
            WeekendLength = weekendLength;
        }

        /// <summary>
        /// The first day of the "localized" week relative to the ISO calendar.
        /// If the week starts on Sunday then this generally is set to -1
        /// </summary>
        public int FirstDayOfWeek { get; set; }

        /// <summary>
        /// The first day of the "localized" weekend relative to the ISO calendar.
        /// Saturday == 6 in ISO
        /// </summary>
        public int FirstDayOfWeekend { get; set; }

        /// <summary>
        /// The number of inclusive days to count in the weekend
        /// </summary>
        public int WeekendLength { get; set; }
    }
}
