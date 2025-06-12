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
    /// Standard definition of wallclock time, which provides the ability for threads to wait
    /// using semantics identical to Task.Delay / Thread.Sleep, except with potentially higher precision.
    /// </summary>
    public class DefaultRealTimeProvider : IRealTimeProvider
    {
        private static readonly ReaderWriterLockMicro HighPrecisionWaitProviderLock = new ReaderWriterLockMicro();
        public static readonly DefaultRealTimeProvider Singleton = new DefaultRealTimeProvider();

        private static IHighPrecisionWaitProvider _highPrecisionWaitProvider = new LowPrecisionWaitProvider();

        private static readonly TimeSpan HIGH_PRECISION_WAIT_THRESHOLD = TimeSpan.FromMilliseconds(20);

        /// <summary>
        /// Gets or sets the global high precision wait provider.
        /// Typically, built-in methods such as Task.Delay and Thread.Sleep only have a granularity
        /// of 64hz (~15 ms), which means that when you depend on waits as low as 1ms, you end up with unexpected delays.
        /// This wait provider allows you to handle those short waits using more precise system resources such as multimedia timers
        /// if available. If not overridden, this implementation will just use the default imprecise behavior.
        /// If an existing provider was set when you set a new value, the previous implementation will be disposed.
        /// </summary>
        public static IHighPrecisionWaitProvider HighPrecisionWaitProvider
        {
            get
            {
                uint hRead = HighPrecisionWaitProviderLock.EnterReadLock();
                try
                {
                    return _highPrecisionWaitProvider;
                }
                finally
                {
                    HighPrecisionWaitProviderLock.ExitReadLock(hRead);
                }
            }
            set
            {
                HighPrecisionWaitProviderLock.EnterWriteLock();
                try
                {
                    // check if we are setting to a new value
                    if (_highPrecisionWaitProvider != null &&
                        !ReferenceEquals(value, _highPrecisionWaitProvider))
                    {
                        _highPrecisionWaitProvider.Dispose();
                    }

                    _highPrecisionWaitProvider = value ?? new LowPrecisionWaitProvider();
                }
                finally
                {
                    HighPrecisionWaitProviderLock.ExitWriteLock();
                }
            }
        }

        private DefaultRealTimeProvider() { }

        /// <inheritdoc />
        public bool IsForDebug => false;

        /// <inheritdoc />
        public long TimestampMilliseconds => HighPrecisionTimer.GetCurrentTicks() / 10000L;

        /// <inheritdoc />
        public long TimestampTicks => HighPrecisionTimer.GetCurrentTicks();

        /// <inheritdoc />
        public DateTimeOffset Time => HighPrecisionTimer.GetCurrentUTCTime();

        /// <inheritdoc />
        public void Wait(TimeSpan time, CancellationToken cancelToken)
        {
            if (time < HIGH_PRECISION_WAIT_THRESHOLD)
            {
                HighPrecisionWaitProvider.Wait(time.TotalMilliseconds, cancelToken);
            }
            else
            {
                Task.Delay(time, cancelToken).Await();
            }
        }

        /// <inheritdoc />
        public Task WaitAsync(TimeSpan time, CancellationToken cancelToken)
        {
            if (time < HIGH_PRECISION_WAIT_THRESHOLD)
            {
                return HighPrecisionWaitProvider.WaitAsync(time.TotalMilliseconds, cancelToken);
            }
            else
            {
                return Task.Delay(time, cancelToken);
            }
        }

        /// <inheritdoc />
        public IRealTimeProvider Fork(string nameForDebug, string callerFilePath = null, string callerMemberName = null, int callerLineNumber = 0)
        {
            return Singleton;
        }

        /// <inheritdoc />
        public void Merge(string callerFilePath = null, string callerMemberName = null, int callerLineNumber = 0) { }
    }
}
