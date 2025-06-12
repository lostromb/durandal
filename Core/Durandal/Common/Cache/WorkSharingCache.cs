using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Cache
{
    /// <summary>
    /// This class is intended to solve the problem of "multiple sources need some kind of value, which takes work to produce, and some of the requests are asking for the same value".
    /// What this does is detect duplicate requests and only perform the work to produce the value once (saving time and effort), and also saves
    /// past results into a cache so that prior results can be retrieved again. This behavior, of course, is based on the assumption that the producer function
    /// is pure and the same input will always produce the same output.
    /// </summary>
    /// <typeparam name="TInput">The "request" data type which are the parameters needed to produce some value</typeparam>
    /// <typeparam name="TOutput">The "response" data type which is </typeparam>
    public class WorkSharingCache<TInput, TOutput> : WorkSharingCacheBase<TInput, TOutput>
    {
        /// <summary>
        /// Delegate which defines the work that this cache performs to produce its values.
        /// </summary>
        /// <param name="input">The structured input to be acted upon</param>
        /// <param name="cancelToken">A cancellation token. Because of the design of the work sharing cache, this will always be <see cref="CancellationToken.None"/>.</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The result of the work.</returns>
        public delegate TOutput ValueProducer(TInput input, CancellationToken cancelToken, IRealTimeProvider realTime);

        private readonly ValueProducer _valueProducer;

        // keep a single function reference so we don't keep recreating a delegate object every time
        private readonly FastConcurrentDictionary<TInput, ProducerTask>.AugmentationDelegate<TInput, ProducerTask, CacheAugmentationClosure> _cacheUpdateDelegateSingleton;

        /// <summary>
        /// Creates a work-sharing cache with an async producer.
        /// </summary>
        /// <param name="valueProducer">A function that will produce the output</param>
        /// <param name="cacheLifetime">The amount of time a produced value will be considered "valid" for successive calls with the same inputs.
        /// This time is only "touched" on the first call, not on subsequent calls</param>
        /// <param name="cacheCapacity">The approximate maximum number of items to store in the cache.</param>
        public WorkSharingCache(ValueProducer valueProducer, TimeSpan cacheLifetime, int cacheCapacity)
            : base(cacheLifetime, cacheCapacity)
        {
            _valueProducer = valueProducer;
            _cacheUpdateDelegateSingleton = AugmentTaskDictionary;
        }

        /// <summary>
        /// Produces a value, either by reading a precalculated value from the cache, or kicking off work to produce a new value.
        /// </summary>
        /// <param name="input">Input to the work processor</param>
        /// <param name="realTime">A definition of "real time" for the purposes of timeouts and lifetime</param>
        /// <param name="cancelToken">A token to cancel the operation (note that this does NOT cancel the producer task itself, only the wait on that task to finish)</param>
        /// <param name="timeout">The maximum amount of time to wait for the value to be produced before throwing a <see cref="TimeoutException"/>.</param>
        /// <returns>The output from the producer function for this input.</returns>
        public TOutput ProduceValue(
            TInput input,
            IRealTimeProvider realTime,
            CancellationToken cancelToken = default(CancellationToken),
            TimeSpan? timeout = null)
        {
            base.CheckForStaleCacheEntries(realTime);

            CacheAugmentationClosure closure = new CacheAugmentationClosure(
                _valueProducer,
                input,
                realTime,
                _cacheLifetime);

            // Atomically do the main cache lookup
            ProducerTask producerTask = _productionTasks.Augment(input, _cacheUpdateDelegateSingleton, closure).AugmentedValue;

            // Is the value already produced?
            if (producerTask.IsCompleted())
            {
                return producerTask.Worker.Await();
            }

            // Wait for the task for finish. Keep in mind that we may not be the only one waiting on this result
            // If the task throws an exception then this method passes it along
            // If no timeout is specified then use a large value instead because we still want to allow cancellation token to be honored
            TimeSpan effectiveTimeout = timeout.GetValueOrDefault(MAX_WORKITEM_TIMEOUT);
            if (effectiveTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException("Timeout must be a positive value");
            }

            if (realTime.IsForDebug)
            {
                TimeSpan timeWaited = TimeSpan.Zero;
                TimeSpan waitIncrement = TimeSpan.FromMilliseconds(10);
                while (!producerTask.Worker.IsFinished() && timeWaited < effectiveTimeout)
                {
                    realTime.Wait(waitIncrement, cancelToken);
                    timeWaited += waitIncrement;
                }
            }
            else
            {
                // this will throw an AggregateException if the task fails
                producerTask.Worker.Wait((int)effectiveTimeout.TotalMilliseconds, cancelToken);
            }

            if (producerTask.Worker.IsFinished())
            {
                return producerTask.Worker.Await();
            }
            else
            {
                if (producerTask.Worker.Status == TaskStatus.WaitingToRun ||
                    producerTask.Worker.Status == TaskStatus.WaitingForActivation)
                {
                    throw new TimeoutException($"Cache work item failed to start after {effectiveTimeout.TotalMilliseconds} ms. Your processor threads are probably overscheduled");
                }
                else
                {
                    throw new TimeoutException($"Cache fetch timed out after {effectiveTimeout.TotalMilliseconds} ms.");
                }
            }
        }

        /// <summary>
        /// The method that is run on the cache dictionary itself while holding the lock to a dictionary slot.
        /// This lets us atomically cases where a value exists but it's stale and needs to be recreated.
        /// </summary>
        /// <param name="exists"></param>
        /// <param name="value"></param>
        /// <param name="closure"></param>
        private static void AugmentTaskDictionary(TInput key, ref bool exists, ref ProducerTask value, CacheAugmentationClosure closure)
        {
            // First check for cache eviction
            if (exists &&
                value.IsCompleted() &&
                value.ExpireTime < closure.RealTime.Time)
            {
                exists = false;
            }

            // Now see if we need to recreate the value
            if (!exists)
            {
                // Key not present means we need to queue up a work item to produce this value
                closure.ThreadLocalTime = closure.RealTime.Fork("WorkSharingCacheProducer");
                exists = true;
                value = new ProducerTask()
                {
                    ExpireTime = closure.RealTime.Time + closure.CacheLifetime,

                    // Make sure we don't actually run all of the work of the producer here while we're holding a lock
                    // to the cache entry itself. Use Task.Run to delegate it to the thread pool.
                    // Reuseing the closure struct we've already created saves the allocation of a separate lambda + closure
                    Worker = Task<TOutput>.Run(closure.RunProducerDirectly),
                };
            }
        }

        /// <summary>
        /// Struct used to avoid closure allocations involved when passing arguments to a newly created task.
        /// </summary>
        private struct CacheAugmentationClosure
        {
            public ValueProducer ValueProducer;
            public TInput Input;
            public IRealTimeProvider RealTime;
            public TimeSpan CacheLifetime;
            public IRealTimeProvider ThreadLocalTime;

            public CacheAugmentationClosure(
                ValueProducer valueProducer,
                TInput input,
                IRealTimeProvider realTime,
                TimeSpan cacheLifetime)
            {
                ValueProducer = valueProducer;
                Input = input;
                RealTime = realTime;
                CacheLifetime = cacheLifetime;
                ThreadLocalTime = null;
            }

            public TOutput RunProducerDirectly()
            {
                try
                {
                    // We have to pass CancellationToken.None here because multiple consumers may be waiting on the result
                    // of a single work result, and the it wouldn't be reasonable to allow a single consumer to cancel
                    // the work for everyone else
                    return ValueProducer(Input, CancellationToken.None, ThreadLocalTime);
                }
                finally
                {
                    ThreadLocalTime.Merge();
                }
            }
        }
    }
}
