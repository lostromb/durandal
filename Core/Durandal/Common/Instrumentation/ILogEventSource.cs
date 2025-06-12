using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Instrumentation
{
    public interface ILogEventSource
    {
        Task<IEnumerable<LogEvent>> GetLogEvents(FilterCriteria logFilter);
    }
}
