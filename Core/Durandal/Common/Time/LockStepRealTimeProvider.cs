using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.MathExt;
using Durandal.Common.Logger;
using Durandal.API;

namespace Durandal.Common.Time
{
    /// <summary>
    /// This is a testing class that is designed to reconcile the actual real time clock (RTC)
    /// and the virtual time (VT) reported to the code. It is necessary because we want to be able
    /// to unit test the client core methods, including asynchronous audio, but we don't want those
    /// tests to be subject to the limitations of real time (i.e. we want to simulate time moving
    /// as quickly as possible). In order to accomplish this on multiple threads, this lock-step
    /// mechanism is needed (logically quite similar to a <see cref="Barrier"/>) to make sure that
    /// all threads have a unique time handle VT and that all of the handles increment at the same time RTC.
    /// It's messy and can cause infinite spinwaits if used wrong, but it works.
    /// </summary>
    public class LockStepRealTimeProvider : IRealTimeProvider
    {
        private static readonly TimeSpan DeadlockTimeout = TimeSpan.FromSeconds(30);

        // a singleton spinwaiter that will eat up one thread for the length of the program. fun!
        private static IHighPrecisionWaitProvider WAIT_PROVIDER;

        private static readonly bool USE_HIGH_PRECISION_SPINWAIT = true;

        /// <summary>
        /// An arbtirary number of milliseconds to use the "epoch" time, in case a user wants to offset
        /// the timestamp to something in the past and we should allow it.
        /// This should put the epoch time somewhere near April 1902
        /// </summary>
        private const long TIMEBASE_OFFSET = 60_000_000_000_000L;
        private readonly ILogger _logger;
        private Core _core;
        private int _forkId;

        /// <inheritdoc />
        public bool IsForDebug => true;

        public LockStepRealTimeProvider(ILogger logger)
        {
            _logger = logger;
            _core = new Core(_logger);
            _forkId = 0;
            
            // Don't create the wait provider statically so that we don't accidentally start a background thread when JIT hits this class.
            if (WAIT_PROVIDER == null)
            {
                IHighPrecisionWaitProvider newProvider = new SpinwaitHighPrecisionWaitProvider(USE_HIGH_PRECISION_SPINWAIT);
                IHighPrecisionWaitProvider oldProvider = Interlocked.Exchange(ref WAIT_PROVIDER, newProvider);
                oldProvider?.Dispose();
            }
        }

        private LockStepRealTimeProvider(Core core, string nameForDebug, string callerFilePath, string callerMemberName, int callerLineNumber)
        {
            nameForDebug = string.IsNullOrEmpty(nameForDebug) ? "Unknown" : nameForDebug;
            _core = core;
            _forkId = _core.CreateFork(_forkId, nameForDebug, callerFilePath, callerMemberName, callerLineNumber);
        }

        /// <inheritdoc />
        public IRealTimeProvider Fork(
            string nameForDebug,
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0)
        {
            return new LockStepRealTimeProvider(_core, nameForDebug, callerFilePath, callerMemberName, callerLineNumber);
        }

        /// <inheritdoc />
        public void Merge(
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0)
        {
            ThrowIfForkIsMerged();
            _core.MergeFork(_forkId, callerFilePath, callerMemberName, callerLineNumber);
            _forkId = 0 - _forkId;
        }

        /// <inheritdoc />
        public long TimestampMilliseconds
        {
            get
            {
                ThrowIfForkIsMerged();
                return (_core.GetTimestamp(_forkId) / 10000L) + TIMEBASE_OFFSET;
            }
        }

        /// <inheritdoc />
        public long TimestampTicks
        {
            get
            {
                ThrowIfForkIsMerged();
                return (_core.GetTimestamp(_forkId)) + (TIMEBASE_OFFSET * 10000L);
            }
        }

        /// <inheritdoc />
        public DateTimeOffset Time
        {
            get
            {
                ThrowIfForkIsMerged();
                return new DateTimeOffset(TimestampMilliseconds * 10000L, TimeSpan.Zero);
            }
        }

        /// <inheritdoc />
        public void Wait(TimeSpan time, CancellationToken cancelToken)
        {
            ThrowIfForkIsMerged();
            _core.Wait(time, _forkId, cancelToken);
        }

        /// <inheritdoc />
        public async Task WaitAsync(TimeSpan time, CancellationToken cancelToken)
        {
            ThrowIfForkIsMerged();
            await _core.WaitAsync(time, _forkId, cancelToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Method specific to this class. Advances the flow of time and returns only when all
        /// instances of LockStepRealTimeProvider are exactly at the target time VT.
        /// </summary>
        /// <param name="time">The amount of virtual time to step</param>
        public void Step(TimeSpan time)
        {
            ThrowIfForkIsMerged();
            _core.Step(time, _forkId, CancellationToken.None);
        }

        /// <summary>
        /// Steps forward the specified amount of time in small increments
        /// </summary>
        /// <param name="time">The total time to step forward</param>
        /// <param name="incrementMs">The size of each increment in ms</param>
        public void Step(TimeSpan time, int incrementMs)
        {
            ThrowIfForkIsMerged();
            int totalms = (int)time.TotalMilliseconds;
            for (int counter = 0; counter < totalms;)
            {
                int actualtime = Math.Min(incrementMs, totalms - counter);
                _core.Step(TimeSpan.FromMilliseconds(actualtime), _forkId, CancellationToken.None);
                counter += actualtime;
            }
        }

        public void Reset()
        {
            // fixme: messy
            _core = new Core(_logger);
            _forkId = 0;
        }

        private void ThrowIfForkIsMerged()
        {
            if (_forkId < 0)
            {
                throw new InvalidOperationException("Time fork has already been merged");
            }
        }

        private class TimeFork
        {
            public long Timestamp;
            public string ForkName;

            public TimeFork(long targetTime, string forkName)
            {
                Timestamp = targetTime;
                ForkName = forkName;
            }

            public override string ToString()
            {
                return ForkName;
            }
        }

        private class Core
        {
            private const int MAX_FORKS = 100;

            // The "ground truth" time that all forks should be synchronized against. Measured in ticks
            private long _targetTime = 0;

            // maps fork id => fork local time
            private readonly IDictionary<int, TimeFork> _aliveForks = new Dictionary<int, TimeFork>();

            // Count of how many instances of each fork are active - this can help with debugging
            private readonly Counter<string> _forkNameCounter = new Counter<string>();

            private readonly ILogger _logger;

            // Create the initial fork manually
            public Core(ILogger logger)
            {
                _aliveForks[0] = new TimeFork(0, "Root");
                _logger = logger;
            }

            public void Reset()
            {
                _targetTime = 0;
                _aliveForks.Clear();
                _aliveForks[0] = new TimeFork(0, "Root");
            }

            public long GetTimestamp(int forkId)
            {
                // Returns the wallclock time relative to the given fork
                lock (_aliveForks)
                {
                    return _aliveForks[forkId].Timestamp;
                }
            }

            /// <summary>
            /// This method will always be called by a worker thread, so we can block it indefinitely.
            /// </summary>
            /// <param name="time"></param>
            /// <param name="forkId"></param>
            /// <param name="cancelToken"></param>
            public void Wait(TimeSpan time, int forkId, CancellationToken cancelToken)
            {
                if (forkId == 0)
                {
                    // Waiting on fork 0 is equivalent to stepping all of the other forks forwards by that amount of time, so do it here
                    Step(time, forkId, cancelToken);
                }
                else
                {
#if LOCKSTEP_TIME_DEBUG
                    _logger.Log("FORK " + forkId + " WAIT FOR " + time.TotalMilliseconds, LogLevel.Vrb);
#endif
                    // Find out the time that we will come out of wait.
                    long wakeUpTime;
                    lock (_aliveForks)
                    {
                        if (!_aliveForks.ContainsKey(forkId))
                        {
#if LOCKSTEP_TIME_DEBUG
                            _logger.Log("FORK " + forkId + " WAS MERGED DURING WAIT", LogLevel.Vrb);
#endif
                            return;
                        }

                        wakeUpTime = _aliveForks[forkId].Timestamp + time.Ticks;
                        _aliveForks[forkId].Timestamp = Math.Min(_targetTime, wakeUpTime);
                    }

                    // Spinwait until that time
                    long origTargetTime = _targetTime;
                    while (wakeUpTime > _targetTime && !cancelToken.IsCancellationRequested)
                    {
                        WAIT_PROVIDER.Wait(1, cancelToken);
                        if (origTargetTime != _targetTime)
                        {
                            // Target time has changed, update this fork's time (this reports our progress to the Step() method)
                            lock (_aliveForks)
                            {
                                if (!_aliveForks.ContainsKey(forkId))
                                {
#if LOCKSTEP_TIME_DEBUG
                                    _logger.Log("FORK " + forkId + " WAS MERGED DURING WAIT", LogLevel.Vrb);
#endif
                                    return;
                                }

                                _aliveForks[forkId].Timestamp = Math.Min(_targetTime, wakeUpTime);
                            }

                            origTargetTime = _targetTime;
                        }
                    }

                    // Finalize this fork's timer
                    lock (_aliveForks)
                    {
                        if (!_aliveForks.ContainsKey(forkId))
                        {
#if LOCKSTEP_TIME_DEBUG
                            _logger.Log("FORK " + forkId + " WAS MERGED DURING WAIT", LogLevel.Vrb);
#endif
                            return;
                        }

                        _aliveForks[forkId].Timestamp = wakeUpTime;
                    }

#if LOCKSTEP_TIME_DEBUG
                    _logger.Log("FORK " + forkId + " DONE WAITING", LogLevel.Vrb);
#endif
                }
            }

            /// <summary>
            /// This method will always be called by a worker thread, so we can block it indefinitely.
            /// </summary>
            /// <param name="time"></param>
            /// <param name="forkId"></param>
            /// <param name="cancelToken"></param>
            public async Task WaitAsync(TimeSpan time, int forkId, CancellationToken cancelToken)
            {
                if (forkId == 0)
                {
                    // Waiting on fork 0 is equivalent to stepping all of the other forks forwards by that amount of time, so do it here
                    Step(time, forkId, cancelToken);
                }
                else
                {
#if LOCKSTEP_TIME_DEBUG
                    _logger.Log("FORK " + forkId + " WAIT FOR " + time.TotalMilliseconds, LogLevel.Vrb);
#endif
                    // Find out the time that we will come out of wait.
                    long wakeUpTime;
                    lock (_aliveForks)
                    {
                        if (!_aliveForks.ContainsKey(forkId))
                        {
#if LOCKSTEP_TIME_DEBUG
                            _logger.Log("FORK " + forkId + " WAS MERGED DURING WAIT", LogLevel.Vrb);
#endif
                            return;
                        }

                        wakeUpTime = _aliveForks[forkId].Timestamp + time.Ticks;
                        _aliveForks[forkId].Timestamp = Math.Min(_targetTime, wakeUpTime);
                    }

                    // Spinwait until that time
                    long origTargetTime = _targetTime;
                    while (wakeUpTime > _targetTime && !cancelToken.IsCancellationRequested)
                    {
                        await WAIT_PROVIDER.WaitAsync(1, cancelToken).ConfigureAwait(false);
                        if (origTargetTime != _targetTime)
                        {
                            // Target time has changed, update this fork's time (this reports our progress to the Step() method)
                            lock (_aliveForks)
                            {
                                if (!_aliveForks.ContainsKey(forkId))
                                {
#if LOCKSTEP_TIME_DEBUG
                                    _logger.Log("FORK " + forkId + " WAS MERGED DURING WAIT", LogLevel.Vrb);
#endif
                                    return;
                                }

                                _aliveForks[forkId].Timestamp = Math.Min(_targetTime, wakeUpTime);
                            }

                            origTargetTime = _targetTime;
                        }
                    }

                    cancelToken.ThrowIfCancellationRequested();

                    // Finalize this fork's timer
                    lock (_aliveForks)
                    {
                        if (!_aliveForks.ContainsKey(forkId))
                        {
#if LOCKSTEP_TIME_DEBUG
                            _logger.Log("FORK " + forkId + " WAS MERGED DURING WAIT", LogLevel.Vrb);
#endif
                            return;
                        }

                        _aliveForks[forkId].Timestamp = wakeUpTime;
                    }

#if LOCKSTEP_TIME_DEBUG
                _logger.Log("FORK " + forkId + " DONE WAITING", LogLevel.Vrb);
#endif
                }
            }

            public int CreateFork(int sourceFork,
                string debugName,
                string callerFilePath,
                string callerMemberName,
                int callerLineNumber)
            {
                long sourceTime = _targetTime;

                lock (_aliveForks)
                {
                    if (_aliveForks.ContainsKey(sourceFork))
                    {
                        sourceTime = _aliveForks[sourceFork].Timestamp;
                    }

                    for (int counter = 0; counter < MAX_FORKS; counter++)
                    {
                        if (!_aliveForks.ContainsKey(counter))
                        {
                            _aliveForks.Add(counter, new TimeFork(sourceTime, debugName));

#if DEBUG
                            _logger.Log("Creating time fork " + counter + " \"" + debugName + "\" at base time " + 
                                ((double)_aliveForks[counter].Timestamp / (double)TimeSpan.TicksPerSecond).ToString("F3") + " and target time " +
                                ((double)_targetTime / (double)TimeSpan.TicksPerSecond).ToString("F3"), LogLevel.Vrb);
                            _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Created time provider fork {0} at {1}, {2} line {3}", counter, callerMemberName, callerFilePath, callerLineNumber);
#endif

                            _forkNameCounter.Increment(debugName);
                            return counter;
                        }
                    }
                }

                // Find out which fork has the most counts
                float mostForks = 0;
                string nameWithMostForks = "Unknown";
                foreach (var count in _forkNameCounter)
                {
                    if (count.Value > mostForks)
                    {
                        mostForks = count.Value;
                        nameWithMostForks = count.Key;
                    }
                }

                throw new SemaphoreFullException("There are more than " + MAX_FORKS + " active forked time processes. " +
                    "This is usually an indication that you forgot to Merge() somewhere. " +
                    "The culprit seems to be " + nameWithMostForks + " with " + mostForks + " instances.");
            }

            public void MergeFork(int targetFork, string callerFilePath, string callerMemberName, int callerLineNumber)
            {
                lock (_aliveForks)
                {
#if DEBUG
                    _logger.Log("Merging time fork  " + targetFork + " \"" + _aliveForks[targetFork].ForkName + "\" at base time " + 
                        ((double)_aliveForks[targetFork].Timestamp / (double)TimeSpan.TicksPerSecond).ToString("F3") + " and target time " +
                                ((double)_targetTime / (double)TimeSpan.TicksPerSecond).ToString("F3"), LogLevel.Vrb);
                    _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Merged time provider fork {0} at {1}, {2} line {3}", targetFork, callerMemberName, callerFilePath, callerLineNumber);
#endif
                    _forkNameCounter.Increment(_aliveForks[targetFork].ForkName, -1);
                    _aliveForks.Remove(targetFork);
                }
            }

            /// <summary>
            /// Called by the orchestrator to advance time by a specific amount
            /// </summary>
            /// <param name="time"></param>
            /// <param name="originatingFork"></param>
            /// <param name="cancelToken">A cancel token</param>
            public void Step(TimeSpan time, int originatingFork, CancellationToken cancelToken)
            {
                // This should unblock everything that is currenty spinning in the Wait() method
                _targetTime += time.Ticks;
                _logger.LogFormat(LogLevel.Vrb, DataPrivacyClassification.SystemMetadata,
                    "Advancing virtual time by {0}ms, target time is now {1:F3}",
                    time.TotalMilliseconds,
                    (double)_targetTime / (double)TimeSpan.TicksPerSecond);

                lock (_aliveForks)
                {
                    // Manually set the time for the originating fork, since it cannot possibly be waiting
                    // and stepping at the same time
                    _aliveForks[originatingFork].Timestamp = _targetTime;
                }

                ValueStopwatch deadlockTimer = ValueStopwatch.StartNew();
                // Now we need to let asynchronous processes resolve.
                bool resolved = false;
                while (!resolved)
                {
                    resolved = true;

                    // Monitor all living forks until their current time is all equal to the target time, or they are merged and thus drop out of the aliveForks set
                    lock (_aliveForks)
                    {
                        foreach (var fork in _aliveForks)
                        {
                            if (fork.Key != originatingFork && fork.Value.Timestamp != _targetTime)
                            {
                                resolved = false;
                                if (deadlockTimer.Elapsed > DeadlockTimeout)
                                {
                                    throw new AbandonedMutexException("Lockstep time provider is deadlocked on fork #" + fork.Key + " \"" + fork.Value.ForkName + "\"");
                                }

                                break;
                            }
                        }
                    }

                    cancelToken.ThrowIfCancellationRequested();
                    WAIT_PROVIDER.Wait(10, cancelToken);
                }
            }
        }
    }
}
