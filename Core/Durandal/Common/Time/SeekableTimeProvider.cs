using Durandal.Common.Tasks;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Time
{
    /// <summary>
    /// Real time provider that can be skipped forward or backwards relative to real time
    /// </summary>
    public class SeekableTimeProvider : IRealTimeProvider
    {
        private long _offsetMs = 0;

        public bool IsForDebug => true;

        public IRealTimeProvider Fork(
            string nameForDebug,
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0)
        {
            return this;
        }

        public long TimestampMilliseconds
        {
            get
            {
                return (DateTimeOffset.UtcNow.Ticks / 10000L) + _offsetMs;
            }
        }

        public long TimestampTicks
        {
            get
            {
                return DateTimeOffset.UtcNow.Ticks + (_offsetMs * 10000L);
            }
        }

        public DateTimeOffset Time
        {
            get
            {
                return new DateTimeOffset(TimestampMilliseconds * 10000L, TimeSpan.Zero);
            }
        }

        public void Merge(
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0) { }

        public void Wait(TimeSpan time, CancellationToken cancelToken)
        {
            // Block in small increments so that we can continuously check if any skips happened and adjust accordingly
            TimeSpan blockIncrement = TimeSpan.FromMilliseconds(Math.Min(5, time.TotalMilliseconds));
            DateTimeOffset targetTime = Time + time;
            while (Time < targetTime)
            {
                DurandalTaskExtensions.Block(blockIncrement, cancelToken);
            }
        }

        public async Task WaitAsync(TimeSpan time, CancellationToken cancelToken)
        {
            TimeSpan blockIncrement = TimeSpan.FromMilliseconds(Math.Min(5, time.TotalMilliseconds));
            DateTimeOffset targetTime = Time + time;
            while (Time < targetTime)
            {
                await Task.Delay(blockIncrement, cancelToken).ConfigureAwait(false);
            }
        }

        public void SkipTime(long offsetMs)
        {
            _offsetMs += offsetMs;
        }
    }
}
