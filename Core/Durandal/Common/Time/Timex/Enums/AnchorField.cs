using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Time.Timex.Enums
{
    public enum AnchorField
    {
        /// <summary>
        /// A specific year
        /// </summary>
        Year,

        /// <summary>
        /// A specific month
        /// </summary>
        Month,

        /// <summary>
        /// A specific ISO week
        /// </summary>
        Week,

        /// <summary>
        /// A specific day of the month
        /// </summary>
        Day,
        /// <summary>
        /// A specific hour of the day
        /// </summary>
        Hour,

        /// <summary>
        /// A specific minute of an hour
        /// </summary>
        Minute,

        /// <summary>
        /// A specific second of a minute
        /// </summary>
        Second,

        /// <summary>
        /// A specific part of a day
        /// </summary>
        PartOfDay,

        /// <summary>
        /// A specific day of a week e.g. "1" for Monday
        /// </summary>
        DayOfWeek,

        /// <summary>
        /// A specific week of the month e.g. "4"
        /// </summary>
        WeekOfMonth,

        /// <summary>
        /// Indicates an anchoring to all the weekdays of a specific week, NOT a specific day such as Tuesday
        /// </summary>
        Weekday,

        /// <summary>
        /// Indicates an anchoring to the weekend of a specific week
        /// </summary>
        Weekend
    }
}
