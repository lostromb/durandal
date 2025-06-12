using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.Instrumentation;
using Durandal.Common.Utils;
using Durandal.Common.Time;
using System.Diagnostics;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Tasks
{
    public class ResourcePool<T> : IDisposable, IMetricSource where T : class
    {
        private readonly int _poolSize;
        private readonly string _poolName;
        private readonly SemaphoreSlim _available;
        private readonly object _occupiedMutex = new object();
        private readonly bool[] _occupied;
        private readonly T[] _resources;
        private readonly ILogger _logger;
        private readonly Func<T, Task<T>> _watchdogFunctor = null;
        private readonly Task _watchdog = null;
        private readonly DimensionSet _dimensions;

        private volatile bool _watchdogRunning = false;
        private ManualResetEventSlim _watchdogFinished = new ManualResetEventSlim(true);
        private int _resourcesLeased = 0;
        private int _lastLeasedResourceId = 0;
        private int _disposed = 0;

        /// <summary>
        /// Creates a pool of resources from a given list of available resources. This constructor assumes that all the
        /// objects are properly instantiated already. Lifetime of the objects is then managed by the pool.
        /// </summary>
        /// <param name="resources">The set of resources to use for the pool</param>
        /// <param name="logger"></param>
        /// <param name="dimensions">Dimensions for use when reporting metrics</param>
        /// <param name="poolName">The pool name, for metrics and debugging</param>
        public ResourcePool(IEnumerable<T> resources, ILogger logger, DimensionSet dimensions, string poolName)
        {
            _resources = new List<T>(resources).ToArray();
            _poolName = poolName;
            _poolSize = _resources.Length;
            _dimensions = dimensions ?? DimensionSet.Empty;
            _dimensions = _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_ResourcePoolName, _poolName));
            _available = new SemaphoreSlim(_poolSize, _poolSize);
            _occupied = new bool[_poolSize];
            _lastLeasedResourceId = 0;
            _logger = logger;
            _logger.Log("Initializing a ResourcePool of " + typeof(T) + " with pool size of " + _poolSize);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        /// <summary>
        /// Creates a pool of resources from a given list of available resources. This constructor assumes that all the
        /// objects are properly instantiated already. This constructor also specifies a watchdog function that is executed
        /// in the background against all the items in the pool
        /// </summary>
        /// <param name="resources">The set of resources to use for the pool</param>
        /// <param name="logger">A logger</param>
        /// <param name="dimensions">Dimensions for use when reporting metrics</param>
        /// <param name="poolName">The pool name, for metrics and debugging</param>
        /// <param name="watchdogFunctor">A watchdog function used to update or replace failing resources</param>
        /// <param name="watchdogCycleLength">The amount of time, in ms, that the watchdog should plan to use to update
        /// every single resource in the pool, or in other words, to do one "full pass"</param>
        /// <param name="realTime">A definition of real time to use for the watchdog</param>
        public ResourcePool(
            IEnumerable<T> resources,
            ILogger logger,
            DimensionSet dimensions,
            string poolName,
            Func<T, Task<T>> watchdogFunctor,
            TimeSpan watchdogCycleLength,
            IRealTimeProvider realTime)
        {
            _resources = new List<T>(resources).ToArray();
            _poolName = poolName;
            _dimensions = dimensions ?? DimensionSet.Empty;
            _dimensions = _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_ResourcePoolName, _poolName));
            _poolSize = _resources.Length;
            _available = new SemaphoreSlim(_poolSize, _poolSize);
            _occupied = new bool[_poolSize];
            _lastLeasedResourceId = 0;
            _logger = logger;
            _watchdogFunctor = watchdogFunctor;
            _logger.Log("Initializing a ResourcePool of " + typeof(T) + " with pool size of " + _poolSize + " and a watchdog every " + watchdogCycleLength.TotalMilliseconds + " ms");

            if (_poolSize > 0)
            {
                _watchdogRunning = true;
                TimeSpan watchdogInterval = TimeSpan.FromMilliseconds(System.Math.Max(10, watchdogCycleLength.TotalMilliseconds / _poolSize));
                IRealTimeProvider watchdogTime = realTime.Fork("ResourcePoolWatchdog-" + poolName);
                _watchdog = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(async () =>
                    {
                        try
                        {
                            await WatchdogLoop(watchdogFunctor, watchdogInterval, watchdogTime).ConfigureAwait(false);
                        }
                        finally
                        {
                            watchdogTime.Merge();
                        }
                    });
            }

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        /// <summary>
        /// Creates a pool of resources of the given size. Each resource is initialized using the parameters passed as ConstructorParams.
        /// </summary>
        /// <param name="poolSize">The number of resources to allocate</param>
        /// <param name="logger">A logger</param>
        /// <param name="dimensions">Dimensions for use when reporting metrics</param>
        /// <param name="poolName">The pool name, for metrics and debugging</param>
        /// <param name="constructorParams">The set of fixed constructor parameters to use when creating pool objects</param>
        public ResourcePool(int poolSize, ILogger logger, DimensionSet dimensions, string poolName, params object[] constructorParams)
        {
            _resources = new T[poolSize];
            _poolName = poolName;
            _dimensions = dimensions ?? DimensionSet.Empty;
            _dimensions = _dimensions.Combine(new MetricDimension(CommonInstrumentation.Key_Dimension_ResourcePoolName, _poolName));

            // Initialize each member of the pool
            for (int c = 0; c < poolSize; c++)
            {
                _resources[c] = (T)Activator.CreateInstance(typeof(T), constructorParams);
            }

            _poolSize = _resources.Length;
            _available = new SemaphoreSlim(_poolSize, _poolSize);
            _occupied = new bool[_poolSize];
            _lastLeasedResourceId = 0;
            _logger = logger;
            _logger.Log("Initializing a ResourcePool of " + typeof(T) + " with pool size of " + _poolSize);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~ResourcePool()
        {
            Dispose(false);
        }
#endif

        public bool TryGetResource(out PooledResource<T> returnVal)
        {
            return TryGetResource(out returnVal, TimeSpan.Zero);
        }

        public bool TryGetResource(out PooledResource<T> returnVal, TimeSpan timeout)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(ResourcePool<T>));
            }

            if (timeout < TimeSpan.Zero)
            {
                throw new ArgumentException("Timeout cannot be less than zero");
            }

            bool success = false;
            returnVal = null;

            // Capped timeout (or tentative fetch if timeout is zero)
            if (_available.Wait(timeout))
            {
                success = GetResource(out returnVal);
            }

            return success;
        }

        /// <summary>
        /// Tentatively attempts to fetch a resource from the pool, returning immediately
        /// whether or not one is available.
        /// </summary>
        /// <returns>A retrieve result potentially containing a fetched resource</returns>
        public Task<RetrieveResult<PooledResource<T>>> TryGetResourceAsync()
        {
            return TryGetResourceAsync(TimeSpan.Zero);
        }

        /// <summary>
        /// Attempts to fetch a resource from the pool within the specified timeout.
        /// </summary>
        /// <param name="timeout">A timeout</param>
        /// <returns>A retrieve result potentially containing a fetched resource</returns>
        public async Task<RetrieveResult<PooledResource<T>>> TryGetResourceAsync(TimeSpan timeout)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(ResourcePool<T>));
            }

            if (timeout < TimeSpan.Zero)
            {
                throw new ArgumentException("Timeout cannot be less than zero");
            }

            bool success = false;
            PooledResource<T> returnVal;
            ValueStopwatch timer = ValueStopwatch.StartNew();

            // Capped  (or tentative fetch if timeout is zero)
            if (await _available.WaitAsync(timeout).ConfigureAwait(false))
            {
                success = GetResource(out returnVal);
            }
            else
            {
                returnVal = null;
            }

            timer.Stop();
            return new RetrieveResult<PooledResource<T>>(returnVal, timer.ElapsedMillisecondsPrecise(), success);
        }

        // after the semaphore grants us access to the critical area, this method actually retrieves an item from the slot
        private bool GetResource(out PooledResource<T> returnVal)
        {
            lock(_occupiedMutex)
            {
                // Find a free resource. If the semaphore let us into this critical area, there is guaranteed to be an available slot.
                int cur = _lastLeasedResourceId;
                for (int c = 0; c < _poolSize; c++)
                {
                    if (++cur >= _poolSize)
                    {
                        cur = 0;
                    }
                    if (!_occupied[cur])
                    {
                        _occupied[cur] = true;
                        _lastLeasedResourceId = cur;
                        returnVal = new PooledResource<T>(_resources[cur], cur);
                        _resourcesLeased++;
                        return true;
                    }
                }
            }

            // If we hit this path it's an error
            _logger.Log("Resource pool is leaking semaphores. Did you forget to release a pooled resource?", LogLevel.Err);
            returnVal = null;
            return false;
        }

        public bool ReleaseResource(PooledResource<T> resource)
        {
            if (resource == null || resource.Value == null)
            {
                throw new ArgumentNullException("resource", "Cannot release a null resource!");
            }

            if (_disposed != 0)
            {
                return false;
            }

            bool successfulRelease = false;

            lock (_occupiedMutex)
            {
                // Find the resources and mark it as unused
                if (_occupied[resource._idx])
                {
                    _occupied[resource._idx] = false;
                    successfulRelease = true;
                    _resourcesLeased--;
                }
            }

            if (successfulRelease)
            {
                _available.Release();
            }

            return successfulRelease;
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

            _watchdogRunning = false;
            // Give the watchdog a second to finish if necessary
            _watchdogFinished.Wait(TimeSpan.FromMilliseconds(1000));

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                // Acquire all resource locks
                for (int c = 0; c < _poolSize; c++)
                {
                    _available.WaitAsync(1000);
                }

                // Now every resource should be free and we can dispose them.
                _available?.Dispose();

                foreach (T item in _resources)
                {
                    if (item is IDisposable)
                    {
                        ((IDisposable)item).Dispose();
                    }
                }

                _watchdogFinished?.Dispose();
            }
        }

        /// <summary>
        /// This is the function that is executed by the (optional) background watchdog task.
        /// The purpose of this task is to monitor the health of each resource and update / replace
        /// them in the pool if needed. For example, the pool might be a collection of open sockets.
        /// The watchdog would need to detect timed out or failing sockets and reinitialize them.
        /// </summary>
        /// <param name="watchdogFunctor">The function that the watchdog applies to each pooled resource while holding its lock</param>
        /// <param name="watchdogInterval">The interval between each watchdog running</param>
        /// <param name="realTime">A definition of real time (usually forked for a background thread)</param>
        private async Task WatchdogLoop(Func<T, Task<T>> watchdogFunctor, TimeSpan watchdogInterval, IRealTimeProvider realTime)
        {
            try
            {
                _watchdogFinished.Reset();
                while (_watchdogRunning)
                {
                    for (int iter = 0; _watchdogRunning && iter < _poolSize; iter++)
                    {
                        if (await _available.WaitAsync(Timeout.Infinite).ConfigureAwait(false))
                        {
                            //_logger.Log("Watchdog has got a ticket");
                            int acquiredResourceId = -1;

                            // Get a resource (by directly indexing into the pool)
                            lock (_occupiedMutex)
                            {
                                acquiredResourceId = iter;
                                for (int c = 0; c < _poolSize; c++)
                                {
                                    if (++acquiredResourceId >= _poolSize)
                                    {
                                        acquiredResourceId = 0;
                                    }
                                    if (!_occupied[acquiredResourceId])
                                    {
                                        _occupied[acquiredResourceId] = true;
                                        _resourcesLeased++;
                                        break;
                                    }
                                }

                                //_logger.Log("Watchdog has acquired resource " + acquiredResourceId);
                            }

                            // Call the functor on it, swapping the value in the pool (we are forced to assume the functor is well-behaved
                            // and has both disposed the old object and properly created the new one, if needed)
                            //_logger.Log("Watchdog executing functor");
                            _resources[acquiredResourceId] = await watchdogFunctor(_resources[acquiredResourceId]).ConfigureAwait(false);

                            // Release the handle (again, using low level operations)
                            lock (_occupiedMutex)
                            {
                                //_logger.Log("Watchdog is releasing resource " + acquiredResourceId);
                                // Find the resources and mark it as unused
                                _occupied[acquiredResourceId] = false;
                                _resourcesLeased--;
                            }

                            _available.Release();
                        }

                        //_logger.Log("Watchdog is waiting for " + watchdogInterval);
                        await realTime.WaitAsync(watchdogInterval, CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Log("Fatal error occurred in the resource pool watchdog: " + e.Message, LogLevel.Err);
            }
            finally
            {
                _watchdogRunning = false;
                _watchdogFinished.Set();
            }
        }

        public void ReportMetrics(IMetricCollector reporter)
        {
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ResourcePool_UsedItems, _dimensions, _resourcesLeased);
            reporter.ReportContinuous(CommonInstrumentation.Key_Counter_ResourcePool_UsedCapacity, _dimensions, (100d * (double)_resourcesLeased / (double)_poolSize));
        }

        public void InitializeMetrics(IMetricCollector collector)
        {
        }
    }

    public class PooledResource<T>
    {
        internal PooledResource(T val, int idx)
        {
            _idx = idx;
            Value = val;
        }

        internal readonly int _idx;

        public T Value
        {
            get;
            private set;
        }

        public static PooledResource<T> Wrap(T val)
        {
            return new PooledResource<T>(val, -1);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is PooledResource<T>))
            {
                return false;
            }

            PooledResource<T> other = obj as PooledResource<T>;

            return _idx == other._idx && Value.Equals(other.Value);
        }

        public override int GetHashCode()
        {
            return _idx.GetHashCode() + Value.GetHashCode();
        }

        public override string ToString()
        {
            return "Pooled<" + Value.ToString() + ">";
        }
    }
}
