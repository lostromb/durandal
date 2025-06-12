using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DurandalServices.Instrumentation.Analytics.Charting
{
    public class DateTimeChartPoint
    {
        public DateTimeOffset Time;
        public float Value;

        public DateTimeChartPoint(DateTimeOffset time, float value)
        {
            Time = time;
            Value = value;
        }
    }
}
