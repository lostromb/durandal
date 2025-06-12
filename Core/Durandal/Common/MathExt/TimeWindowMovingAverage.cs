using Durandal.Common.Time;
using System;

namespace Durandal.Common.MathExt
{
    /// <summary>
    /// A moving average class which is aware of real time and calculates a running average based on a fixed time window
    /// as well as a maximum number of rolling observations within that window. Useful for realtime reporting of metrics.
    /// This class is thread-safe.
    /// </summary>
    public class TimeWindowMovingAverage
    {
        /// <summary>
        /// The array that contains the data
        /// </summary>
        private readonly double[] _observations;

        /// <summary>
        /// The array that contains the timestamp for each observation
        /// </summary>
        private readonly long[] _timeStamps;

        /// <summary>
        /// A definition of real time
        /// </summary>
        private readonly IRealTimeProvider _realTime;

        /// <summary>
        /// The length of the time window in milliseconds
        /// </summary>
        private readonly long _windowLengthMs;

        /// <summary>
        /// The maximum number of observations to store at once
        /// </summary>
        private readonly int _maxObservations;

        /// <summary>
        /// Always points to the oldest data item in the set (the tail)
        /// </summary>
        private int _oldestObservation;

        /// <summary>
        /// Always points to one beyond the newest data item in the set
        /// </summary>
        private int _newestObservation;

        /// <summary>
        /// The number of observations currently in the time window
        /// </summary>
        private int _observationCount;
        
        /// <summary>
        /// The current average, continuously updated
        /// </summary>
        private double? _curAverage;
        
        /// <summary>
        /// Constructs a new moving average
        /// </summary>
        /// <param name="dataWindowSize">The maximum number of observations to factor into the average at one time</param>
        /// <param name="timeWindowSize">The time window that observations will remain valid</param>
        /// <param name="realTime">A definition of real time, or wallclock time if null</param>
        public TimeWindowMovingAverage(int dataWindowSize, TimeSpan timeWindowSize, IRealTimeProvider realTime = null)
        {
            if (dataWindowSize <= 0)
            {
                throw new ArgumentOutOfRangeException("Data window size must be above 0");
            }

            _realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            _observations = new double[dataWindowSize];
            _timeStamps = new long[dataWindowSize];
            _maxObservations = dataWindowSize;
            _oldestObservation = 0;
            _newestObservation = 0;
            _observationCount = 0;
            _curAverage = null;
            _windowLengthMs = (long)timeWindowSize.TotalMilliseconds;
        }
        
        /// <summary>
        /// Adds a new observation and updates the running average
        /// </summary>
        /// <param name="value"></param>
        public void Add(double value)
        {
            long curTime = _realTime.TimestampMilliseconds;
            lock (this)
            {
                CullExpiredObservations(curTime);

                if (_observationCount == 0)
                {
                    // Empty set - new value becomes the average
                    _curAverage = value;
                    _observations[_newestObservation] = value;
                    _timeStamps[_newestObservation] = curTime;
                    _newestObservation = (_newestObservation + 1) % _maxObservations;
                    _observationCount++;
                }
                else if (_observationCount == _maxObservations)
                {
                    // Full set - new value replaces old value directly
                    double oldValue = _observations[_oldestObservation];
                    _observations[_oldestObservation] = value;
                    _timeStamps[_oldestObservation] = curTime;
                    _curAverage += (value / (double)_observationCount) - (oldValue / (double)_observationCount);
                    _oldestObservation = (_oldestObservation + 1) % _maxObservations;
                    _newestObservation = (_newestObservation + 1) % _maxObservations;
                }
                else
                {
                    // Half-filled set - append new value
                    _observations[_newestObservation] = value;
                    _timeStamps[_newestObservation] = curTime;
                    _curAverage = ((_curAverage.Value * (double)_observationCount) + value) / ((double)_observationCount + 1);
                    _newestObservation = (_newestObservation + 1) % _maxObservations;
                    _observationCount++;
                }
            }
        }
        
        /// <summary>
        /// The current average value factoring in observations within the time window only. If no observations are within the window, this returns null
        /// </summary>
        public double? Average
        {
            get
            {
                lock (this)
                {
                    CullExpiredObservations(_realTime.TimestampMilliseconds);
                    return _curAverage;
                }
            }
        }

        /// <summary>
        /// Resets the average value back to null and removes all observations
        /// </summary>
        public void Reset()
        {
            lock (this)
            {
                _oldestObservation = 0;
                _newestObservation = 0;
                _observationCount = 0;
                _curAverage = null;
            }
        }

        public override string ToString()
        {
            return Average.ToString();
        }

        private void CullExpiredObservations(long curTime)
        {
            long cutoff = curTime - _windowLengthMs;
            while (_observationCount > 0 &&
                _timeStamps[_oldestObservation] < cutoff)
            {
                if (_observationCount == 1)
                {
                    // Set is becoming empty, so just reset all counters
                    _oldestObservation = 0;
                    _newestObservation = 0;
                    _observationCount = 0;
                    _curAverage = null;
                }
                else
                {
                    // Remove one old entry from set
                    // OPT I can probably do this faster if I batched all of the expired observations into one, but whatever
                    _curAverage = ((_curAverage.Value * (double)_observationCount) - _observations[_oldestObservation]) / ((double)_observationCount - 1);
                    _oldestObservation = (_oldestObservation + 1) % _maxObservations;
                    _observationCount--;
                }
            }

            // If curAverage is very close to zero due to rounding errors, round to zero
            if (_curAverage < 0.000001 && _curAverage > -0.000001)
            {
                _curAverage = 0;
            }
        }
    }
}
