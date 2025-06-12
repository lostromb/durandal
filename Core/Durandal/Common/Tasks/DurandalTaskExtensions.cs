using Durandal.Common.Collections;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.Utils;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Tasks
{
    public static class DurandalTaskExtensions
    {
        /// <summary>
        /// Same as Task.CompletedTask, for runtimes where that definition is not available.
        /// </summary>
        public static readonly Task NoOpTask = Task.FromResult(0);

        /// <summary>
        /// For when you need to start a "dedicated background thread" without having access to the Thread class.
        /// You should only use this as a replacement to Thread.Start since it has the potential to still cause issues
        /// with the global thread pool. Also, minor note, TaskFactory.StartNew doesn't raise exceptions to the returned Task
        /// in the same way that Task.Run does, so it might suppress errors that you might otherwise expect to se.
        /// </summary>
        public static readonly TaskFactory LongRunningTaskFactory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.LongRunning);

        /// <summary>
        /// A statis shared thread pool for "lossy" work items. That is,
        /// work items which are queued to this pool are not guaranteed to actually run,
        /// as the framework will decide to shed excess work if the system is overscheduled.
        /// </summary>
        public static readonly IThreadPool LossyThreadPool =
            new FixedCapacityThreadPool(
                new TaskThreadPool(),
                NullLogger.Singleton,
                NullMetricCollector.Singleton,
                DimensionSet.Empty,
                "LossyThreadPool",
                20, // 20 is kind of an arbitrary maximum of max queued work items, maybe this needs refinement?
                ThreadPoolOverschedulingBehavior.ShedExcessWorkItems);

        /// <summary>
        /// Synchronously waits for an asynchronous task to complete, and then returns its result.
        /// Effectively a Join() function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task"></param>
        /// <returns></returns>
        public static T Await<T>(this Task<T> task)
        {
            task.GetAwaiter().GetResult();
            task.ThrowIfExceptional();

            return task.Result;
        }

        /// <summary>
        /// Synchronously waits for an asynchronous task to complete, and then returns its result.
        /// Effectively a Join() function
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public static void Await(this Task task)
        {
            task.GetAwaiter().GetResult();
            task.ThrowIfExceptional();
        }

        public static void Await(this Task task, CancellationToken cancelToken)
        {
            task.Wait(cancelToken);
            task.ThrowIfExceptional();
        }

        /// <summary>
        /// Synchronously waits for an asynchronous task to complete, and then returns its result.
        /// Effectively a Join() function
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public static void Await(this ValueTask task)
        {
            task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Synchronously waits for an asynchronous task to complete, and then returns its result.
        /// Effectively a Join() function
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public static T Await<T>(this ValueTask<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Waits for an asynchronous task to finish within a specified timeout
        /// Returns a boolean value indicating whether the task finished.
        /// </summary>
        /// <param name="task"></param>
        /// <param name="msToWait"></param>
        /// <returns></returns>
        public static bool AwaitWithTimeout(this Task task, int msToWait)
        {
            task.Wait(msToWait);
            task.ThrowIfExceptional();

            if (task.IsFinished())
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tenative "await" that has a timeout. Returns a boolean value indicating whether the task finished
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task"></param>
        /// <param name="msToWait"></param>
        /// <param name="returnVal"></param>
        /// <returns></returns>
        public static bool AwaitWithTimeout<T>(this Task<T> task, int msToWait, out T returnVal)
        {
            task.Wait(msToWait);
            task.ThrowIfExceptional();

            if (task.IsCompleted)
            {
                returnVal = task.Result;
                return true;
            }

            returnVal = default(T);
            return false;
        }

        public static Task<T> FromCanceled<T>(CancellationToken token)
        {
            if (!token.IsCancellationRequested)
            {
                throw new ArgumentOutOfRangeException("Cancellation token is not canceled");
            }

            Task<T> cancelledTask = new Task<T>(() => default(T), token);
            return cancelledTask;
        }

        /// <summary>
        /// Returns true if the task is in any of the "end-state" statuses; that is, RanToCompletion, Faulted, or Canceled.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public static bool IsFinished(this Task task)
        {
            return task.Status == TaskStatus.RanToCompletion ||
                    task.Status == TaskStatus.Faulted ||
                    task.Status == TaskStatus.Canceled;
        }

        /// <summary>
        /// Returns true if the task is in any of the "end-state" statuses; that is, RanToCompletion, Faulted, or Canceled.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public static bool IsFinished<T>(this Task<T> task)
        {
            return task.Status == TaskStatus.RanToCompletion ||
                    task.Status == TaskStatus.Faulted ||
                    task.Status == TaskStatus.Canceled;
        }

        /// <summary>
        /// Returns true if the task is in any of the "end-state" statuses; that is, RanToCompletion, Faulted, or Canceled.
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public static bool IsFinished<T>(this ValueTask<T> task)
        {
            return task.IsCompleted ||
                    task.IsFaulted ||
                    task.IsCanceled;
        }

        /// <summary>
        /// Blocks the current thread for a specified amount of time
        /// </summary>
        /// <param name="time"></param>
        /// <param name="cancelToken"></param>
        public static void Block(TimeSpan time, CancellationToken cancelToken)
        {
            Task.Delay(time, cancelToken).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Blocks the current thread for a specified amount of milliseconds
        /// </summary>
        /// <param name="ms"></param>
        /// <param name="cancelToken"></param>
        public static void Block(int ms, CancellationToken cancelToken)
        {
            try
            {
                Task.Delay(ms, cancelToken).GetAwaiter().GetResult();
            }
            catch (AggregateException) { }
        }

        public static void ThrowIfExceptional(this Task task)
        {
            if (task.Exception != null)
            {
                // TODO I could alternatively use task.GetAwaiter().GetResult() to get the raw, unwrapped exception instead of the AggregateException
                // see https://stackoverflow.com/questions/17284517/is-task-result-the-same-as-getawaiter-getresult
                ExceptionDispatchInfo info = ExceptionDispatchInfo.Capture(task.Exception);
                info.Throw();
            }
        }

        /// <summary>
        /// Sometimes we want a task to run as "fire-and-forget", but we don't have a good place
        /// to handle exceptions that might get raised during async execution. An example of this
        /// is during "async void"-style events. Ideally, all tasks would be fully awaited and
        /// their results logged. However, this provides a compromise. Passing the task to ObserveTask()
        /// will let it keep running in the background, but if it fails it will signal the debugger (if
        /// in debug mode) and will log the error to the given error logger (hopefully a trace logger).
        /// </summary>
        /// <param name="t">The task to observe</param>
        /// <param name="errorLogger">A logger that will report any exceptions</param>
        public static void Forget(this Task t, ILogger errorLogger)
        {
            t.AssertNonNull("Task");
            errorLogger.AssertNonNull(nameof(errorLogger));
            t = t.ContinueWith((task) =>
            {
                if (task.Exception != null)
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        System.Diagnostics.Debugger.Break();
                    }

                    if (task.Exception is AggregateException)
                    {
                        AggregateException aggregate = task.Exception as AggregateException;
                        foreach (var subException in aggregate.InnerExceptions)
                        {
                            errorLogger.Log(subException);
                        }
                    }
                    else
                    {
                        errorLogger.Log(task.Exception);
                    }
                }
            });
        }

        /// <summary>
        /// Asynchronously waits for the task to complete, or for the cancellation token to be canceled.
        /// </summary>
        /// <param name="task">The task to wait for. May not be <c>null</c>.</param>
        /// <param name="cancellationToken">The cancellation token that cancels the wait.</param>
        public static Task WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            task.AssertNonNull(nameof(task));

            if (!cancellationToken.CanBeCanceled)
            {
                return task;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new Task(() => { }, cancellationToken); // is this the same as Task.Canceled? I could also capture the token in a closure....
            }

            return DoWaitAsync(task, cancellationToken);
        }

        private static async Task DoWaitAsync(Task task, CancellationToken cancellationToken)
        {
            using (var cancelTaskSource = new CancellationTokenTaskSource<object>(cancellationToken))
            {
                await (await Task.WhenAny(task, cancelTaskSource.Task).ConfigureAwait(false)).ConfigureAwait(false);
            }
        }

        ///// <summary>
        ///// Applies a timeout to any async task, and returns either the regular response or a flag indicating timeout
        ///// </summary>
        ///// <typeparam name="T"></typeparam>
        ///// <param name="task"></param>
        ///// <param name="msToWait"></param>
        ///// <returns></returns>
        //public static async Task<TimeoutResult<T>> Timeout<T>(this Task<T> task, int msToWait)
        //{
        //    Task delayTask = Task.Delay(msToWait);
        //    Task finishedTask = await Task.WhenAny(delayTask, task);
        //    if (task.Equals(finishedTask))
        //    {
        //        return new TimeoutResult<T>(await task);
        //    }
        //    else
        //    {
        //        return new TimeoutResult<T>();
        //    }
        //}

        ///// <summary>
        ///// Applies a timeout to any task, returning true if the task succeeded within the time period
        ///// </summary>
        ///// <param name="task">The task to wait for</param>
        ///// <param name="msToWait">The maximum time to wait</param>
        ///// <returns>True if the task succeeded</returns>
        //public static async Task<bool> Timeout(this Task task, int msToWait)
        //{
        //    Task delayTask = Task.Delay(msToWait);
        //    Task finishedTask = await Task.WhenAny(delayTask, task);
        //    if (task.Equals(finishedTask))
        //    {
        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}
    }
}
