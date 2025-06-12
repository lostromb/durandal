using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Monitoring
{
    /// <summary>
    /// Represents a single test result with additional metadata added, including test name, timestamps, latency, traceid, etc.
    /// </summary>
    public class SingleTestResultInternal
    {
        public DateTimeOffset BeginTimestamp { get; set; }
        public DateTimeOffset EndTimestamp { get; set; }
        public string TestName { get; set; }
        public string TestSuiteName { get; set; }
        public bool Success { get; set; }
        public TimeSpan Latency { get; set; }
        public Guid TraceId { get; set; }
        public string ErrorMessage { get; set; }
    }
}
