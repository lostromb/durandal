namespace Durandal.Common.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Durandal.Common.IO;
    using Durandal.Common.Time;

    /// <summary>
    /// Implements a simple realtime rate or throughput counter.
    /// This class is thread-safe.
    /// </summary>
    public class RateCounter : IDisposable
    {
        private readonly Queue<PooledBuffer<RateCountedEvent>> _events = new Queue<PooledBuffer<RateCountedEvent>>();
        private readonly object _mutex = new object();
        private readonly long _windowSizeTicks;
        private readonly IRealTimeProvider _realTime;
        private readonly long _granularityTicks;

#pragma warning disable CA2213 // Dispose of IDisposable member fields - these are references to objects in the events queue
        private PooledBuffer<RateCountedEvent> _oldestBlock; // never null
        private PooledBuffer<RateCountedEvent> _newestBlock; // never null
#pragma warning restore CA2213
        private int _oldestEntryIdx = 0;
        private int _currentEntryIdx = 0;
        private uint _entryCount = 0;
        private double _totalWeight = 0;

        private struct RateCountedEvent
        {
            public long Timestamp;
            public double Weight;
        }

        /// <summary>
        /// Builds a new rate counter
        /// </summary>
        /// <param name="windowSize">The amount of time to use for a window function. Smaller window = more granular, but more noisy results</param>
        /// <param name="customTimeProvider">A custom time provider, if your scenario is not being run at real time. If null, the regular wallclock time is used.</param>
        /// <param name="granularity">Optional parameter to control the granularity (precision) of the rate counter estimates, to lower allocations in this class.
        /// Defaults to 1% of the overall time window.</param>
        public RateCounter(TimeSpan windowSize, IRealTimeProvider customTimeProvider = null, TimeSpan? granularity = null)
        {
            if (windowSize <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be a positive value");
            }

            _windowSizeTicks = windowSize.Ticks;
            if (granularity.HasValue)
            {
                if (granularity <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(granularity), "Granularity must be greater than zero");
                }

                _granularityTicks = granularity.Value.Ticks;
            }
            else
            {
                _granularityTicks = Math.Max(1, _windowSizeTicks / 100);
            }

            _realTime = customTimeProvider ?? DefaultRealTimeProvider.Singleton;
            _newestBlock = BufferPool<RateCountedEvent>.Rent();
            _oldestBlock = _newestBlock;
            _events.Enqueue(_newestBlock);
        }

        /// <summary>
        /// Signals that an event happened at the current time, which increments the rate counter.
        /// The definition of "current time" depends on the IRealTimeProvider passed into the constructor, which
        /// is wallclock time by default
        /// </summary>
        public void Increment()
        {
            IncrementInternal(1.0);
        }

        /// <summary>
        /// Signals that multiple events happened at the current time, which increments the rate counter.
        /// The definition of "current time" depends on the IRealTimeProvider passed into the constructor, which
        /// is wallclock time by default. The definition of "count" is also vague - it could mean measurements per second,
        /// it could be the "weight" of a single event, whatever.
        /// </summary>
        /// <param name="count">The number to increment the counter by, or the "weight" of this event.</param>
        public void Increment(double count)
        {
            IncrementInternal(count);
        }

        private void IncrementInternal(double weight)
        {
            if (double.IsNaN(weight) || double.IsInfinity(weight))
            {
                throw new ArgumentException("Weight must be a real number");
            }

            weight.AssertPositive(nameof(weight));
            long curTime = _realTime.TimestampTicks;

            lock (_mutex)
            {
                // See if we're below granularity threshold, in which case we just update the previous event
                // instead of creating a new event
                ref RateCountedEvent currentBin = ref _newestBlock.Buffer[_currentEntryIdx];
                if ((curTime - currentBin.Timestamp) < _granularityTicks)
                {
                    currentBin.Weight += weight;
                }
                else
                {
                    _currentEntryIdx++;

                    if (_currentEntryIdx >= _newestBlock.Buffer.Length)
                    {
                        _newestBlock = BufferPool<RateCountedEvent>.Rent();
                        _currentEntryIdx = 0;
                        _events.Enqueue(_newestBlock);
                    }

                    // Add the new weighted event
                    _newestBlock.Buffer[_currentEntryIdx] = new RateCountedEvent() { Timestamp = curTime, Weight = weight };
                }

                _entryCount++;
                _totalWeight += weight;
                PruneOutdatedEvents(curTime);
            }
        }

        /// <summary>
        /// Retrieves the current rate, in events per second.
        /// </summary>
        public double Rate
        {
            get
            {
                long curTime = _realTime.TimestampTicks;

                lock (_mutex)
                {
                    // Remove all outdated events first (otherwise the rate would never decay properly if events stopped)
                    PruneOutdatedEvents(curTime);
                    return _totalWeight * 10_000_000.0 / (double)_windowSizeTicks;
                }
            }
        }

        private void PruneOutdatedEvents(long curTime)
        {
            long cutoffTime = curTime - _windowSizeTicks;
            while (_entryCount > 0 && _oldestBlock.Buffer[_oldestEntryIdx].Timestamp < cutoffTime)
            {
                _totalWeight -= _oldestBlock.Buffer[_oldestEntryIdx].Weight;
                _oldestEntryIdx++;
                _entryCount--;
                if (_oldestEntryIdx >= _oldestBlock.Buffer.Length)
                {
                    if (_entryCount == 0)
                    {
                        // Rare case: we ran out just at the end of a block. In which case we can reuse the current block
                        // (we don't want the queue to empty or any of our blocks to become null)
                        _currentEntryIdx = 0;
                        _newestBlock.Buffer[0] = new RateCountedEvent() { Timestamp = curTime, Weight = 0 };
                    }
                    else
                    {
                        _events.Dequeue()?.Dispose();
                        _oldestBlock = _events.Peek();
                    }

                    _oldestEntryIdx = 0;
                }
            }
        }

        public void Dispose()
        {
            while (_events.Count > 0)
            {
                _events.Dequeue()?.Dispose();
            }
        }
    }
}
