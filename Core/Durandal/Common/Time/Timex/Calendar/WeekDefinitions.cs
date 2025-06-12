using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Time.Timex.Calendar
{
    /// <summary>
    /// Represents the week definition used in the majority of the world. Weeks begin on Sunday, weekends are saturday and sunday.
    /// </summary>
    public class StandardWeekDefinition : ILocalizedWeekDefinition
    {
        public int FirstDayOfWeek
        {
            get
            {
                return -1;
            }
        }

        public int FirstDayOfWeekend
        {
            get
            {
                return 5;
            }
        }

        public int WeekendLength
        {
            get
            {
                return 2;
            }
        }
    }

    /// <summary>
    /// Represents the week definition used predominantly in the Arabian peninsula. Weeks begin on Sunday, weekends are friday and saturday.
    /// </summary>
    public class ArabianWeekDefinition : ILocalizedWeekDefinition
    {
        public int FirstDayOfWeek
        {
            get
            {
                return -1;
            }
        }

        public int FirstDayOfWeekend
        {
            get
            {
                return 4;
            }
        }

        public int WeekendLength
        {
            get
            {
                return 2;
            }
        }
    }
}
