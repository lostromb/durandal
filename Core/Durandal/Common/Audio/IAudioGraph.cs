using Durandal.Common.Logger;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// An audio graph manages a collection of audio graph components, and coordinates high-level operations such as locking the graph
    /// in concurrent scenarios and making sure things connect properly, etc.
    /// </summary>
    public interface IAudioGraph : IDisposable
    {
        /// <summary>
        /// Locks the entire audio graph, if it specified <see cref="AudioGraphCapabilities.Concurrent"/> at its creation.
        /// While locked, component connects and disconnects are not allowed, and only one component can read or write
        /// to the graph at a time. Instrumentation data is also made thread-safe for stutter-detecting or fully instrumentation operations.
        /// </summary>
        void LockGraph();

        /// <summary>
        /// Locks the entire audio graph asynchronously, if it specified <see cref="AudioGraphCapabilities.Concurrent"/> at its creation.
        /// While locked, component connects and disconnects are not allowed, and only one component can read or write
        /// to the graph at a time.
        /// </summary>
        /// <param name="cancelToken">A cancel token for acquiring the lock</param>
        /// <param name="realTime">Real time wait provider for acquiring the lock</param>
        ValueTask LockGraphAsync(CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Unlocks the graph after a previous call to <see cref="LockGraph"/> or <see cref="LockGraphAsync"/>.
        /// </summary>
        void UnlockGraph();

        /// <summary>
        /// Begins a top-level instrumented operation, either a read or a write. Typically, only the active node in the
        /// graph will call this method, as it is the one driving the input or output. If the graph specifies that it tracks
        /// instrumentation (via <see cref="AudioGraphCapabilities.Instrumented"/> or similar), then that instrumentation
        /// will be recorded by the graph and written to some output (like a logger). Otherwise, this call is a no-op.
        /// This functional also implicitly calls <see cref="BeginComponentInclusiveScope(IRealTimeProvider, string)"/> for the current component.
        /// </summary>
        /// <param name="realTime">A time provider for tracking component latencies.</param>
        /// <param name="componentName">The name of the active graph component to report to instrumentation. Typically this is the node's full name.</param>
        void BeginInstrumentedScope(IRealTimeProvider realTime, string componentName);

        /// <summary>
        /// Ends a top-level instrumented operation after a previous call to <see cref="BeginInstrumentedScope"/>.
        /// This functional also implicitly calls <see cref="EndComponentInclusiveScope(IRealTimeProvider)"/> for the current component.
        /// </summary>
        /// <param name="realTime">A time provider for tracking component latencies.</param>
        /// <param name="budget">The time length of the audio that was processed in this operation, which represents the real-time budget that should be used for stutter detection</param>
        void EndInstrumentedScope(IRealTimeProvider realTime, TimeSpan budget);

        /// <summary>
        /// Begins a mid-level instrumented operation, either a read or a write. This is typically called by filters or non-active nodes in the graph, in a
        /// nested fashion alongside <see cref="EndComponentInclusiveScope(IRealTimeProvider)"/>
        /// For a single operation, the flow may look something like this:
        /// <list type="number">
        /// <item>Active node A calls <see cref="BeginInstrumentedScope"/> and begins a read from filter B</item>
        /// <item>Filter B calls <see cref="BeginComponentInclusiveScope"/>, does some work, and starts a read from source C.</item>
        /// <item>Source C calls <see cref="BeginComponentInclusiveScope"/> and does some work.</item>
        /// <item>Source C calls <see cref="EndComponentInclusiveScope"/> and returns audio to Filter B.</item>
        /// <item>Filter B calls <see cref="EndComponentInclusiveScope"/> and returns audio to active node A.</item>
        /// <item>Active node calls <see cref="EndInstrumentedScope"/>.</item>
        /// </list>
        /// </summary>
        /// <param name="realTime">A time provider for tracking component latencies.</param>
        /// <param name="componentName">The name of this component to report to instrumentation. Typically this is the node's full name.</param>
        void BeginComponentInclusiveScope(IRealTimeProvider realTime, string componentName);

        /// <summary>
        /// Ends a component-level instrumented scope. Typically called just before returning control to a calling node in the graph, in conjunction with <see cref="BeginComponentInclusiveScope(IRealTimeProvider, string)"/>.
        /// </summary>
        /// <param name="realTime">A time provider for tracking component latencies.</param>
        void EndComponentInclusiveScope(IRealTimeProvider realTime);
    }
}
