using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Events
{
    /// <summary>
    /// Creates a channel which listens to a specific event and lets you asynchronously wait (tentatively) for an event of that type to come in,
    /// after which you can access the event's source and arguments.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class EventRecorder<T> : IDisposable where T : EventArgs
    {
        private readonly BufferedChannel<CapturedEvent<T>> _eventChannel = new BufferedChannel<CapturedEvent<T>>();
        private int _disposed = 0;

        public EventRecorder()
        {
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~EventRecorder()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Intercepts a regular event. Use this method as a delegate when subscribing to a native event.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="args"></param>
        public void HandleEvent(object source, T args)
        {
            _eventChannel.Send(new CapturedEvent<T>(source, args));
        }

        /// <summary>
        /// Intercepts an asynchronous event. Use this method as a delegate when subscribing to an <see cref="AsyncEvent{TArgs}"/> .
        /// </summary>
        /// <param name="source">The sender of the event</param>
        /// <param name="args">The EventArgs being sent</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns></returns>
        public Task HandleEventAsync(object source, T args, IRealTimeProvider realTime)
        {
            return _eventChannel.SendAsync(new CapturedEvent<T>(source, args)).AsTask();
        }

        /// <summary>
        /// Clears all captured events.
        /// </summary>
        public void Reset()
        {
            _eventChannel.Clear();
        }

        /// <summary>
        /// Waits for an event to come within a specific timeout.
        /// Setting the timeout to TimeSpan.Zero will do a non-blocking tentative retrieval.
        /// </summary>
        /// <param name="cancelToken">A cancellation token for this operation.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <param name="timeout">The maximum amount of time to wait for the event to come.</param>
        /// <returns>A retrieve result which may or may not contain a captured event.
        /// If multiple events are captured during the time period, this will return the one that occurred earliest.
        /// If an event was already captured <i>before</i> this method was called, it will be returned in FIFO order.</returns>
        public Task<RetrieveResult<CapturedEvent<T>>> WaitForEvent(CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan timeout)
        {
            return _eventChannel.TryReceiveAsync(cancelToken, realTime, timeout).AsTask();
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
                _eventChannel.Dispose();
            }
        }
    }
}
