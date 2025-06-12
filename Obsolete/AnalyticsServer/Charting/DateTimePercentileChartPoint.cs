using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DurandalServices.Instrumentation.Analytics.Charting
{
    public class DateTimePercentileChartPoint
    {
        public DateTimeOffset Time;
        public float PercentileP5;
        public float PercentileP25;
        public float PercentileP50;
        public float PercentileP75;
        public float PercentileP95;
    }
}
