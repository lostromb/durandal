using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Monitoring
{
    /// <summary>
    /// Stores the result of a failed test.
    /// </summary>
    public class ErrorResult
    {
        public DateTimeOffset BeginTimestamp { get; set; }
        public DateTimeOffset EndTimestamp { get; set; }
        public string Message { get; set; }
        public Guid TraceId { get; set; }
    }
}
