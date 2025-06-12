using Durandal.API;
using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Default audio graph implementation which can be configured for optional concurrency and instrumentation
    /// </summary>
    public class AudioGraph : IAudioGraph
    {
        private readonly Guid _graphId;
        private readonly bool _concurrent;
        private readonly bool _instrumented;
        private int _disposed;

        // Only exist for concurrent graphs
        private readonly AsyncLockSlim _lock;
        //
        
        // The following only exist for instrumented graphs
        private readonly Stack<AudioInstrumentationEntry> _componentActivationStack;
        private readonly Counter<string> _componentExclusiveLatencies;
        private readonly FastConcurrentDictionary<string, MovingPercentile> _exclusiveLatencyPercentiles;
        private readonly InstrumentationResultsDelegate _instrumentationDelegate;
        private long _lastInstrumentedReadStartTicks;
        //

        /// <summary>
        /// Delegate that is invoked when this graph is reporting instrumentation data.
        /// </summary>
        /// <param name="componentExclusiveLatencies">A counter containing the name of each component in the operation along with its exclusive time spent running, in milliseconds</param>
        /// <param name="latencyBudget">The real-time length of the audio data that was processed in this operation</param>
        /// <param name="actualInclusiveLatency">The actual real CPU time spent on the entire operation</param>
        public delegate void InstrumentationResultsDelegate(Counter<string> componentExclusiveLatencies, TimeSpan latencyBudget, TimeSpan actualInclusiveLatency);

        public AudioGraph(AudioGraphCapabilities capabilities, InstrumentationResultsDelegate instrumentationDelegate = null)
        {
            _concurrent = capabilities.HasFlag(AudioGraphCapabilities.Concurrent);
            _instrumented = capabilities.HasFlag(AudioGraphCapabilities.Instrumented);

            if (_concurrent)
            {
                _lock = new AsyncLockSlim();
            }

            if (_instrumented)
            {
                _componentActivationStack = new Stack<AudioInstrumentationEntry>();
                _componentExclusiveLatencies = new Counter<string>();
                _exclusiveLatencyPercentiles = new FastConcurrentDictionary<string, MovingPercentile>();
                if (instrumentationDelegate == null)
                {
                    throw new ArgumentNullException(nameof(instrumentationDelegate), "An instrumentation delegate is required when AudioGraphCapabilities.Instrumented is specified");
                }

                _instrumentationDelegate = instrumentationDelegate;
            }

            _graphId = Guid.NewGuid();

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AudioGraph()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc />
        public void LockGraph()
        {
            if (_concurrent)
            {
                _lock.GetLock();
            }
        }

        /// <inheritdoc />
        public ValueTask LockGraphAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_concurrent)
            {
                return _lock.GetLockAsync(cancelToken, realTime);
            }
            else
            {
                return new ValueTask();
            }
        }

        /// <inheritdoc />
        public void UnlockGraph()
        {
            if (_concurrent)
            {
                _lock.Release();
            }
        }

        /// <inheritdoc />
        public void BeginInstrumentedScope(IRealTimeProvider realTime, string nodeName)
        {
            if (_instrumented)
            {
                _componentActivationStack.Clear();
                _componentExclusiveLatencies.Clear();
                _lastInstrumentedReadStartTicks = realTime.TimestampTicks;
                BeginComponentInclusiveScope(realTime, nodeName);
            }
        }

        /// <inheritdoc />
        public void EndInstrumentedScope(IRealTimeProvider realTime, TimeSpan budget)
        {
            if (_instrumented)
            {
                EndComponentInclusiveScope(realTime);

                // Update all percentiles
                foreach (var count in _componentExclusiveLatencies)
                {
                    MovingPercentile percentile;
                    _exclusiveLatencyPercentiles.TryGetValueOrSet(count.Key, out percentile, () => new MovingPercentile(100, 0.25, 0.5, 0.75, 0.95, 0.99));
                    percentile.Add(count.Value);
                }

                // Report statistics to delegate
                _instrumentationDelegate(_componentExclusiveLatencies, budget, TimeSpan.FromTicks(realTime.TimestampTicks - _lastInstrumentedReadStartTicks));
            }
        }

        /// <inheritdoc />
        public void BeginComponentInclusiveScope(IRealTimeProvider realTime, string componentName)
        {
            if (_instrumented)
            {
                componentName = componentName ?? "Unknown";
                long currentTicks = realTime.TimestampTicks;
                if (_componentActivationStack.Count > 0)
                {
                    // Capture exclusive latency between two Begin entries in the stack
                    AudioInstrumentationEntry previousEntry = _componentActivationStack.Peek();
                    long previousExclusiveTimeTicks = currentTicks - previousEntry.LastOperationTimeTicks;
                    float previousExclusiveTimeMs = (float)((double)previousExclusiveTimeTicks / 10000.0);
                    // set or increment the running total for the entry on top of the stack
                    _componentExclusiveLatencies.Increment(previousEntry.ComponentName, previousExclusiveTimeMs);
                }

                // Then push a new entry onto the stack
                _componentActivationStack.Push(new AudioInstrumentationEntry(componentName, currentTicks));
            }
        }

        /// <inheritdoc />
        public void EndComponentInclusiveScope(IRealTimeProvider realTime)
        {
            if (_instrumented)
            {
                long currentTicks = realTime.TimestampTicks;
                if (_componentActivationStack.Count > 0)
                {
                    // Capture exclusive latency between two End entries on the stack
                    AudioInstrumentationEntry componentFinishingNow = _componentActivationStack.Pop();
                    long previousExclusiveTimeTicks = currentTicks - componentFinishingNow.LastOperationTimeTicks;
                    float previousExclusiveTimeMs = (float)((double)previousExclusiveTimeTicks / 10000.0);
                    _componentExclusiveLatencies.Increment(componentFinishingNow.ComponentName, previousExclusiveTimeMs);
                }

                // And update the parent timestamp so its child's exclusive latency doesn't count as the parent's exclusive
                if (_componentActivationStack.Count > 0)
                {
                    _componentActivationStack.Peek().LastOperationTimeTicks = currentTicks;
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            return ((AudioGraph)obj)._graphId == this._graphId;
        }

        public override int GetHashCode()
        {
            return this._graphId.GetHashCode();
        }

        /// <inheritdoc />
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
                _lock?.Dispose();
            }
        }

        /// <summary>
        /// Stack object used to track inclusive latencies for a particular component
        /// </summary>
        private class AudioInstrumentationEntry
        {
            public AudioInstrumentationEntry(string componentName, long operationTimeTicks)
            {
                ComponentName = componentName;
                LastOperationTimeTicks = operationTimeTicks;
            }

            public string ComponentName;
            public long LastOperationTimeTicks;
        }
    }
}
