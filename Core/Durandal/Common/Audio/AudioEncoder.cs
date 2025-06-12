using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Durandal.Common.Audio
{
    /// <summary>
    /// Base class for an audio encoder, which is an audio graph component which connects to a
    /// regular audio graph, accepts audio samples as input and writes them to a single output
    /// stream which represents encoded audio data.
    /// </summary>
    public abstract class AudioEncoder : IAudioSampleTarget
    {
        protected readonly WeakPointer<IAudioGraph> _graph;
        private readonly string _nodeName;
        private readonly string _nodeFullName;
        private readonly Guid _uniqueId = Guid.NewGuid();
        private NonRealTimeStream _outputStream;
        private HashSet<IDisposable> _extraDisposables;
        private int _disposed = 0;

        public AudioEncoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat inputSampleFormat,
            string implementingTypeName,
            string nodeCustomName)
        {
            _graph = graph.AssertNonNull(nameof(graph));
            InputFormat = inputSampleFormat.AssertNonNull(nameof(inputSampleFormat));
            implementingTypeName.AssertNonNullOrEmpty(nameof(implementingTypeName));
            AudioHelpers.BuildAudioNodeNames(implementingTypeName, nodeCustomName, out _nodeName, out _nodeFullName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AudioEncoder()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc/>
        public Guid NodeId => _uniqueId;

        /// <summary>
        /// Gets the codec which this encoder encoded to (as a constant format code such as "aiff")
        /// </summary>
        public abstract string Codec { get; }

        /// <summary>
        /// Gets the codec parameters used during the encode.
        /// Not guaranteed to be populated until the stream is initialized.
        /// </summary>
        public virtual string CodecParams => string.Empty;

        /// <summary>
        /// Indicates whether this encoder has been initalized to an output stream
        /// </summary>
        public bool IsInitialized { get; protected set; }

        /// <inheritdoc/>
        public IAudioGraph InputGraph => _graph.Value;

        /// <inheritdoc/>
        public IAudioSampleSource Input { get; private set; }

        /// <inheritdoc/>
        public AudioSampleFormat InputFormat { get; private set; }

        /// <inheritdoc/>
        public virtual bool IsActiveNode => false;

        /// <inheritdoc/>
        public string NodeName => _nodeName;

        /// <inheritdoc/>
        public string NodeFullName => _nodeFullName;

        protected NonRealTimeStream OutputStream
        {
            get
            {
                return _outputStream;
            }
            set
            {
                if (!value.CanWrite)
                {
                    throw new ArgumentException("Audio encoder output stream must be writable");
                }

                _outputStream = value.AssertNonNull(nameof(OutputStream));
            }
        }

        protected bool OwnsStream { get; set; }

        /// <summary>
        /// Attempts to initialize this encoder. This usually involves writing some kind of format header
        /// to the output stream stream (for example, a riff header indicating the format and layout of the data).
        /// Some codecs don't require initialization at all, but this method should still be called for consistency.
        /// </summary>
        /// <param name="outputStream">The output stream that this encoder will write to.</param>
        /// <param name="ownsStream">Indicates whether this object should take responsibility for disposing of the stream.</param>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>An initialization result. A failure code (negative) means this object is stuck in an error state. A success code means you can proceed with encoding.</returns>
        public Task<AudioInitializationResult> Initialize(Stream outputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            NonRealTimeStreamWrapper wrapper = new NonRealTimeStreamWrapper(outputStream, ownsStream);
            return Initialize(wrapper, true, cancelToken, realTime);
        }

        /// <summary>
        /// Attempts to initialize this encoder. This usually involves writing some kind of format header
        /// to the output stream stream (for example, a riff header indicating the format and layout of the data).
        /// Some codecs don't require initialization at all, but this method should still be called for consistency.
        /// </summary>
        /// <param name="outputStream">The output stream that this decoder will write to.</param>
        /// <param name="ownsStream">Indicates whether this object should take responsibility for disposing of the stream.</param>
        /// <param name="cancelToken">A cancellation token.</param>
        /// <param name="realTime">A definition of real time.</param>
        /// <returns>An initialization result. A failure code (negative) means this object is stuck in an error state. A success code means you can proceed with encoding.</returns>
        public abstract Task<AudioInitializationResult> Initialize(NonRealTimeStream outputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <inheritdoc/>
        public void ConnectInput(IAudioSampleSource source, bool noRecursiveConnection = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AudioEncoder));
            }

            source.AssertNonNull(nameof(source));
            AudioSampleFormat.AssertFormatsAreEqual(source.OutputFormat, InputFormat);

            if (!this.InputGraph.Equals(source.OutputGraph))
            {
                throw new ArgumentException("Cannot connect audio components that are part of different graphs");
            }

            if (noRecursiveConnection)
            {
                if (Input != null)
                {
                    Input.DisconnectOutput(true);
                }

                Input = source;
            }
            else
            {
                _graph.Value.LockGraph();
                try
                {
                    if (Input != source)
                    {
                        if (Input != null)
                        {
                            Input.DisconnectOutput(true);
                        }

                        source.ConnectOutput(this, true);
                        Input = source;
                    }
                }
                finally
                {
                    _graph.Value.UnlockGraph();
                }
            }
        }

        /// <inheritdoc/>
        public void DisconnectInput(bool noRecursiveConnection = false)
        {
            if (noRecursiveConnection)
            {
                Input = null;
            }
            else
            {
                bool disconnectHappened = false;
                _graph.Value.LockGraph();
                try
                {
                    if (Input != null)
                    {
                        Input.DisconnectOutput(true);
                        Input = null;
                        disconnectHappened = true;
                    }
                }
                finally
                {
                    _graph.Value.UnlockGraph();
                }

                if (disconnectHappened)
                {
                    // OnInputDisconnected();
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

        public async ValueTask<int> ReadFromSource(int desiredReadSizeSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AudioEncoder));
            }

            if (InputFormat == null || !IsInitialized)
            {
                throw new InvalidOperationException("Codec has not been initialized");
            }

            using (PooledBuffer<float> pooledBuf = BufferPool<float>.Rent(desiredReadSizeSamplesPerChannel * InputFormat.NumChannels))
            {
                await _graph.Value.LockGraphAsync(cancelToken, realTime).ConfigureAwait(false);
                _graph.Value.BeginInstrumentedScope(realTime, _nodeFullName);
                try
                {
                    if (Input == null)
                    {
                        return 0;
                    }
                    else if (Input.PlaybackFinished)
                    {
                        return -1;
                    }
                    else
                    {
                        int amountRead = await Input.ReadAsync(pooledBuf.Buffer, 0, desiredReadSizeSamplesPerChannel, cancelToken, realTime).ConfigureAwait(false);

                        if (amountRead > 0)
                        {
                            await WriteAsyncInternal(pooledBuf.Buffer, 0, amountRead, cancelToken, realTime).ConfigureAwait(false);
                        }

                        return amountRead;
                    }
                }
                finally
                {
                    _graph.Value.EndInstrumentedScope(realTime, AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, desiredReadSizeSamplesPerChannel));
                    _graph.Value.UnlockGraph();
                }
            }
        }

        /// <summary>
        /// Enters a constant encode loop which will drive input from the audio graph and output
        /// encoded data to the output stream, until the audio graph finishes playback.
        /// </summary>
        /// <returns>An async task</returns>
        public async Task ReadFully(CancellationToken cancelToken, IRealTimeProvider realTime, TimeSpan bufferLength)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AudioEncoder));
            }

            if (InputFormat == null || !IsInitialized)
            {
                throw new InvalidOperationException("Codec has not been initialized");
            }

            int bufferLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.SampleRateHz, bufferLength);
            using (PooledBuffer<float> pooledBuf = BufferPool<float>.Rent(bufferLengthSamplesPerChannel * InputFormat.NumChannels))
            {
                bool finished = false;
                while (!finished)
                {
                    await _graph.Value.LockGraphAsync(cancelToken, realTime).ConfigureAwait(false);
                    _graph.Value.BeginInstrumentedScope(realTime, _nodeFullName);
                    try
                    {
                        if (Input == null || Input.PlaybackFinished)
                        {
                            finished = true;
                        }
                        else
                        {
                            int amountRead = await Input.ReadAsync(pooledBuf.Buffer, 0, bufferLengthSamplesPerChannel, cancelToken, realTime).ConfigureAwait(false);

                            if (amountRead > 0)
                            {
                                await WriteAsyncInternal(pooledBuf.Buffer, 0, amountRead, cancelToken, realTime).ConfigureAwait(false);
                            }
                        }
                    }
                    finally
                    {
                        _graph.Value.EndInstrumentedScope(realTime, AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, bufferLengthSamplesPerChannel));
                        _graph.Value.UnlockGraph();
                    }
                }

                await Finish(cancelToken, realTime).ConfigureAwait(false);
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
                    _outputStream?.Dispose();
                }
                else
                {
                    _outputStream?.Flush();
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

        /// <summary>
        /// Must be called at the end of encoding to ensure that the final data is written to the output stream and closed properly.
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <param name="realTime"></param>
        /// <returns></returns>
        public ValueTask Finish(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AudioEncoder));
            }

            if (InputFormat == null || !IsInitialized)
            {
                throw new InvalidOperationException("Codec has not been initialized");
            }

            return FinishInternal(cancelToken, realTime);
        }

        /// <inheritdoc/>
        public async ValueTask FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AudioEncoder));
            }

            if (InputFormat == null || !IsInitialized)
            {
                throw new InvalidOperationException("Codec has not been initialized");
            }

            if (Input != null)
            {
                await FlushAsyncInternal(cancelToken, realTime).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async ValueTask WriteAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AudioEncoder));
            }

            if (InputFormat == null || !IsInitialized)
            {
                throw new InvalidOperationException("Codec has not been initialized");
            }

            _graph.Value.BeginComponentInclusiveScope(realTime, NodeFullName);
            try
            {
                await WriteAsyncInternal(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime).ConfigureAwait(false);
            }
            finally
            {
                _graph.Value.EndComponentInclusiveScope(realTime);
            }
        }

        /// <summary>
        /// Internal implementation of WriteAsync().
        /// The base class provides you with the following guarantees: 1) Read/Write are protected by a mutex 2) Output is non-null and constant.
        /// </summary>
        /// <param name="buffer">The buffer to write samples to</param>
        /// <param name="bufferOffset">The array offset when writing to output buffer</param>
        /// <param name="numSamplesPerChannel">The number of samples per channel to write</param>
        /// <param name="cancelToken">A cancellation token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        protected abstract ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Internal implementation of FlushAsync().
        /// The base class provides you with the following guarantees: 1) Read/Write are protected by a mutex 2) Output is non-null and constant.
        /// </summary>
        /// <param name="cancelToken">A cancellation token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        protected virtual ValueTask FlushAsyncInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return new ValueTask();
        }

        /// <summary>
        /// Internal implementation of Finish().
        /// Note that, unlike Flush() and Write(), the audio graph is NOT locked and you are NOT guaranteed to be connected to anything.
        /// It is expected that the finish operation should not require the audio graph or input components at all.
        /// </summary>
        /// <param name="cancelToken">A cancellation token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        protected virtual ValueTask FinishInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return new ValueTask();
        }
    }
}
