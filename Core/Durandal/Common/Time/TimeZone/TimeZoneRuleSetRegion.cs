using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Time.TimeZone
{
    internal class TimeZoneRuleSetRegion
    {
        public DateTimeOffset RangeBegin;
        public DateTimeOffset RangeEnd;
        public IanaTimeZoneEntry ZoneDef;

        public override string ToString()
        {
            return RangeBegin.ToString("yyyy-MM-ddTHH:mm:ss zzz") + " => " + RangeEnd.ToString("yyyy-MM-ddTHH:mm:ss zzz") + " use rules " + ZoneDef.Rules;
        }
    }
}
