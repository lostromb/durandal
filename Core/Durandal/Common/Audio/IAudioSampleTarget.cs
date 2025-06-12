using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Defines a component in an audio graph to which audio samples can be written.
    /// </summary>
    public interface IAudioSampleTarget : IAudioGraphComponent
    {
        /// <summary>
        /// Gets the audio graph that the input to this component is part of (some special components may span
        /// multiple graphs and thus have separate values for input and output)
        /// </summary>
        IAudioGraph InputGraph { get; }
        
        /// <summary>
        /// The audio graph component that this component's input will read from
        /// </summary>
        IAudioSampleSource Input { get; }

        /// <summary>
        /// The expected input format of audio samples that are written to this component.
        /// </summary>
        AudioSampleFormat InputFormat { get; }

        /// <summary>
        /// Writes audio samples to this target. All samples are guaranteed to be written.
        /// </summary>
        /// <param name="buffer">The buffer to read the samples from</param>
        /// <param name="bufferOffset">The read offset to the buffer, as an array index</param>
        /// <param name="numSamplesPerChannel">The number of samples PER CHANNEL to write.</param>
        /// <param name="cancelToken">A token for cancelling the operation.</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        ValueTask WriteAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Attempts to flush all outbound samples that may be cached in this audio sample target, and to all downstream components.
        /// Generally this is done right after an active sample source finishes playback, but not always.
        /// </summary>
        /// <returns>An async task</returns>
        ValueTask FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Connects this component's input to an output component in an audio graph.
        /// All connections are bidirectional so you don't need to worry about calling Connect() on the other side.
        /// </summary>
        /// <param name="source">The graph component to connect to.</param>
        /// <param name="noRecursiveConnection">Used internally to manage recursive connections. User code should set this to FALSE always.</param>
        void ConnectInput(IAudioSampleSource source, bool noRecursiveConnection = false);

        /// <summary>
        /// Disconnects the input of this component.
        /// The disconnection is bidirectional so you don't need to worry about calling Disconnect() on the other side.
        /// </summary>
        /// <param name="noRecursiveConnection">Used internally to manage recursive connections. User code should set this to FALSE always.</param>
        void DisconnectInput(bool noRecursiveConnection = false);
    }
}
