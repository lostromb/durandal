using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// Implements a somewhat convoluted way of enumerating a list of tasks and then allowing
    /// the caller to respond any time one task in the set completes. This is superceded
    /// by Task.WhenEach in .Net 9, but not all runtimes are going to support that.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TaskWhenAnyCollection<T>
    {
        // Design based on https://devblogs.microsoft.com/pfxteam/processing-tasks-as-they-complete/
        private readonly TaskCompletionSource<Task<T>>[] _buckets;
        private readonly Task<Task<T>>[] _results;
        private int _nextTaskIndex = -1;
        private int _nextTaskToAwait = 0;

        /// <summary>
        /// Constructs a new <see cref="TaskWhenAnyCollection{T}"/> with a set of input tasks.
        /// </summary>
        /// <param name="task1"></param>
        /// <param name="task2"></param>
        public TaskWhenAnyCollection(Task<T> task1, Task<T> task2)
        {
            _buckets = new TaskCompletionSource<Task<T>>[2];
            _results = new Task<Task<T>>[2];
            for (int i = 0; i < _buckets.Length; i++)
            {
                _buckets[i] = new TaskCompletionSource<Task<T>>();
                _results[i] = _buckets[i].Task;
            }

            task1.ContinueWith(SetNextTaskResultInOrder, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            task2.ContinueWith(SetNextTaskResultInOrder, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        /// <summary>
        /// Constructs a new <see cref="TaskWhenAnyCollection{T}"/> with a set of input tasks.
        /// </summary>
        /// <param name="task1"></param>
        /// <param name="task2"></param>
        /// <param name="task3"></param>
        public TaskWhenAnyCollection(Task<T> task1, Task<T> task2, Task<T> task3)
        {
            _buckets = new TaskCompletionSource<Task<T>>[3];
            _results = new Task<Task<T>>[3];
            for (int i = 0; i < _buckets.Length; i++)
            {
                _buckets[i] = new TaskCompletionSource<Task<T>>();
                _results[i] = _buckets[i].Task;
            }

            task1.ContinueWith(SetNextTaskResultInOrder, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            task2.ContinueWith(SetNextTaskResultInOrder, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            task3.ContinueWith(SetNextTaskResultInOrder, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        /// <summary>
        /// Constructs a new <see cref="TaskWhenAnyCollection{T}"/> with a set of input tasks.
        /// </summary>
        /// <param name="tasks"></param>
        public TaskWhenAnyCollection(params Task<T>[] tasks)
        {
            _buckets = new TaskCompletionSource<Task<T>>[tasks.Length];
            _results = new Task<Task<T>>[tasks.Length];
            for (int i = 0; i < _buckets.Length; i++)
            {
                _buckets[i] = new TaskCompletionSource<Task<T>>();
                _results[i] = _buckets[i].Task;
            }

            foreach (var inputTask in tasks)
            {
                inputTask.ContinueWith(SetNextTaskResultInOrder, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Constructs a new <see cref="TaskWhenAnyCollection{T}"/> with a set of input tasks.
        /// </summary>
        /// <param name="tasks"></param>
        public TaskWhenAnyCollection(IReadOnlyCollection<Task<T>> tasks)
        {
            _buckets = new TaskCompletionSource<Task<T>>[tasks.Count];
            _results = new Task<Task<T>>[tasks.Count];
            for (int i = 0; i < _buckets.Length; i++)
            {
                _buckets[i] = new TaskCompletionSource<Task<T>>();
                _results[i] = _buckets[i].Task;
            }

            foreach (var inputTask in tasks)
            {
                inputTask.ContinueWith(SetNextTaskResultInOrder, this, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }

        /// <summary>
        /// Gets the number of tasks in this collection.
        /// </summary>
        public int Count => _results.Length;

        /// <summary>
        /// Awaits the next task that will complete (or has already completed) in the set, and returns its Task object.
        /// You can in turn await that object to get its result, or just use .Result since it's guaranteed to be completed.
        /// </summary>
        /// <returns>A completed task.</returns>
        public async Task<Task<T>> WaitForNextFinishedTask()
        {
            if (_nextTaskToAwait >= _results.Length)
            {
                throw new IndexOutOfRangeException("Attempted to enumerate past the number of tasks in the collection");
            }

            return await _results[_nextTaskToAwait++];
        }

        private static void SetNextTaskResultInOrder(Task<T> completedTask, object state)
        {
            TaskWhenAnyCollection<T> parent = state as TaskWhenAnyCollection<T>;
            parent._buckets[Interlocked.Increment(ref parent._nextTaskIndex)].TrySetResult(completedTask);
        }
    }
}
