using Durandal.Common.Instrumentation;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Utils
{
    /// <summary>
    /// The purpose of this class is to implement a pattern of committing a data change asynchronously
    /// without blocking UI. For example, touching a configuration file might trigger multiple "write" events
    /// to put the changes to a backing file. We only want a single write task to happen, and we want to make
    /// sure it always pick up the latest changes. This class provides a solution that is simultaneously
    /// asynchronous and single-threaded.
    /// </summary>
    public class Committer : IDisposable
    {
        private readonly CommitmentDelegate _commitmentAction;
        private readonly IRealTimeProvider _realTime;
        private readonly TaskFactory _taskFactory;
        private readonly TimeSpan? _commitmentDelay;
        private readonly TimeSpan? _maxCommitmentDelay;
        private readonly object _mutex = new object();
        private bool _commitRunning = false;
        private int _commitQueued = 0;
        private int _commitDelayRequired = 0;
        private int _disposed = 0;

        public delegate Task CommitmentDelegate(IRealTimeProvider realTime);

        /// <summary>
        /// Instantiates a new committer object that executes the specified delegate on the specified thread pool
        /// </summary>
        /// <param name="commitmentAction">The action to run</param>
        /// <param name="realTime">A definition of "real time" for purposes of measuring timeouts and intervals</param>
        /// <param name="commitmentDelay">An optional delay to apply before actually committing changes.
        /// The purpose of this parameter is if, for example, you have something that fires a lot of "change" events in a short amount of time and you only care about the last one.
        /// Any duplicate change event that happens within the initial commitment delay will coalesce into the previous event and reset the delay.</param>
        /// <param name="maxCommitmentDelay">If commitmentDelay is specified, this is the maximum amount of time a commit can be delayed before it is forced to go through.
        /// This is intended for systems that may expect a long period of constant changes events, and you want to strike a balance between firing each one constantly
        /// vs. deferring all of the events entirely until activity slows down</param>
        public Committer(CommitmentDelegate commitmentAction, IRealTimeProvider realTime = null, TimeSpan? commitmentDelay = null, TimeSpan? maxCommitmentDelay = null)
        {
            _realTime = realTime ?? DefaultRealTimeProvider.Singleton;
            _commitmentAction = commitmentAction;
            _commitmentDelay = commitmentDelay;
            _maxCommitmentDelay = maxCommitmentDelay;
            _taskFactory = new TaskFactory();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~Committer()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Schedules a new background commitment and returns immediately.
        /// </summary>
        public void Commit()
        {
            lock (_mutex)
            {
                _commitQueued = 1;
                _commitDelayRequired = 1;

                // Is there a commitment in progress?
                if (_commitRunning == false)
                {
                    // No commit is currently in progress. Start a new task
                    _commitRunning = true;
                    IRealTimeProvider forkedTime = _realTime.Fork("CommitterWorkThread");
                    _taskFactory.StartNew(() => RunCommitTask(forkedTime));
                }

                // If a task already exists, we set the queued flag so nothing else we need to do
            }
        }

        private async Task RunCommitTask(IRealTimeProvider threadLocalTime)
        {
            try
            {
                bool hasNextCommit = true;

                // Keep this same task going for as long as commitments are queued
                while (hasNextCommit)
                {
                    if (_commitmentDelay.HasValue)
                    {
                        double msWaited = 0;
                        // This will continue to loop for as long as external processes trigger the Commit() method, or
                        // until maxCommitmentDelay is reached (if specified)
                        while (Interlocked.CompareExchange(ref _commitDelayRequired, 0, 1) != 0)
                        {
                            double timeToWait = _commitmentDelay.Value.TotalMilliseconds;
                            if (_maxCommitmentDelay.HasValue)
                            {
                                timeToWait = Math.Max(0, Math.Min(timeToWait, _maxCommitmentDelay.Value.TotalMilliseconds - msWaited));
                            }

                            if (timeToWait > 0)
                            {
                                await threadLocalTime.WaitAsync(_commitmentDelay.Value, CancellationToken.None).ConfigureAwait(false);
                                msWaited += timeToWait;
                            }
                        }
                    }

                    lock (_mutex)
                    {
                        hasNextCommit = Interlocked.CompareExchange(ref _commitQueued, 0, 1) != 0;
                    }

                    await _commitmentAction(threadLocalTime).ConfigureAwait(false);

                    lock (_mutex)
                    {
                        hasNextCommit = Interlocked.CompareExchange(ref _commitQueued, 0, 1) != 0;

                        // If there are no more commits to process, set this task to be null. It will die and potentially be replaced by a new task
                        if (!hasNextCommit)
                        {
                            _commitRunning = false;
                        }
                    }
                }
            }
            finally
            {
                threadLocalTime.Merge();
            }
        }

        /// <summary>
        /// If there is a current commitment in the background, block until it is finished.
        /// </summary>
        public void WaitUntilCommitFinished(CancellationToken cancelToken, IRealTimeProvider realTime, int maxTimeoutMs = 10000)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            int timeWaited = 0;
            while (timeWaited < maxTimeoutMs)
            {
                lock (_mutex)
                {
                    if (_commitRunning == false)
                    {
                        return;
                    }
                }

                realTime.Wait(TimeSpan.FromMilliseconds(5), cancelToken);
                timeWaited += 5;
            }
        }

        /// <summary>
        /// If there is a current commitment in the background, block until it is finished.
        /// </summary>
        public async Task WaitUntilCommitFinishedAsync(CancellationToken cancelToken, IRealTimeProvider realTime, int maxTimeoutMs = 10000)
        {
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            int timeWaited = 0;
            while (timeWaited < maxTimeoutMs)
            {
                lock (_mutex)
                {
                    if (_commitRunning == false)
                    {
                        return;
                    }
                }

                await realTime.WaitAsync(TimeSpan.FromMilliseconds(5), cancelToken).ConfigureAwait(false);
                timeWaited += 5;
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
                WaitUntilCommitFinished(CancellationToken.None, null, 5000);
            }
        }
    }
}
