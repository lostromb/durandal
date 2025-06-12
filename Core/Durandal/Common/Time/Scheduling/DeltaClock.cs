namespace Durandal.Common.Time.Scheduling
{
    using Durandal.Common.Utils;
    using Durandal.Common.Time;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Durandal.Common.ServiceMgmt;

    /// <summary>
    /// This class represents a non-threaded scheduler based on a real-time delta clock.
    /// Its purpose is to schedule tasks "asynchronously" without actually requiring threads.
    /// Tasks can be scheduled at any time, after which a call to WaitForNextEvent()
    /// will BLOCK until the time the next event is scheduled. Note that this class is thread-safe,
    /// concurrent waits and schedulings will still work, though output ordering is not guaranteed
    /// </summary>
    /// <typeparam name="T">The type of events to be scheduled by this clock; usually this will contain a state object and a delegate</typeparam>
    public class DeltaClock<T> : IDisposable
    {
        /// <summary>
        /// The main event queue
        /// </summary>
        private readonly IList<ClockEvent<T>> _events = new List<ClockEvent<T>>();

        /// <summary>
        /// A signal that this clock has been stopped. Once this is triggered,
        /// all calls to WaitForNextEvent will instantly return null
        /// </summary>
        private readonly CancellationTokenSource _clockStopped = new CancellationTokenSource();

        /// <summary>
        /// Provides the definition of "real time" for the purpose of the scheduler.
        /// </summary>
        private readonly IRealTimeProvider _realTime;

        private int _disposed = 0;

        public DeltaClock(IRealTimeProvider realTime = null)
        {
            _realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~DeltaClock()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Schedules an event to be run ___ milliseconds from the current system time
        /// </summary>
        /// <param name="eventObject">An object representing the thing to be scheduled</param>
        /// <param name="msecOffset">The time offset</param>
        public void ScheduleEvent(T eventObject, int msecOffset)
        {
            ScheduleEventAbsolute(eventObject, GetCurrentTime() + msecOffset);
        }

        /// <summary>
        /// Schedules an event to be run ___ milliseconds from the current system time
        /// </summary>
        /// <param name="eventObject">An object representing the thing to be scheduled</param>
        /// <param name="msecOffset">The time offset</param>
        public void ScheduleEvent(T eventObject, long msecOffset)
        {
            ScheduleEventAbsolute(eventObject, GetCurrentTime() + msecOffset);
        }

        /// <summary>
        /// Schedules an event to be run some time interval in the future from the current system time
        /// </summary>
        /// <param name="eventObject">An object representing the thing to be scheduled</param>
        /// <param name="offset">The time offset</param>
        public void ScheduleEvent(T eventObject, TimeSpan offset)
        {
            ScheduleEventAbsolute(eventObject, GetCurrentTime() + (long)offset.TotalMilliseconds);
        }

        /// <summary>
        /// Schedules an event to be run at an absolute time value
        /// </summary>
        /// <param name="eventObject">An object representing the thing to be scheduled</param>
        /// <param name="absoluteTime">The time to schedule the event at</param>
        public void ScheduleEventAbsolute(T eventObject, long absoluteTime)
        {
            if (_clockStopped.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(DeltaClock<T>), "Cannot schedule events to a stopped DeltaClock");
            }

            ClockEvent<T> newEvent = new ClockEvent<T>()
            {
                EventObject = eventObject,
                Time = absoluteTime
            };

            lock (_events)
            {
                // Insert the event into the proper place on the list, based on time
                int insertIndex;
                for (insertIndex = 0;
                    insertIndex < _events.Count && _events[insertIndex].Time < absoluteTime;
                    insertIndex++)
                {
                }

                _events.Insert(insertIndex, newEvent);
            }
        }

        /// <summary>
        /// Stops this timer.
        /// </summary>
        public void Stop()
        {
            _clockStopped.Cancel();
            _clockStopped.Dispose();
        }

        /// <summary>
        /// Blocks the calling thread until the time of the next event. Once that time is reached,
        /// returns the next event in the queue to be executed.
        /// If the clock is stopped, all current and subsequent calls to this method will instantly return null.
        /// </summary>
        /// <returns>The next event to be run, or null</returns>
        public T WaitForNextEvent(CancellationToken cancelToken)
        {
            if (_clockStopped.IsCancellationRequested)
            {
                return default(T);
            }

            ClockEvent<T> nextEvent = null;

            try
            {
                lock (_events)
                {
                    if (!_events.Any())
                    {
                        return default(T);
                    }

                    // Dequeue the event
                    nextEvent = _events.First();
                    _events.RemoveAt(0);
                }

                // OPT creating a merged cancellation source here is costly; any way we can cut it down?
                long currentTime = GetCurrentTime();
                using (CancellationTokenSource mergedCancelSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _clockStopped.Token))
                {
                    if (currentTime < nextEvent.Time)
                    {
                        // BLOCKS THE CALLING THREAD UNTIL THE NEXT EVENT'S TRIGGER TIME OR THE CLOCK IS STOPPED
                        _realTime.Wait(TimeSpan.FromMilliseconds(nextEvent.Time - currentTime), mergedCancelSource.Token);
                        if (mergedCancelSource.IsCancellationRequested)
                        {
                            // This path means that the clock has been stopped or the wait was aborted
                            return default(T);
                        }
                    }
                }

                // And return its action
                return nextEvent.EventObject;
            }
            catch (OperationCanceledException)
            {
                return default(T);
            }
        }

        /// <summary>
        /// Blocks the calling thread until the time of the next event. Once that time is reached,
        /// returns the next event in the queue to be executed.
        /// If the clock is stopped, all current and subsequent calls to this method will instantly return null.
        /// </summary>
        /// <returns>The next event to be run, or null</returns>
        public async Task<T> WaitForNextEventAsync(CancellationToken cancelToken)
        {
            if (_clockStopped.IsCancellationRequested)
            {
                return default(T);
            }

            ClockEvent<T> nextEvent = null;

            try
            {
                lock (_events)
                {
                    if (!_events.Any())
                    {
                        return default(T);
                    }

                    // Dequeue the event
                    nextEvent = _events.First();
                    _events.RemoveAt(0);
                }

                // OPT creating a merged cancellation source here is costly; any way we can cut it down?
                long currentTime = GetCurrentTime();
                using (CancellationTokenSource mergedCancelSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, _clockStopped.Token))
                {
                    if (currentTime < nextEvent.Time)
                    {
                        // BLOCKS THE CALLING THREAD UNTIL THE NEXT EVENT'S TRIGGER TIME OR THE CLOCK IS STOPPED
                        await _realTime.WaitAsync(TimeSpan.FromMilliseconds(nextEvent.Time - currentTime), mergedCancelSource.Token).ConfigureAwait(false);
                        if (mergedCancelSource.IsCancellationRequested)
                        {
                            // This path means that the clock has been stopped or the wait was aborted
                            return default(T);
                        }
                    }
                }

                // And return its action
                return nextEvent.EventObject;
            }
            catch (OperationCanceledException)
            {
                return default(T);
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
                _clockStopped.Dispose();
            }
        }

        /// <summary>
        /// Gets the current absolute epoch time in ms
        /// </summary>
        /// <returns>The current system time</returns>
        private long GetCurrentTime()
        {
            return _realTime.TimestampMilliseconds;
        }

        /// <summary>
        /// Represents a single scheduled event on the delta clock
        /// </summary>
        /// <typeparam name="E">The type of events to be scheduled on this clock</typeparam>
        private class ClockEvent<E>
        {
            /// <summary>
            /// Gets or sets a generic state object for the event to be scheduled
            /// </summary>
            public E EventObject
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets the desired epoch time to execute this event
            /// </summary>
            public long Time
            {
                get;
                set;
            }
        }
    }
}
