using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Instrumentation
{
    public class TimeLineEntry
    {
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public string SpanName { get; set; }
        public double DurationMs { get; set; }
    }
}
