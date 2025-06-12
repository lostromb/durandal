using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit
{
    public class TimeResolutionInfo
    {
        public DateTimeOffset UserLocalTime { get; set; }
        public DateTimeOffset QueryTime { get; set; }

        /// <summary>
        /// The number of whole days separating the current local time and the query time
        /// </summary>
        public TimeSpan DaysOffset
        {
            get
            {
                long qtDays = (((long)(QueryTime.Year) * 365) + (long)QueryTime.DayOfYear);
                long utDays = (((long)(UserLocalTime.Year) * 365) + (long)UserLocalTime.DayOfYear);
                return TimeSpan.FromDays((int)(qtDays - utDays));
            }
        }

        /// <summary>
        /// Indicates whether the query time is somewhere within the current day (local to the user)
        /// </summary>
        public bool IsToday
        {
            get
            {
                return DaysOffset < TimeSpan.FromDays(1) &&
                    DaysOffset > TimeSpan.FromDays(-1);
            }
        }
    }
}
