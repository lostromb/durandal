using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Monitoring
{
    /// <summary>
    /// Represents the aggregated status of a single test over a certain window of time (e.g. the past 10 minutes)
    /// </summary>
    public class TestMonitorStatus
    {
        public string TestName { get; set; }
        public string TestSuiteName { get; set; }
        public float PassRate { get; set; }
        public float? PassRateThreshold { get; set; }
        public TimeSpan MeanLatency { get; set; }
        public TimeSpan MedianLatency { get; set; }
        public TimeSpan? LatencyThreshold { get; set; }
        public List<ErrorResult> LastErrors { get; set; }
        public int TestsRan { get; set; }
        public TimeSpan MonitoringWindow { get; set; }
        public string TestDescription { get; set; }

        public bool IsPassing
        {
            get
            {
                return !((PassRateThreshold.HasValue && PassRate < (PassRateThreshold.Value - 0.0001f))
                    || (LatencyThreshold.HasValue && MedianLatency > LatencyThreshold.Value));
            }
        }
    }
}
