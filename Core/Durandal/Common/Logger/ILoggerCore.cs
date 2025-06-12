using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Logger
{
    public interface ILoggerCore : IDisposable
    {
        void LoggerImplementation(PooledLogEvent e);

        // BUGBUG because each logger instance shares a reference to the logger core, and individual loggers aren't disposable,
        // the logger core will also never get properly disposed and therefore cause problems with missed logs on program
        // exit because there's no flush. Is there a way to fix that design?
        Task Flush(CancellationToken cancellizer, IRealTimeProvider realTime, bool blocking);
    }
}
