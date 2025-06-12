using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Time
{
    /// <summary>
    /// This interface is a bit esoteric so I will explain:
    /// In unit tests, we want to be able to test real-time processes that use multiple asynchronous threads. For example,
    /// a background thread may be waiting to run a specific task at certain intervals, or a thread may be waiting on a timeout from another.
    /// We want a way to test this deterministically, and ideally at the maximum possible speed (rather than running at 1:1 real time). This class solves the problem
    /// by providing two methods, Wait() and GetTimestamp(), which in the default implementation just uses the wallclock time, but
    /// for unit testing uses an augmented virtual time which the test case has complete control over.
    /// This class also abstracts the functionality of things like DateTimeOffset.Now which allows determinism of things that depend on the current wall clock.
    /// For this to work properly, each logical thread needs to have its own Fork() of the time provider, which must be Merge()d before the thread finishes.
    /// This does not apply to normal async/await invocation _except_ in the case where multiple async tasks are awaited in parallel (e.g. await Task.WhenAll()...)
    /// </summary>
    public interface IRealTimeProvider
    {
        /// <summary>
        /// Waits for the specified span of time according to this time provider's implementation (in the default implementation
        /// this is just equivalent to Thread.Sleep() as you would expect)
        /// </summary>
        /// <param name="time">The amount of time to wait</param>
        /// <param name="cancelToken">A token to cancel the waiting</param>
        void Wait(TimeSpan time, CancellationToken cancelToken);

        /// <summary>
        /// Waits asynchronously for the specified amount of time according to this time provider's implementation.
        /// In the default implementation this is equivalent to Task.Delay()
        /// </summary>
        /// <param name="time">The amount of time to wait</param>
        /// <param name="cancelToken">A token to cancel the waiting</param>
        /// <returns>An async wait task.</returns>
        Task WaitAsync(TimeSpan time, CancellationToken cancelToken);

        /// <summary>
        /// Gets the timestamp of this current time provider measured in milliseconds. The origin point of the timestamp should be considered arbitrary
        /// </summary>
        /// <returns>The current timestamp according to this provider, measured in milliseconds</returns>
        long TimestampMilliseconds { get; }

        /// <summary>
        /// Gets the timestamp of this current time provider measured in ticks. The origin point of the timestamp should be considered arbitrary
        /// </summary>
        /// <returns>The current timestamp according to this provider, measured in ticks</returns>
        long TimestampTicks { get; }

        /// <summary>
        /// Gets the UTC datetime value of this current time provider
        /// </summary>
        /// <returns>The current UTC datetime according to this provider</returns>
        DateTimeOffset Time { get; }

        /// <summary>
        /// If you hand a reference of this time provider to another thread, you need to call Fork() to create a new instance
        /// beforehand. This is necessary to explicitly coordinate those two threads and keep them in lock-step (at least, during
        /// unit tests; the default implementation is a NoOp). All instances that are Forked() must call Merge() before their associated thread finishes.
        /// </summary>
        /// <param name="nameForDebug">A name of this fork, used for debugging.</param>
        /// <param name="callerFilePath">Ignore this, the compiler will fill it in</param>
        /// <param name="callerMemberName">Ignore this, the compiler will fill it in</param>
        /// <param name="callerLineNumber">Ignore this, the compiler will fill it in</param>
        /// <returns>A fork of this time provider.</returns>
        IRealTimeProvider Fork(
            string nameForDebug,
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0);

        /// <summary>
        /// After a time provider has been Fork()ed and associated with a thread, you need to call Merge() when that thread finishes, otherwise
        /// deadlock will occur when the time provider tries to sync up to the now non-existent thread
        /// </summary>
        /// <param name="callerFilePath">Ignore this, the compiler will fill it in</param>
        /// <param name="callerMemberName">Ignore this, the compiler will fill it in</param>
        /// <param name="callerLineNumber">Ignore this, the compiler will fill it in</param>
        void Merge(
            [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMemberName = null,
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLineNumber = 0);

        /// <summary>
        /// Indicates that this time provider is of a type that is used for debugging scenarios.
        /// This can be checked to determine whether to apply certain wait-loop optimizations at runtime that cause problems in lock-step unit tests.
        /// </summary>
        bool IsForDebug { get; }
    }
}
