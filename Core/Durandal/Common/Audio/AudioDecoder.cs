using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Internal.CoreOntology.SchemaDotOrg;
using Durandal.Common.IO;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Base class for an audio decoder, which is an audio graph component which accepts a stream
    /// at initialization time representing the encoded audio data, and has an output
    /// which connects to a regular audio graph the same as any other sample source component.
    /// </summary>
    public abstract class AudioDecoder : IAudioSampleSource
    {
        protected readonly WeakPointer<IAudioGraph> _graph;
        private readonly string _nodeName;
        private readonly string _nodeFullName;
        private NonRealTimeStream _inputStream;
        private HashSet<IDisposable> _extraDisposables;
        private int _disposed = 0;

        public AudioDecoder(
            string codec,
            WeakPointer<IAudioGraph> graph,
            string implementingTypeName,
            string nodeCustomName)
        {
            Codec = codec.AssertNonNull(nameof(codec));
            _graph = graph.AssertNonNull(nameof(graph));
            implementingTypeName.AssertNonNullOrEmpty(nameof(implementingTypeName));
            AudioHelpers.BuildAudioNodeNames(implementingTypeName, nodeCustomName, out _nodeName, out _nodeFullName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AudioDecoder()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc/>
        public IAudioGraph OutputGraph => _graph.Value;

        /// <inheritdoc/>
        public IAudioSampleTarget Output { get; protected set; }

        /// <inheritdoc/>
        public AudioSampleFormat OutputFormat { get; protected set; }

        /// <summary>
        /// Gets the codec which this decoder can handle (as a constant format code such as "aiff")
        /// </summary>
        public string Codec { get; protected set; }

        /// <summary>
        /// Gets a human-readable string describing this codec
        /// </summary>
        public abstract string CodecDescription { get; }

        /// <inheritdoc/>
        public abstract bool PlaybackFinished { get; }

        /// <summary>
        /// Indicates whether this codec has been initialized from an input stream
        /// </summary>
        public bool IsInitialized { get; protected set; }

        /// <inheritdoc/>
        public bool IsActiveNode => false;

        /// <inheritdoc/>
        public string NodeName => _nodeName;

        /// <inheritdoc/>
        public string NodeFullName => _nodeFullName;

        protected bool OwnsStream { get; set; }

        /// <summary>
        /// Attempts to initialize this decoder. This usually involves reading some kind of format header
        /// from the input stream (for example, a riff header indicating the format and layout of the data).
        /// This method must be called once successfully before you can connect the decoder, because the output
        /// format is assumed to be unknown until this header data has been parsed. Some codecs don't require
        /// initialization at all, but this method should still be called for consistency.
        /// </summary>
        /// <param name="inputStream">The input stream that this decoder will read from.</param>
        /// <param name="ownsStream">Indicates whether this object should take responsibility for disposing of the stream.</param>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>An initialization result. A failure code (negative) means this object is stuck in an error state. A success code means you can proceed with decoding.</returns>
        public Task<AudioInitializationResult> Initialize(Stream inputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            NonRealTimeStreamWrapper wrapper = new NonRealTimeStreamWrapper(inputStream, ownsStream);
            return Initialize(wrapper, true, cancelToken, realTime);
        }

        /// <summary>
        /// Attempts to initialize this decoder. This usually involves reading some kind of format header
        /// from the input stream (for example, a riff header indicating the format and layout of the data).
        /// This method must be called once successfully before you can connect the decoder, because the output
        /// format is assumed to be unknown until this header data has been parsed. Some codecs don't require
        /// initialization at all, but this method should still be called for consistency.
        /// </summary>
        /// <param name="inputStream">The input stream that this decoder will read from.</param>
        /// <param name="ownsStream">Indicates whether this object should take responsibility for disposing of the stream.</param>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>An initialization result. A failure code (negative) means this object is stuck in an error state. A success code means you can proceed with decoding.</returns>
        public abstract Task<AudioInitializationResult> Initialize(NonRealTimeStream inputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// The input stream from which this decoder should read audio data from internally.
        /// </summary>
        protected NonRealTimeStream InputStream
        {
            get
            {
                return _inputStream;
            }
            set
            {
                if (!value.CanRead)
                {
                    throw new ArgumentException("Audio decoder input stream must be readable");
                }

                _inputStream = value.AssertNonNull(nameof(InputStream));
            }
        }

        /// <inheritdoc/>
        public void ConnectOutput(IAudioSampleTarget target, bool noRecursiveConnection = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AudioDecoder));
            }

            if (OutputFormat == null || !IsInitialized)
            {
                throw new InvalidOperationException("Codec has not been initialized");
            }

            target.AssertNonNull(nameof(target));
            AudioSampleFormat.AssertFormatsAreEqual(target.InputFormat, OutputFormat);

            if (!this.OutputGraph.Equals(target.InputGraph))
            {
                throw new ArgumentException("Cannot connect audio components that are part of different graphs");
            }

            if (noRecursiveConnection)
            {
                if (Output != null)
                {
                    Output.DisconnectInput(true);
                }

                Output = target;
            }
            else
            {
                _graph.Value.LockGraph();
                try
                {
                    if (Output != target)
                    {
                        if (Output != null)
                        {
                            Output.DisconnectInput(true);
                        }

                        target.ConnectInput(this, true);
                        Output = target;
                    }
                }
                finally
                {
                    _graph.Value.UnlockGraph();
                }
            }
        }

        /// <inheritdoc/>
        public void DisconnectOutput(bool noRecursiveConnection = false)
        {
            if (noRecursiveConnection)
            {
                Output = null;
            }
            else
            {
                _graph.Value.LockGraph();
                try
                {
                    if (Output != null)
                    {
                        Output.DisconnectInput(true);
                        Output = null;
                    }
                }
                finally
                {
                    _graph.Value.UnlockGraph();
                }
            }
        }

        /// <inheritdoc/>
        public void TakeOwnershipOfDisposable(IDisposable obj)
        {
            if (_extraDisposables == null)
            {
                _extraDisposables = new HashSet<IDisposable>();
            }

            if (!_extraDisposables.Contains(obj))
            {
                _extraDisposables.Add(obj);
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return _nodeFullName;
        }

        /// <summary>
        /// Enters a constant decode loop which will drive output to the audio graph
        /// until the input stream is exhausted.
        /// </summary>
        /// <returns>An async task</returns>
        public async Task ReadFully(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelPerBuffer = 65536 / OutputFormat.NumChannels;
            using (PooledBuffer<float> pooledBuf = BufferPool<float>.Rent(samplesPerChannelPerBuffer * OutputFormat.NumChannels))
            {
                while (!PlaybackFinished)
                {
                    await _graph.Value.LockGraphAsync(cancelToken, realTime).ConfigureAwait(false);
                    _graph.Value.BeginInstrumentedScope(realTime, _nodeFullName);
                    try
                    {
                        if (Output == null)
                        {
                            throw new NullReferenceException("Attempted to decode audio while decoder output was null");
                        }

                        int amountRead = await ReadAsync(pooledBuf.Buffer, 0, samplesPerChannelPerBuffer, cancelToken, realTime).ConfigureAwait(false);
                        if (amountRead > 0)
                        {
                            await Output.WriteAsync(pooledBuf.Buffer, 0, amountRead, cancelToken, realTime).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        _graph.Value.EndInstrumentedScope(realTime, AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, samplesPerChannelPerBuffer));
                        _graph.Value.UnlockGraph();
                    }
                }

                await Output.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
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
                if (OwnsStream)
                {
                    _inputStream?.Dispose();
                }

                if (_extraDisposables != null)
                {
                    foreach (IDisposable b in _extraDisposables)
                    {
                        b?.Dispose();
                    }
                }
            }
        }

        /// <inheritdoc/>
        public async ValueTask<int> ReadAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AudioDecoder));
            }

            if (OutputFormat == null || !IsInitialized)
            {
                throw new InvalidOperationException("Codec has not been initialized");
            }

            _graph.Value.BeginComponentInclusiveScope(realTime, _nodeFullName);
            try
            {
                return await ReadAsyncInternal(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime).ConfigureAwait(false);
            }
            finally
            {
                _graph.Value.EndComponentInclusiveScope(realTime);
            }
        }

        /// <summary>
        /// Reads audio samples from this source in a non-blocking way.
        /// </summary>
        /// <param name="targetBuffer">The buffer to read the samples into</param>
        /// <param name="targetOffset">The write offset to the buffer, as an array index</param>
        /// <param name="numSamplesPerChannel">The number of samples PER CHANNEL to read.</param>
        /// <param name="cancelToken">A token for cancelling the operation.</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The number of samples per channel that were actually read.
        /// This does NOT follow C# stream semantics. A return value of 0 means no samples are currently available but try again later. A return value of -1 indicates end of stream.
        /// The read will typically not attempt to wait for samples to become available, though implementations may choose to block or do some buffer prefetching during this time.
        /// Reading from a disconnected graph (e.g. a filter whose input is not connected to any source) will always return 0.</returns>
        protected abstract ValueTask<int> ReadAsyncInternal(float[] targetBuffer, int targetOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime);
    }
}
