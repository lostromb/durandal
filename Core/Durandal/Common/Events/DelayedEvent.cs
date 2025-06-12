using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Events
{
    public class DelayedEvent<T> : IDisposable where T : EventArgs
    {
        private readonly AsyncEvent<T> _baseEvent;
        private CancellationTokenSource _cancelTokenSource;
        private CancellationToken _cancelToken;
        private Task _delayTask = null;
        private AsyncLockSlim _lock = new AsyncLockSlim();
        private object _currentEventSource = null;
        private T _currentEventArgs = null;
        private long _currentTargetTime = 0;
        private int _disposed = 0;

        public DelayedEvent(AsyncEvent<T> eventToFire)
        {
            _baseEvent = eventToFire;
            _cancelTokenSource = new CancellationTokenSource();
            _cancelToken = _cancelTokenSource.Token;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~DelayedEvent()
        {
            Dispose(false);
        }
#endif

        public void ScheduleEvent(object source, T args, IRealTimeProvider realTime, TimeSpan delay)
        {
            _lock.GetLock(_cancelToken, realTime);
            try
            {
                _currentEventSource = source;
                _currentEventArgs = args;
                _currentTargetTime = realTime.TimestampMilliseconds + (long)delay.TotalMilliseconds;
                if (_delayTask == null)
                {
                    IRealTimeProvider forkedTime = realTime.Fork("DelayedEvent");
                    _delayTask = Task.Run(async () =>
                    {
                        try
                        {
                            await RunBackgroundTask(_cancelToken, forkedTime).ConfigureAwait(false);
                        }
                        finally
                        {
                            forkedTime.Merge();
                        }
                    });
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public void CancelScheduledEvent()
        {
            _lock.GetLock();
            try
            {
                _cancelTokenSource.Cancel();
                _cancelTokenSource.Dispose();
                _cancelTokenSource = new CancellationTokenSource();
                _cancelToken = _cancelTokenSource.Token;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _cancelTokenSource.Cancel();
                _cancelTokenSource.Dispose();
                _lock?.Dispose();
            }
        }

        private async Task RunBackgroundTask(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            bool hasLock = true;
            try
            {
                // Hold the lock for every part of this method except during the wait.
                // This is to try and prevent race conditions between the loop finishing and the event being fired
                _lock.GetLock(_cancelToken, realTime);
                bool keepWaiting = true;
                while (keepWaiting)
                {
                    _lock.Release();
                    hasLock = false;
                    await realTime.WaitAsync(TimeSpan.FromMilliseconds(10), cancelToken).ConfigureAwait(false);
                    await _lock.GetLockAsync(cancelToken, realTime).ConfigureAwait(false);
                    hasLock = true;
                    keepWaiting = realTime.TimestampMilliseconds < _currentTargetTime;
                }

                if (!cancelToken.IsCancellationRequested)
                {
                    await _baseEvent.Fire(_currentEventSource, _currentEventArgs, realTime).ConfigureAwait(false);
                }

                _delayTask = null;
            }
            finally
            {
                if (hasLock)
                {
                    _lock.Release();
                }
            }
        }
    }
}
