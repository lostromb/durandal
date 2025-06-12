using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Logger
{
    public interface ILoggingHistory : IEnumerable<LogEvent>
    {
        IEnumerable<LogEvent> FilterByCriteria(LogLevel level, bool iterateReverse = false);
        IEnumerable<LogEvent> FilterByCriteria(FilterCriteria criteria, bool iterateReverse = false);
    }
}
