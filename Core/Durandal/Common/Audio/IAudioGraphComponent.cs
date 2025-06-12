using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Represents an abstract component in the audio pipeline, usually as a member of an <see cref="IAudioGraph"/>.
    /// </summary>
    public interface IAudioGraphComponent : IDisposable
    {
        /// <summary>
        /// The custom name (if any) of this audio node in the graph. Useful for debugging and instrumentation.
        /// Cannot be null. The default value of this is just the name of the implementation class.
        /// </summary>
        string NodeName { get; }

        /// <summary>
        /// A concatenation of the node name and its implementing type (or just the type name if there is no custom node name).
        /// </summary>
        string NodeFullName { get; }

        /// <summary>
        /// Indicates whether this component is an "active" node. "Active" means that the component
        /// has a thread loop and is actively pushing or pulling samples from a node graph.
        /// Generally, a graph can have only 1 active node, as that maximizes simplicity and minimizes latency.
        /// </summary>
        bool IsActiveNode { get; }
    }
}
