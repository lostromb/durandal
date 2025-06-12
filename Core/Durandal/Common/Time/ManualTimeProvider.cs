using Durandal.Common.Utils;
using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Time
{
    /// <summary>
    /// A time provider where the current time must be explicitly set and never changes
    /// </summary>
    public class ManualTimeProvider : IRealTimeProvider
    {
        private static IHighPrecisionWaitProvider WAIT_PROVIDER;
        private DateTimeOffset _currentTime;

        public bool IsForDebug => true;

        public ManualTimeProvider()
        {
            _currentTime = new DateTimeOffset(1900, 1, 1, 0, 0, 0, 0, TimeSpan.Zero);

            // Don't create the wait provider statically so that we don't accidentally start a background thread when JIT hits this class.
            if (WAIT_PROVIDER == null)
            {
                IHighPrecisionWaitProvider newProvider = new SpinwaitHighPrecisionWaitProvider(false);
                IHighPrecisionWaitProvider oldProvider = Interlocked.Exchange(ref WAIT_PROVIDER, newProvider);
                oldProvider?.Dispose();
            }
        }

        /// <summary>
        /// The current time, measured in milliseconds from any arbitrary epoch
        /// </summary>
        public long TimestampMilliseconds
        {
            get
            {
                return _currentTime.Ticks / 10000L;
            }
            set
            {
                _currentTime = new DateTimeOffset(value * 10000L, TimeSpan.Zero);
            }
        }

        public long TimestampTicks
        {
            get
            {
                return _currentTime.Ticks;
            }
            set
            {
                _currentTime = new DateTimeOffset(value, TimeSpan.Zero);
            }
        }

        public DateTimeOffset Time
        {
            get
            {
                return _currentTime;
            }
            set
            {
                _currentTime = value;
            }
        }

        public void Wait(TimeSpan time, CancellationToken cancelToken)
        {
            DateTimeOffset waitEndTime = _currentTime + time;
            while (_currentTime <= waitEndTime)
            {
                WAIT_PROVIDER.Wait(1, cancelToken);
            }
        }

        public async Task WaitAsync(TimeSpan time, CancellationToken cancelToken)
        {
            DateTimeOffset waitEndTime = _currentTime + time;
            while (_currentTime <= waitEndTime)
            {
                await WAIT_PROVIDER.WaitAsync(1, cancelToken).ConfigureAwait(false);
            }
        }

        public IRealTimeProvider Fork(
            string nameForDebug,
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0)
        {
            return this;
        }

        public void Merge(
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0) { }
    }
}
