using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// Cancellation token source that operates on virtual time provided by an IRealTimeProvider rather than the default system clock time, for unit test compatability.
    /// FIXME This class has a few issues that can still cause desyncs, especially when running optimized tests in Release mode
    /// </summary>
    public class NonRealTimeCancellationTokenSource : CancellationTokenSource
    {
        private CancellationTokenSource _isBeingDisposed = null;
        private volatile bool _threadFinished;
        private int _disposed = 0;

        public NonRealTimeCancellationTokenSource(IRealTimeProvider realTime, TimeSpan timeout)
        {
            if (realTime.IsForDebug)
            {
                // If we are using a debug time provider, do the slow path of spinning a thread to monitor the timeout in virtual time
                IRealTimeProvider forkedTime = realTime.Fork("NonRealTimeCancellationTokenSource");
                _isBeingDisposed = new CancellationTokenSource();
                CancellationToken cancelTokenClosure = _isBeingDisposed.Token;
                Task.Run(() =>
                {
                    try
                    {
                        // TODO - wait for a smaller timeout?
                        forkedTime.Wait(timeout, cancelTokenClosure);
                        if (_disposed == 0) // This check isn't actually guaranteed to work. There needs to be an actual mutex or something guarding it.
                        {
                            Cancel();
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (ObjectDisposedException)
                    {
                        // FIXME this can happen if the inner cancellation source is disposed - need to do something besides swallow the exception
                        //e.GetHashCode();
                    }
                    finally
                    {
                        forkedTime.Merge();
                        _threadFinished = true;
                    }
                });
            }
            else
            {
                // If we are using a normal time provider, just CancelAfter like normal
                CancelAfter(timeout);
                _threadFinished = true;
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

                _isBeingDisposed?.Cancel();

                // Tenative spinwait for the background thread to die
                ValueStopwatch timer = ValueStopwatch.StartNew();
                while (!_threadFinished && timer.Elapsed < TimeSpan.FromMilliseconds(100)) { }

                if (disposing)
                {
                    _isBeingDisposed?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
