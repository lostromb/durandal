using Durandal.Common.Time;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// Implements a real-time rate limiter to restrict an operation to only a few times per second (or once every N seconds).
    /// This class is not thread-safe.
    /// </summary>
    public class RateLimiter
    {
        /// <summary>
        /// The minimum amount of delay we need to accumulate before triggering a sleep.
        /// Default 15ms to match the behavior of Windows timers.
        /// </summary>
        private readonly TimeSpan _minSleepTime;

        /// <summary>
        /// Store a moving average of previous intervals
        /// </summary>
        private readonly double[] _historyTicks;
        private readonly double[] _historyResourceUse;

        /// <summary>
        /// Points to the oldest entry in the moving average set
        /// </summary>
        private int _historyPointer;

        /// <summary>
        /// Current average interval of the set
        /// </summary>
        private double _averageIntervalTicks;

        /// <summary>
        /// Current average resource use of the set
        /// </summary>
        private double _averageResourceUse;

        /// <summary>
        /// The time that the last call to Limit() finished
        /// </summary>
        private double _lastInvocationTimeTicks;

        /// <summary>
        /// For threadsleep-based timers, this is the number of ticks of waiting that is currently accumulated
        /// </summary>
        private double _accumulatedWaitTimeTicks = 0;

        /// <summary>
        /// Desired limiter freqency in hertz
        /// </summary>
        private double _targetFrequencyHz;

        /// <summary>
        /// The desired limiting interval measured in stopwatch ticks
        /// </summary>
        private double _targetIntervalTicks;

        /// <summary>
        /// Constructs a new rate limiter with the given frequency (Hz or iterations per second) and an optional smoothing factor
        /// </summary>
        /// <param name="desiredRateHz">The desired rate to limit to, in hertz. Or more generically, in "resources per second".</param>
        /// <param name="smoothingFactor">The number of increments to keep in a history for estimating future average wait times</param>
        /// <param name="minimumSleepTime">The minimum amount of time to spend sleeping (default 15ms).</param>
        public RateLimiter(double desiredRateHz, int smoothingFactor = 100, TimeSpan? minimumSleepTime = null)
        {
            desiredRateHz.AssertPositive(nameof(desiredRateHz));
            smoothingFactor.AssertPositive(nameof(smoothingFactor));

            _minSleepTime = minimumSleepTime.GetValueOrDefault(TimeSpan.FromMilliseconds(15));
            if (_minSleepTime <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumSleepTime), "Minimum sleep time must be a positive time span");
            }

            _lastInvocationTimeTicks = -1;
            _historyTicks = new double[smoothingFactor];
            _historyResourceUse.AsSpan().Fill(0);
            _averageIntervalTicks = 0;
            _historyResourceUse = new double[smoothingFactor];
            _historyResourceUse.AsSpan().Fill(1);
            _averageResourceUse = 1;
            _historyPointer = 0;
            TargetFrequency = desiredRateHz;
        }

        /// <summary>
        /// Gets or sets the target frequency of the limiter, in hertz.
        /// </summary>
        public double TargetFrequency
        {
            get
            {
                return _targetFrequencyHz;
            }
            set
            {
                _targetFrequencyHz = value;
                _targetIntervalTicks = TimeSpan.TicksPerSecond / value;
            }
        }

        /// <summary>
        /// Causes the calling thread to sleep in such a way that the frequency that this method finishes is limited to the target frequency of the limiter.
        /// </summary>
        /// <param name="timeProvider">A definition of real time, to be used as the blocking object for the wait</param>
        /// <param name="cancelToken">A cancellation token to abort the wait.</param>
        /// <param name="resourcesUsed">For limiters that are based on "resources used per second" rather than just hertz, this is the number of resources used by this call</param>
        /// <returns>The amount of (potentially virtual) time that was spent waiting on the limiter, or zero if no waiting happened.</returns>
        public TimeSpan Limit(IRealTimeProvider timeProvider, CancellationToken cancelToken, double resourcesUsed = 1)
        {
            resourcesUsed.AssertPositive(nameof(resourcesUsed));
            long ticksToWait = GetTicksToWait(timeProvider, resourcesUsed);
            TimeSpan waitTime = TimeSpan.FromTicks(ticksToWait);

            if (waitTime >= _minSleepTime)
            {   
                // Opportunistically consume from the wait accumulator before actually doing the wait
                _accumulatedWaitTimeTicks -= ticksToWait;
                long actualWaitStart = timeProvider.TimestampTicks;
                timeProvider.Wait(waitTime, cancelToken);
                long actualWaitTicks = timeProvider.TimestampTicks - actualWaitStart;
                // After the wait, assume the processor did not wait for PRECISELY what we asked for,
                // so adjust the accumulator after the fact
                _accumulatedWaitTimeTicks += ticksToWait - actualWaitTicks;
            }

            // Mark the END of the wait loop as the last invocation time. This prevents the wait duration from factoring in to future frametimes and thus causing a feedback loop
            _lastInvocationTimeTicks = timeProvider.TimestampTicks;
            return waitTime;
        }

        /// <summary>
        /// Causes the calling thread to sleep in such a way that the frequency that this method finishes is limited to the target frequency of the limiter.
        /// </summary>
        /// <param name="timeProvider">A definition of real time, to be used as the blocking object for the wait</param>
        /// <param name="cancelToken">A cancellation token to abort the wait.</param>
        /// <param name="resourcesUsed">For limiters that are based on "resources used per second" rather than just hertz, this is the number of resources used by this call</param>
        /// <returns>The amount of (potentially virtual) time that was spent waiting on the limiter, or zero if no waiting happened.</returns>
        public async Task<TimeSpan> LimitAsync(IRealTimeProvider timeProvider, CancellationToken cancelToken, double resourcesUsed = 1)
        {
            resourcesUsed.AssertPositive(nameof(resourcesUsed));
            long ticksToWait = GetTicksToWait(timeProvider, resourcesUsed);
            TimeSpan waitTime = TimeSpan.FromTicks(ticksToWait);

            if (waitTime >= _minSleepTime)
            {
                // Opportunistically consume from the wait accumulator before actually doing the wait
                _accumulatedWaitTimeTicks -= ticksToWait;
                long actualWaitStart = timeProvider.TimestampTicks;
                await timeProvider.WaitAsync(waitTime, cancelToken).ConfigureAwait(false);
                long actualWaitTicks = timeProvider.TimestampTicks - actualWaitStart;
                // After the wait, assume the processor did not wait for PRECISELY what we asked for,
                // so adjust the accumulator after the fact
                _accumulatedWaitTimeTicks += ticksToWait - actualWaitTicks;
            }

            // Mark the END of the wait loop as the last invocation time. This prevents the wait duration from factoring in to future frametimes and thus causing a feedback loop
            _lastInvocationTimeTicks = timeProvider.TimestampTicks;
            return waitTime;
        }

        private long GetTicksToWait(IRealTimeProvider timeProvider, double resourcesUsed)
        {
            if (_lastInvocationTimeTicks < 0)
            {
                // On first invocation, don't wait at all
                return 0;
            }

            double lastIntervalTicks = (double)(timeProvider.TimestampTicks - _lastInvocationTimeTicks);

            // Update moving average rate incrementally
            double oldHistoryTicks = _historyTicks[_historyPointer];
            double oldHistoryResources = _historyResourceUse[_historyPointer];
            _historyTicks[_historyPointer] = lastIntervalTicks;
            _historyResourceUse[_historyPointer] = resourcesUsed;
            _historyPointer = (_historyPointer + 1) % _historyTicks.Length;
            _averageIntervalTicks += (lastIntervalTicks - oldHistoryTicks) / _historyTicks.Length;
            _averageResourceUse += (resourcesUsed - oldHistoryResources) / _historyResourceUse.Length;

            // Calculate how long to wait based on the difference between the current average rate and the desired target interval
            double waitTimeTicks = _averageResourceUse * (_targetIntervalTicks - _averageIntervalTicks);

            // Add the wait time to an accumulator and whenever we can sleep for 1ms or more, suspend the thread
            _accumulatedWaitTimeTicks += waitTimeTicks;
            if (_accumulatedWaitTimeTicks < 0 - _targetIntervalTicks)
            {
                // Don't let accumulator go too far in the negative, if rates are consistently lagging
                _accumulatedWaitTimeTicks = 0 - _targetIntervalTicks;
            }

            return (long)_accumulatedWaitTimeTicks;
        }
    }
}
