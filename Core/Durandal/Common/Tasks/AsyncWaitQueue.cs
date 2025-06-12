using Durandal.Common.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// The default wait queue implementation, which uses a double-ended queue.
    /// </summary>
    /// <typeparam name="T">The type of the results. If this isn't needed, use <see cref="Object"/>.</typeparam>
    public sealed class AsyncWaitQueue<T>
    {
        private readonly Deque<TaskCompletionSource<T>> _queue = new Deque<TaskCompletionSource<T>>();

        private int Count
        {
            get { return _queue.Count; }
        }

        public bool IsEmpty
        {
            get { return Count == 0; }
        }

        public Task<T> Enqueue()
        {
            var tcs = TaskCompletionSourceExtensions.CreateAsyncTaskSource<T>();
            _queue.AddToBack(tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Creates a new entry and queues it to this wait queue. If the cancellation token is already canceled, this method immediately returns a canceled task without modifying the wait queue.
        /// </summary>
        /// <param name="mutex">A synchronization object taken while cancelling the entry.</param>
        /// <param name="token">The token used to cancel the wait.</param>
        /// <returns>The queued task.</returns>
        public Task<T> Enqueue(object mutex, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return DurandalTaskExtensions.FromCanceled<T>(token);
            }

            var ret = this.Enqueue();
            if (!token.CanBeCanceled)
            {
                return ret;
            }

            var registration = token.Register(() =>
                {
                    lock (mutex)
                    {
                        this.TryCancel(ret, token);
                    }
                },
                useSynchronizationContext: false);

            ret.ContinueWith(
                _ =>
                {
                    registration.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return ret;
        }

        public void Dequeue(T result = default(T))
        {
            _queue.RemoveFromFront().TrySetResult(result);
        }

        public void DequeueAll(T result = default(T))
        {
            foreach (var source in _queue)
            {
                source.TrySetResult(result);
            }

            _queue.Clear();
        }

        public bool TryCancel(Task task, CancellationToken cancelToken)
        {
            for (int i = 0; i != _queue.Count; ++i)
            {
                if (_queue[i].Task == task)
                {
                    _queue[i].TrySetCanceled();
                    _queue.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public void CancelAll(CancellationToken cancelToken)
        {
            foreach (var source in _queue)
            {
                source.TrySetCanceled();
            }

            _queue.Clear();
        }
    }
}
