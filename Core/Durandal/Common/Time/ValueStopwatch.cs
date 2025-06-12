using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Durandal.Common.Time
{
    /// <summary>
    /// Functionally equivalent to Stopwatch, except it's a ref type so you're
    /// not always allocating stopwatch objects during your instrumentation.
    /// The limitation is that behavior gets weird when you pass values by copy.
    /// </summary>
    public struct ValueStopwatch
    {
        // this won't necessarily always be the time the watch was started,
        // in the case where we stop + start the watch multiple times.
        // It's only here to store the delta.
        // Please note that this is stored in STOPWATCH ticks, not normal ticks!
        private long _startTimestamp;

        // only valid if the watch is stopped. Otherwise, use Stopwatch.GetTimestamp()
        // to get the current reading.
        private long _endTimestamp;

        private bool _running;

        public ValueStopwatch(bool running = false)
        {
            _startTimestamp = Stopwatch.GetTimestamp();
            _endTimestamp = _startTimestamp;
            _running = running;
        }

        public void Start()
        {
            if (!_running)
            {
                _running = true;
                // Carry over cumulative time if we have stopped and then started
                long existingTicks = _endTimestamp - _startTimestamp;
                _startTimestamp = Stopwatch.GetTimestamp() - existingTicks;
            }
        }

        public void Stop()
        {
            if (_running)
            {
                _endTimestamp = Stopwatch.GetTimestamp();
                _running = false;
            }
        }

        public void Restart()
        {
            _startTimestamp = Stopwatch.GetTimestamp();
            _endTimestamp = _startTimestamp;
            _running = true;
        }

        /// <summary>
        /// Returns the total amount of time this watch has been running.
        /// </summary>
        public TimeSpan Elapsed
        {
            get
            {
                return TimeSpan.FromTicks(ElapsedTicks);
            }
        }

        /// <summary>
        /// Returns the total amount of time this watch has been running, in ticks.
        /// </summary>
        public long ElapsedTicks
        {
            get
            {
                if (_running)
                {
                    return (Stopwatch.GetTimestamp() - _startTimestamp) * TimeSpan.TicksPerSecond / Stopwatch.Frequency;
                }
                else
                {
                    return (_endTimestamp - _startTimestamp) * TimeSpan.TicksPerSecond / Stopwatch.Frequency;
                }
            }
        }

        /// <summary>
        /// Returns the total amount of time this watch has been running, in whole milliseconds.
        /// </summary>
        public long ElapsedMilliseconds
        {
            get
            {
               return ElapsedTicks / TimeSpan.TicksPerMillisecond;
            }
        }

        /// <summary>
        /// Returns the total amount of time this watch has been running, in fractional milliseconds.
        /// </summary>
        public double ElapsedMillisecondsPrecise()
        {
            if (_running)
            {
                return (double)(Stopwatch.GetTimestamp() - _startTimestamp) * 1000.0 / (double)Stopwatch.Frequency;
            }
            else
            {
                return (double)(_endTimestamp - _startTimestamp) * 1000.0 / (double)Stopwatch.Frequency;
            }
        }

        /// <summary>
        /// Starts a new ValueStopwatch and returns it.
        /// </summary>
        /// <returns>A newly created running stopwatch.</returns>
        public static ValueStopwatch StartNew()
        {
            return new ValueStopwatch(true);
        }
    }
}
