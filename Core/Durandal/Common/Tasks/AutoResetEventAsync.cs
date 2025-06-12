using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

// Original idea by Stephen Toub: http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266923.aspx

namespace Durandal.Common.Tasks
{
    /// <summary>
    /// An async-compatible auto-reset event.
    /// </summary>
    [DebuggerDisplay("Id = {Id}, IsSet = {_set}")]
    public sealed class AutoResetEventAsync
    {
        /// <summary>
        /// The queue of TCSs that other tasks are awaiting.
        /// </summary>
        private readonly AsyncWaitQueue<object> _queue;

        /// <summary>
        /// The current state of the event.
        /// </summary>
        private bool _set;

        /// <summary>
        /// The semi-unique identifier for this instance. This is 0 if the id has not yet been created.
        /// </summary>
        private int _id;

        /// <summary>
        /// The object used for mutual exclusion.
        /// </summary>
        private readonly object _mutex;

        /// <summary>
        /// Creates an async-compatible auto-reset event.
        /// </summary>
        /// <param name="set">Whether the auto-reset event is initially set or unset.</param>
        public AutoResetEventAsync(bool set)
        {
            _queue = new AsyncWaitQueue<object>();
            _set = set;
            _mutex = new object();
        }
        
        /// <summary>
        /// Creates an async-compatible auto-reset event that is initially unset.
        /// </summary>
        public AutoResetEventAsync() : this(false)
        {
        }

        /// <summary>
        /// Gets a semi-unique identifier for this asynchronous auto-reset event.
        /// </summary>
        public int Id
        {
            get
            {
                return IdManager<AutoResetEventAsync>.GetId(ref _id);
            }
        }

        /// <summary>
        /// Whether this event is currently set. This member is seldom used; code using this member has a high possibility of race conditions.
        /// </summary>
        public bool IsSet
        {
            get { lock (_mutex) return _set; }
        }

        /// <summary>
        /// Manually attempts to check if this event is set, and if so, returns TRUE and clears the flag.
        /// This is intended to be used for "no-wait" scenarios where you just want to opportunistically check the flag without blocking.
        /// </summary>
        public bool TryGetAndClear()
        {
            lock (_mutex)
            {
                if (_set)
                {
                    _set = false;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Asynchronously waits for this event to be set. If the event is set, this method will auto-reset it and return immediately, even if the cancellation token is already signalled. If the wait is canceled, then it will not auto-reset this event.
        /// </summary>
        /// <param name="cancelToken">The cancellation token used to cancel this wait.</param>
        public Task WaitAsync(CancellationToken cancelToken)
        {
            Task ret;
            lock (_mutex)
            {
                if (_set)
                {
                    _set = false;
                    ret = DurandalTaskExtensions.NoOpTask;
                }
                else
                {
                    ret = _queue.Enqueue(_mutex, cancelToken);
                }
            }

            return ret;
        }

        /// <summary>
        /// Asynchronously waits for this event to be set. If the event is set, this method will auto-reset it and return immediately.
        /// </summary>
        public Task WaitAsync()
        {
            return WaitAsync(CancellationToken.None);
        }

        /// <summary>
        /// Synchronously waits for this event to be set. If the event is set, this method will auto-reset it and return immediately, even if the cancellation token is already signalled. If the wait is canceled, then it will not auto-reset this event. This method may block the calling thread.
        /// </summary>
        /// <param name="cancelToken">The cancellation token used to cancel this wait.</param>
        public void Wait(CancellationToken cancelToken)
        {
            WaitAsync(cancelToken).Await();
        }

        /// <summary>
        /// Synchronously waits for this event to be set. If the event is set, this method will auto-reset it and return immediately. This method may block the calling thread.
        /// </summary>
        public void Wait()
        {
            Wait(CancellationToken.None);
        }

        /// <summary>
        /// Sets the event, atomically completing a task returned by WaitAsync. If the event is already set, this method does nothing.
        /// </summary>
        public void Set()
        {
            lock (_mutex)
            {
                if (_queue.IsEmpty)
                    _set = true;
                else
                    _queue.Dequeue();
            }
        }
    }
}