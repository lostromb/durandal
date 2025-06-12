using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Monitoring
{
    /// <summary>
    /// Represents the aggregated status of a single suite of tests over a certain window of time (e.g. the past 10 minutes)
    /// </summary>
    public class TestSuiteStatus
    {
        public string SuiteName { get; set; }
        public Dictionary<string, TestMonitorStatus> TestResults { get; set; }
        public float PassRate { get; set; }
        public float MeanLatency { get; set; }
        public float MedianLatency { get; set; }
        public int TestsRan { get; set; }
        public int MonitoringWindowSeconds { get; set; }
        //public List<AppInsightsQuery> AnalyticsQueries { get; set; }

        public TestSuiteStatus()
        {
            TestResults = new Dictionary<string, TestMonitorStatus>();
            //AnalyticsQueries = new List<AppInsightsQuery>();
        }
    }
}
