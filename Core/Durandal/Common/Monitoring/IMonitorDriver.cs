using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Durandal.Common.Time.Scheduling;
using Durandal.Common.Time;
using System.Threading;

namespace Durandal.Common.Monitoring
{
    /// <summary>
    /// Wraps up logic to actually queue up the monitor to a thread pool, run it, and observe it
    /// </summary>
    public interface IMonitorDriver : IDisposable
    {
        void QueueTest(IServiceMonitor testCase, ITestResultStore testResultStore, DeltaClock<IServiceMonitor> testScheduler, int testScaleDenominator, CancellationToken cancelToken, IRealTimeProvider realTime);

        int QueuedTestCount { get; }
    }
}
