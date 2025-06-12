using Durandal.Common.Instrumentation;
using Durandal.Common.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// Represents a thread pool to be used for SHORT-TERM processor-bound work items.
    /// If you want to run a task for a long time in the background, use <see cref="DurandalTaskExtensions.LongRunningTaskFactory">LongRunningTaskFactory</see> instead.
    /// </summary>
    public interface IThreadPool : IDisposable, IMetricSource
    {
        /// <summary>
        /// The total number of work items that are currently running on threads
        /// </summary>
        int RunningWorkItems { get; }

        /// <summary>
        /// The maximum concurrent work item capacity of this pool
        /// </summary>
        int ThreadCount { get; }

        /// <summary>
        /// The total number of running + queued work items
        /// </summary>
        int TotalWorkItems { get; }
        
        /// <summary>
        /// Enqueues a new work item to be delegated to the thread pool. The item will
        /// run on the first available thread, as soon as any are free
        /// </summary>
        /// <param name="workItem"></param>
        void EnqueueUserWorkItem(Action workItem);

        /// <summary>
        /// Enqueues a new async work item to be delegated to the thread pool. The item will
        /// run on the first available thread, as soon as any are free
        /// </summary>
        /// <param name="workItem"></param>
        void EnqueueUserAsyncWorkItem(Func<Task> workItem);

        /// <summary>
        /// Blocks until all currently running tasks have completed. This does not factor in tasks in the backlog
        /// that have yet to be started.
        /// </summary>
        /// <returns></returns>
        Task WaitForCurrentTasksToFinish(CancellationToken cancellizer, IRealTimeProvider realTime);

        /// <summary>
        /// Get thread pool status for debugging
        /// </summary>
        /// <returns></returns>
        string GetStatus();
    }
}