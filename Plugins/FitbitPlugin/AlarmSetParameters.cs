using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Fitbit
{
    public class AlarmSetParameters
    {
        public AlarmDayOfWeek DayOfWeek { get; set; }
        public bool IsForWeekends { get; set; }
        public bool IsForWeekdays { get; set; }
        public int? Hour { get; set; }
        public int? Minute { get; set; }
        public bool AmPmResolved { get; set; }
        public string TrackerId { get; set; }
        public bool Recurring { get; set; }
    }
}
