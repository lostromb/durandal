using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Versioning;

namespace Durandal.Common.Time
{
    /// <summary>
    /// Implementation of <see cref="IHighPrecisionWaitProvider"/> which uses P/Invoke to access high-precision Windows multimedia timers.
    /// This timer is then used to accurately pulse waiting events to achieve precise delays with low overhead.
    /// See https://docs.microsoft.com/en-US/windows/win32/multimedia/multimedia-timers
    /// </summary>
#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    public class Win32HighPrecisionWaitProvider : IHighPrecisionWaitProvider
    {
        private const int MAX_WAITING_THREADS = 1000;
        private readonly SemaphoreSlim _tickSignal;
        private readonly TimerEventHandler _callbackHandler;
        private int _numWaitingThreads = 0;
        private long _previousTickTime;
        private int _timerId;
        private int _disposed = 0;

        public Win32HighPrecisionWaitProvider()
        {
            _tickSignal = new SemaphoreSlim(0, MAX_WAITING_THREADS);
            _previousTickTime = timeGetTime();
            _callbackHandler = TimerHandler; // assign the callback method to an explicit variable to keep the reference alive in the garbage collector
            
            // Ensure that timers support the requested resolution of 1ms
            int err = timeBeginPeriod(1);
            if (err != 0)
            {
                throw new InvalidOperationException("Multimedia timer operation returned HRESULT " + err);
            }

            // Start the timer
            _timerId = timeSetEvent(1, 0, _callbackHandler, IntPtr.Zero, eventType: 1);

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        ~Win32HighPrecisionWaitProvider()
        {
            Dispose(false);
        }

        public void Wait(double milliseconds, CancellationToken cancelToken)
        {
            if (milliseconds <= 0)
            {
                return;
            }

            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Win32HighPrecisionWaitProvider));
            }

            long targetTicks = (long)((milliseconds - 0.5) * (double)TimeSpan.TicksPerMillisecond);
            ValueStopwatch elapsedTime = ValueStopwatch.StartNew();
            Interlocked.Increment(ref _numWaitingThreads);
            try
            {
                while (elapsedTime.ElapsedTicks < targetTicks && _disposed == 0)
                {
                    _tickSignal.Wait();
                    cancelToken.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _numWaitingThreads);
            }
        }

        /// <summary>
        /// This implementation is not actually async since the task callbacks and signaling would cause too much allocation.
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        public async Task WaitAsync(double milliseconds, CancellationToken cancelToken)
        {
            if (milliseconds <= 0)
            {
                return;
            }

            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(Win32HighPrecisionWaitProvider));
            }

            long targetTicks = (long)((milliseconds - 0.5) * (double)TimeSpan.TicksPerMillisecond);
            ValueStopwatch elapsedTime = ValueStopwatch.StartNew();
            Interlocked.Increment(ref _numWaitingThreads);
            try
            {
                while (elapsedTime.ElapsedTicks < targetTicks && _disposed == 0)
                {
                    // don't use cancellation token on the tick signal itself because it's running at very high precision,
                    // at which we point we don't care about the granularity of the cancel, and using the token causes lots of allocations.
                    // We could make this a synchronous wait to avoid Task<T> allocations, but doing so could also potentially block threads and cause thread pool starvation
                    await _tickSignal.WaitAsync().ConfigureAwait(false);
                    cancelToken.ThrowIfCancellationRequested();
                }
            }
            finally
            {
                Interlocked.Decrement(ref _numWaitingThreads);
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

            // Stop the timer
            int err = timeKillEvent(_timerId);
            if (err != 0)
            {
                err = timeEndPeriod(1);
            }

            // Wait for all waiting threads to finish
            ValueStopwatch timeout = ValueStopwatch.StartNew();
            while (_numWaitingThreads > 0 && timeout.ElapsedMilliseconds < 2000)
            {
                SignalAllThreads();
            }

            if (disposing)
            {
                _tickSignal?.Dispose();
            }
        }

        private void SignalAllThreads()
        {
            int waitingThreads = Math.Min(_numWaitingThreads - _tickSignal.CurrentCount, MAX_WAITING_THREADS);
            if (waitingThreads > 0)
            {
                _tickSignal.Release(waitingThreads);
            }
        }

        private void TimerHandler(int id, int msg, IntPtr user, int dw1, int dw2)
        {
            try
            {
                const double TOLERANCE = 0.95;
                long currentTime = timeGetTime();

                if (currentTime - _previousTickTime >= TOLERANCE)
                {
                    _previousTickTime = currentTime;
                    SignalAllThreads();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception in high precision callback timer: " + e.Message);
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }

        internal delegate void TimerEventHandler(int id, int msg, IntPtr user, int dw1, int dw2);

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        internal static extern int timeGetTime();

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        internal static extern int timeSetEvent(int delay, int resolution, TimerEventHandler handler, IntPtr user, int eventType);

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        internal static extern int timeKillEvent(int id);

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        internal static extern int timeBeginPeriod(int msec);

        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        internal static extern int timeEndPeriod(int msec);
    }
}
