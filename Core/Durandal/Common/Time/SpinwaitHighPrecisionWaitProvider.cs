using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Time
{
    /// <summary>
    /// Implementation of <see cref="IHighPrecisionWaitProvider"/> which uses spinwaiting to enforce an exact delay
    /// on calling threads. It's generally not advisable to use this, since the "low precision" mode doesn't add
    /// much precision, and the "high precision" mode requires a single CPU thread to constantly spin in the background,
    /// which could really waste your processor time.
    /// </summary>
    public class SpinwaitHighPrecisionWaitProvider : IHighPrecisionWaitProvider
    {
        private const int MAX_WAITING_THREADS = 1000;
        private readonly Task _backgroundThread;
        private readonly SemaphoreSlim _tickSignal;
        private readonly Stopwatch _stopwatch;
        private readonly RateLimiter _rateLimiter;
        private bool _running = true;
        private int _numWaitingThreads = 0;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="SpinwaitHighPrecisionWaitProvider"/>.
        /// </summary>
        /// <param name="highPrecision">IF TRUE, THIS CLASS WILL USE A DEDICATED SPINNING THREAD ON YOUR CPU, WHICH WILL EAT UP ONE CORE.</param>
        public SpinwaitHighPrecisionWaitProvider(bool highPrecision = false)
        {
            _tickSignal = new SemaphoreSlim(0, MAX_WAITING_THREADS);

            if (highPrecision)
            {
                _stopwatch = new Stopwatch();
            }
            else
            {
                _rateLimiter = new RateLimiter(10000, 100);
            }

            _backgroundThread = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(() =>
            {
                // Run timer thread forever
                while (_running)
                {
                    if (highPrecision)
                    {
                        _stopwatch.Restart();

                        // Busy spin loop. Can't use Spinwait.SpinUntil because that falls through to an idle loop after a few (?) milliseconds
                        // 1ms = 10000 ticks
                        while (_stopwatch.ElapsedTicks < 1000L) { }
                    }
                    else
                    {
                        // BUGBUG this can cause weird reentrant problems if the default realtime provider uses this object as its inner wait provider
                        _rateLimiter.Limit(DefaultRealTimeProvider.Singleton, CancellationToken.None);
                    }

                    // Signal all waiting threads
                    int waitingThreads = Math.Min(_numWaitingThreads - _tickSignal.CurrentCount, MAX_WAITING_THREADS);
                    if (waitingThreads > 0)
                    {
                        _tickSignal.Release(waitingThreads);
                    }
                }
            });

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        // This class can potentially leave a zombie thread looping forever so make sure we dispose of it
        ~SpinwaitHighPrecisionWaitProvider()
        {
            Dispose(false);
        }

        public void Wait(double milliseconds, CancellationToken cancelToken)
        {
            if (milliseconds <= 0)
            {
                return;
            }

            long targetTicks = (long)(milliseconds * (double)TimeSpan.TicksPerMillisecond);
            ValueStopwatch elapsedTime = ValueStopwatch.StartNew();
            Interlocked.Increment(ref _numWaitingThreads);
            while (elapsedTime.ElapsedTicks < targetTicks)
            {
                _tickSignal.Wait(cancelToken);
            }

            Interlocked.Decrement(ref _numWaitingThreads);
        }

        public async Task WaitAsync(double milliseconds, CancellationToken cancelToken)
        {
            if (milliseconds <= 0)
            {
                return;
            }

            long targetTicks = (long)(milliseconds * (double)TimeSpan.TicksPerMillisecond);
            ValueStopwatch elapsedTime = ValueStopwatch.StartNew();
            Interlocked.Increment(ref _numWaitingThreads);
            while (elapsedTime.ElapsedTicks < targetTicks)
            {
                await _tickSignal.WaitAsync(cancelToken);
            }

            Interlocked.Decrement(ref _numWaitingThreads);
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

            _running = false;
            try
            {
                _backgroundThread.Await();
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            if (disposing)
            {
            }
        }
    }
}
