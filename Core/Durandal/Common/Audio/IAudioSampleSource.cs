using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Defines a component in an audio graph from which audio samples can be read.
    /// </summary>
    public interface IAudioSampleSource : IAudioGraphComponent
    {
        /// <summary>
        /// Gets the audio graph that the input to this component is part of (some special components may span
        /// multiple graphs and thus have separate values for input and output)
        /// </summary>
        IAudioGraph OutputGraph { get; }

        /// <summary>
        /// The audio graph component that this component's output will go to
        /// </summary>
        IAudioSampleTarget Output { get; }

        /// <summary>
        /// The format descriptor of samples that are output by this component.
        /// </summary>
        AudioSampleFormat OutputFormat { get; }

        /// <summary>
        /// Gets a value indicating whether all samples have been exhausted from this source.
        /// Setting this to true is a flag that more samples will _never_ be produced, and this component
        /// can be disposed of or otherwise disconnected at the graph's discretion.
        /// </summary>
        bool PlaybackFinished { get; }

        /// <summary>
        /// Reads audio samples from this source in a non-blocking way.
        /// </summary>
        /// <param name="buffer">The buffer to read the samples into</param>
        /// <param name="bufferOffset">The write offset to the buffer, as an array index</param>
        /// <param name="numSamplesPerChannel">The number of samples PER CHANNEL to read.</param>
        /// <param name="cancelToken">A token for cancelling the operation.</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The number of samples per channel that were actually read.
        /// This does NOT follow C# stream semantics. A return value of 0 means no samples are currently available but try again later. A return value of -1 indicates end of stream.
        /// The read will typically not attempt to wait for samples to become available, though implementations may choose to block or do some buffer prefetching during this time.
        /// Reading from a disconnected graph (e.g. a filter whose input is not connected to any source) will always return 0.</returns>
        ValueTask<int> ReadAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Connects this component's output to an input component in an audio graph.
        /// All connections are bidirectional so you don't need to worry about calling Connect() on the other side.
        /// </summary>
        /// <param name="target">The graph component to connect to.</param>
        /// <param name="noRecursiveConnection">Used internally to manage recursive connections. User code should set this to FALSE always.</param>
        void ConnectOutput(IAudioSampleTarget target, bool noRecursiveConnection = false);

        /// <summary>
        /// Disconnects the output of this component.
        /// The disconnection is bidirectional so you don't need to worry about calling Disconnect() on the other side.
        /// </summary>
        /// <param name="noRecursiveConnection">Used internally to manage recursive connections. User code should set this to FALSE always.</param>
        void DisconnectOutput(bool noRecursiveConnection = false);
    }
}
