
namespace Durandal.Common.Events
{
    using Durandal.Common.Cache;
    using Durandal.Common.Collections;
    using Durandal.Common.Logger;
    using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
    using Durandal.Common.Tasks;
    using Durandal.Common.Time;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// This class behaves like a built-in EventHandler, however it fires events with a signature of
    /// async Task instead of async void, which is better for transactionality and error handling.
    /// </summary>
    /// <typeparam name="TArgs">The type of arguments expected by the event handler</typeparam>
    public class AsyncEvent<TArgs> where TArgs : EventArgs
    {
        /// <summary>
        /// Pool for lists of Task, used to avoid reallocations.
        /// </summary>
        private static readonly LockFreeCache<List<Task>> POOLED_TASKLIST_QUEUE = new LockFreeCache<List<Task>>(32);

        /// <summary>
        /// Delegate representing a subscriber to an async event.
        /// </summary>
        /// <typeparam name="T">The runtime type of the event arguments</typeparam>
        /// <param name="sender">The sender of the event</param>
        /// <param name="args">The event arguments</param>
        /// <param name="realTime"></param>
        /// <returns>An async task representing execution</returns>
        public delegate Task AsyncEventHandler<T>(object sender, T args, IRealTimeProvider realTime);

        /// <summary>
        /// The set of delegates that we will invoke when this event is fired.
        /// </summary>
        private readonly FastConcurrentHashSet<AsyncEventHandler<TArgs>> _listeners =
            new FastConcurrentHashSet<AsyncEventHandler<TArgs>>();

        /// <summary>
        /// The set of events that subscribers of this event will also subscribe to.
        /// </summary>
        private readonly AsyncEvent<TArgs>[] _subordinateEvents;
        
        public bool HasSubscribers { get; private set; }

        /// <summary>
        /// Creates an async event.
        /// </summary>
        public AsyncEvent()
        {
            _subordinateEvents = new AsyncEvent<TArgs>[0];
        }

        /// <summary>
        /// Creates an async event which specifies one or more "subordinate" events.
        /// A subordinate event is one for which this event is acting as the external API.
        /// In C# it would be expressed as one event doing += on another one.
        /// Subscribers who subscribe to this event will also subscribe to all subordinate events.
        /// They will then be notified of events that are fired either from this event or any subordinate events.
        /// Unsubscribe works in the same way. It is recommended that listeners should not
        /// be given direct access to the subordinate event because then they might double-subscribe or
        /// otherwise mess things up.
        /// </summary>
        /// <param name="subordinates">A list of async events to be specified as subordinate to this one.</param>
        public AsyncEvent(params AsyncEvent<TArgs>[] subordinates)
        {
            _subordinateEvents = subordinates;
        }

        /// <summary>
        /// Fires this event to all subscribers with the given sender and arguments.
        /// IMPORTANT: This implementation of Fire() waits for all event subscribers to <b>fully resolve</b> before
        /// returning. In other words, if an event handler initiates some long async operation, await Fire() will not
        /// complete until that entire async operation is complete. This is a bit different from how async void events
        /// work in C# and can cause unexpected hangs if used improperly.
        /// </summary>
        /// <param name="sender">The sender of the event</param>
        /// <param name="args">The event arguments</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An awaitable task that will finish after all event subscribers have been fully notified (using async pseudo-parallelism)</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1030:Use events where appropriate", Justification = "This is an event")]
        public async Task Fire(object sender, TArgs args, IRealTimeProvider realTime)
        {
            if (!HasSubscribers)
            {
                return;
            }

            if (realTime.IsForDebug)
            {
                // Special branch for debug mode. Run all event handlers with a separate fork of realtime provider.
                List<Task> taskList = POOLED_TASKLIST_QUEUE.TryDequeue();
                if (taskList == null)
                {
                    taskList = new List<Task>(_listeners.Count);
                }

                try
                {
                    int c = 0;
                    foreach (var listener in _listeners)
                    {
                        EventFireClosure<TArgs> listenerClosure = new EventFireClosure<TArgs>(
                            listener,
                            sender,
                            args,
                            realTime.Fork("AsyncEvent-" + c.ToString()));

                        taskList.Add(listenerClosure.Run());
                        c++;
                    }

                    foreach (var task in taskList)
                    {
                        await task.ConfigureAwait(false);
                    }
                }
                finally
                {
                    taskList.Clear();
                    POOLED_TASKLIST_QUEUE.TryEnqueue(taskList);
                }
            }
            else
            {
                // Use a pool of task lists to avoid allocations
                List<Task> taskList = POOLED_TASKLIST_QUEUE.TryDequeue();
                if (taskList == null)
                {
                    taskList = new List<Task>(_listeners.Count);
                }

                // Note that C# native "async void" events work in this way:
                // Each subscriber is invoked serially but their async continuations
                // (the Task return values) are unobserved.
                // So the portion of the event handler before its await is performed serially,
                // and then once it awaits, the next event handler begins.
                // The code below approximates the same behavior, except it actually awaits the resulting tasks afterwards.
                try
                {
                    BuildTaskList(taskList, sender, args, realTime);

                    foreach (var task in taskList)
                    {
                        await task.ConfigureAwait(false);
                    }
                }
                finally
                {
                    taskList.Clear();
                    POOLED_TASKLIST_QUEUE.TryEnqueue(taskList);
                }
            }
        }

        // this has to be a separate method because we can't mix ref struct (value enumerator) with async
        private void BuildTaskList(List<Task> outTasks, object sender, TArgs args, IRealTimeProvider realTime)
        {
            var valueEnumerator = _listeners.GetValueEnumerator();

            while (valueEnumerator.MoveNext())
            {
                EventFireClosure<TArgs> listenerClosure = new EventFireClosure<TArgs>(
                    valueEnumerator.Current,
                    sender,
                    args,
                    realTime);

                outTasks.Add(listenerClosure.Run());
            }
        }

        /// <summary>
        /// Fires this event to all subscribers with the given sender and arguments, 
        /// with "unobserved" behavior that it similar to built-in "async void" events.
        /// This call will return as soon as the first event handler awaits something.
        /// Future continuations will then be run in the background on a processing queue.
        /// Exceptions that may arise in event handlers will be logged to the given event
        /// logger but will not halt execution.
        /// </summary>
        /// <param name="sender">The sender of the event</param>
        /// <param name="args">The event arguments</param>
        /// <param name="errorLogger"></param>
        /// <param name="realTime">A definition of real time</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1030:Use events where appropriate", Justification = "This is an event")]
        public void FireInBackground(object sender, TArgs args, ILogger errorLogger, IRealTimeProvider realTime)
        {
            if (!HasSubscribers)
            {
                return;
            }

            var valueEnumerator = _listeners.GetValueEnumerator();
            int c = 0;
            while (valueEnumerator.MoveNext())
            {
                EventFireClosure<TArgs> listenerClosure = new EventFireClosure<TArgs>(
                    valueEnumerator.Current,
                    sender,
                    args,
                    realTime.IsForDebug ? realTime.Fork("AsyncEvent-" + c.ToString()) : realTime);

                // So, normally I wanted to just be able to Forget() the task returned by Run().
                // The problem with that is that Run() doesn't actually return a task until the first yield,
                // which might not happen until all of the event handlers have processed.
                // That leads to problems, so I have to use Task.Run instead to ensure that the entirety
                // of the event handler runs on a separate thread.
                Task.Run(listenerClosure.Run).Forget(errorLogger);
                c++;
            }
        }
        
        /// <summary>
        /// Adds a subscriber to this async event.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public void Subscribe(AsyncEventHandler<TArgs> handler)
        {
            if (!_listeners.Add(handler))
            {
                throw new InvalidOperationException("Cannot subscribe to the same event multiple times");
            }

            HasSubscribers = _listeners.Count > 0;

            if (_subordinateEvents.Length > 0)
            {
                foreach (AsyncEvent<TArgs> subordinate in _subordinateEvents)
                {
                    subordinate.Subscribe(handler);
                }
            }
        }

        /// <summary>
        /// Removes a subscriber from this async event.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public void Unsubscribe(AsyncEventHandler<TArgs> handler)
        {
            if (!_listeners.Remove(handler))
            {
                throw new InvalidOperationException("Cannot unsubscribe from an event that you are not subscribed to");
            }

            HasSubscribers = _listeners.Count > 0;

            // Recursively unsubscribe the given event handler from any subordinates.
            if (_subordinateEvents.Length > 0)
            {
                foreach (AsyncEvent<TArgs> subordinate in _subordinateEvents)
                {
                    subordinate.Unsubscribe(handler);
                }
            }
        }

        /// <summary>
        /// Tries to remove a subscriber from this async event.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public void TryUnsubscribe(AsyncEventHandler<TArgs> handler)
        {
            if (!_listeners.Remove(handler))
            {
                return;
            }

            HasSubscribers = _listeners.Count > 0;

            // Recursively unsubscribe the given event handler from any subordinates.
            if (_subordinateEvents.Length > 0)
            {
                foreach (AsyncEvent<TArgs> subordinate in _subordinateEvents)
                {
                    subordinate.TryUnsubscribe(handler);
                }
            }
        }

        // <summary>
        // Removes all subscribers from this event
        // </summary>
        //public void UnsubscribeAll()
        //{
        //    // Since we assume this event is the only "external API" that subscribers
        //    // and callers will access, we unsubscribe every listener from all subordinates
        //    // as well.
        //    foreach (AsyncEvent<TArgs> subordinate in _subordinateEvents)
        //    {
        //        foreach (AsyncEventHandler<TArgs> listener in _listeners)
        //        {
        //            subordinate.Unsubscribe(listener);
        //        }
        //    }

        //    _listeners.Clear();

        //    HasSubscribers = false;
        //}

        /// <summary>
        /// struct used to hold inputs to a background event, used to reduce allocations from anonymous lambdas
        /// </summary>
        /// <typeparam name="E"></typeparam>
        private struct EventFireClosure<E>
        {
            public AsyncEventHandler<E> Listener;
            public object Sender;
            public E Args;
            public IRealTimeProvider ThreadLocalTime;

            public EventFireClosure(AsyncEventHandler<E> listener, object sender, E args, IRealTimeProvider threadLocalTime)
            {
                Listener = listener;
                Sender = sender;
                Args = args;
                ThreadLocalTime = threadLocalTime;
            }

            public async Task Run()
            {
                try
                {
                    await Listener(Sender, Args, ThreadLocalTime).ConfigureAwait(false);
                }
                finally
                {
                    ThreadLocalTime.Merge();
                }
            }
        }
    }
}
