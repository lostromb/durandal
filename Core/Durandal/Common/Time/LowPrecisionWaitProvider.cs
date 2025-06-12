using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Time
{
    /// <summary>
    /// Dummy implementation of <see cref="IHighPrecisionWaitProvider"/> which doesn't actually add any precision.
    /// </summary>
    public class LowPrecisionWaitProvider : IHighPrecisionWaitProvider
    {
        public void Wait(double milliseconds, CancellationToken cancelToken)
        {
            WaitAsync(milliseconds, cancelToken).Await();
        }

        public Task WaitAsync(double milliseconds, CancellationToken cancelToken)
        {
            if (milliseconds <= 0)
            {
                return DurandalTaskExtensions.NoOpTask;
            }

            int ms = (int)milliseconds;
            if (ms <= 0)
            {
                ms = 1;
            }

            return Task.Delay(ms, cancelToken);
        }

        public void Dispose()
        {
        }
    }
}
