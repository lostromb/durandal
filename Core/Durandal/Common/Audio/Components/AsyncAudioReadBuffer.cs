using Durandal.Common.Cache;
using Durandal.Common.Collections;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
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
using System.Xml;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// The purpose of this class is to provide a buffer between a realtime "pulling" audio component (such as speakers which require low latency)
    /// and potentially unpredictable high-latency components such as decoders which read from a file, from a network, etc.
    /// The idea is to create an air-gap between two different audio graphs. When something reads from this buffer, it immediately pulls
    /// whatever audio is currently in the buffer, and then it queues a background task to fill the buffer again from the input asynchronously.
    /// The caller achieves low latency because the potentially slow read operation is done in a background task, and when it finishes,
    /// it puts the read audio into the buffer to be read again in subsequent calls. This introduces some latency, which can be tweaked using
    /// the <see cref="DesiredBufferLength"/> property.
    /// This class is counterparts to the <see cref="AsyncAudioWriteBuffer"/>; whereas that one is write-only, this is read-only.
    /// </summary>
    public sealed class AsyncAudioReadBuffer : IAudioSampleSource, IAudioSampleTarget, IAudioDelayingFilter
    {
        private static readonly LockFreeCache<BufferSegment> _bufferAllocationPool = new LockFreeCache<BufferSegment>(128); // Reuse old buffer segments to avoid allocations

        private readonly AsyncLockSlim _bufferLock = new AsyncLockSlim();
        private readonly Queue<BufferSegment> _buffers = new Queue<BufferSegment>(); // The buffer itself, guarded by BufferLock
        private int _samplesPerChannelInBuffer = 0; // Number of samples per channel currently buffered, guarded by BufferLock
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly string _nodeName;
        private readonly string _nodeFullName;
        private readonly Guid _uniqueId = Guid.NewGuid();
        private readonly CancellationTokenSource _backgroundTaskCancel = new CancellationTokenSource();
        private readonly ILogger _logger;
        private readonly AutoResetEventAsync _backgroundTaskFinished = new AutoResetEventAsync(true);
        private readonly WeakPointer<IAudioGraph> _inputGraph;
        private readonly WeakPointer<IAudioGraph> _outputGraph;

        private TimeSpan _desiredBufferLength;
        private volatile bool _playbackFinished = false;
        private int _disposed = 0;

        /// <summary>
        /// Creates a new <see cref="AsyncAudioReadBuffer"/>.
        /// </summary>
        /// <param name="inputGraph">The input audio graph.</param>
        /// <param name="outputGraph">The output audio graph. Must be different from the input graph.</param>
        /// <param name="format">The input/output format of audio.</param>
        /// <param name="nodeCustomName">A name to assign to this component, or null</param>
        /// <param name="desiredBufferLength">The desired length of data to keep buffered</param>
        /// <param name="logger">A logger</param>
        /// <param name="metrics">Metrics for reporting buffer status</param>
        /// <param name="metricDimensions">Dimensions for metrics</param>
        public AsyncAudioReadBuffer(
            WeakPointer<IAudioGraph> inputGraph,
            WeakPointer<IAudioGraph> outputGraph,
            AudioSampleFormat format,
            string nodeCustomName,
            TimeSpan desiredBufferLength,
            ILogger logger,
            WeakPointer<IMetricCollector> metrics,
            DimensionSet metricDimensions)
        {
            _inputGraph = inputGraph.AssertNonNull(nameof(inputGraph));
            _outputGraph = outputGraph.AssertNonNull(nameof(outputGraph));
            if (ReferenceEquals(inputGraph.Value, outputGraph.Value))
            {
                throw new ArgumentException("Input and output graphs must be separate objects.");
            }

            _metrics = metrics.AssertNonNull(nameof(metrics));
            _metricDimensions = metricDimensions.AssertNonNull(nameof(metricDimensions));
            _logger = logger.AssertNonNull(nameof(logger));
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            DesiredBufferLength = desiredBufferLength;

            AudioHelpers.BuildAudioNodeNames(nameof(AsyncAudioReadBuffer), nodeCustomName, out _nodeName, out _nodeFullName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AsyncAudioReadBuffer()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc/>
        public Guid NodeId => _uniqueId;

        /// <inheritdoc/>
        public IAudioGraph InputGraph => _inputGraph.Value;

        /// <inheritdoc/>
        public IAudioGraph OutputGraph => _outputGraph.Value;

        /// <inheritdoc/>
        public IAudioSampleTarget Output { get; private set; }

        /// <inheritdoc/>
        public AudioSampleFormat OutputFormat { get; private set; }

        /// <inheritdoc/>
        public IAudioSampleSource Input { get; private set; }

        /// <inheritdoc/>
        public AudioSampleFormat InputFormat { get; private set; }

        /// <inheritdoc/>
        public bool IsActiveNode => false;

        /// <inheritdoc/>
        public string NodeName => _nodeName;

        /// <inheritdoc/>
        public string NodeFullName => _nodeFullName;

        /// <inheritdoc/>
        public bool PlaybackFinished
        {
            get
            {
                _bufferLock.GetLock();
                try
                {
                    return _playbackFinished;
                }
                finally
                {
                    _bufferLock.Release();
                }
            }
        }

        /// <inheritdoc />
        public TimeSpan AlgorithmicDelay
        {
            get
            {
                _bufferLock.GetLock();
                try
                {
                    return AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, _samplesPerChannelInBuffer);
                }
                finally
                {
                    _bufferLock.Release();
                }
            }
        }

        /// <summary>
        /// Gets or sets the amount of audio that you want to keep buffered via async reads from the input.
        /// This directly corresponds to the expected algorithmic delay of this component.
        /// </summary>
        public TimeSpan DesiredBufferLength
        {
            get
            {
                return _desiredBufferLength;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException("Buffer size must be greater than zero");
                }

                _desiredBufferLength = value;
            }
        }

        /// <inheritdoc/>
        public void ConnectOutput(IAudioSampleTarget target, bool noRecursiveConnection = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AsyncAudioReadBuffer));
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
                OutputGraph.LockGraph();
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
                    OutputGraph.UnlockGraph();
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
                OutputGraph.LockGraph();
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
                    OutputGraph.UnlockGraph();
                }
            }
        }

        /// <inheritdoc/>
        public void ConnectInput(IAudioSampleSource source, bool noRecursiveConnection = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AsyncAudioReadBuffer));
            }

            if (PlaybackFinished)
            {
                throw new InvalidOperationException("Can't connect an audio component to something else after its playback has finished");
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
                InputGraph.LockGraph();
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
                    InputGraph.UnlockGraph();
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
                InputGraph.LockGraph();
                try
                {
                    if (Input != null)
                    {
                        Input.DisconnectOutput(true);
                        Input = null;
                    }
                }
                finally
                {
                    InputGraph.UnlockGraph();
                }
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return _nodeFullName;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                BufferSegment segment;
                while (_buffers.Count > 0)
                {
                    segment = _buffers.Dequeue();
                    segment.Buffer?.Dispose();
                }

                segment = _bufferAllocationPool.TryDequeue();
                while (segment != null)
                {
                    segment.Buffer?.Dispose();
                    segment = _bufferAllocationPool.TryDequeue();
                }

                _backgroundTaskCancel?.Cancel();
                _backgroundTaskCancel?.Dispose();
                _bufferLock?.Dispose();
            }
        }

        /// <inheritdoc/>
        public ValueTask FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotSupportedException("Cannot write to an async read buffer");
        }

        /// <inheritdoc/>
        public async ValueTask<int> ReadAsync(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AsyncAudioReadBuffer));
            }

            OutputGraph.BeginComponentInclusiveScope(realTime, _nodeFullName);

            if (_playbackFinished)
            {
                OutputGraph.EndComponentInclusiveScope(realTime);
                return -1;
            }

            // Try to read from what's in the current buffer
            int amountReadFromLocalBuffer;
            int amountStillNeededToFillBuffer;
            await _bufferLock.GetLockAsync(cancelToken, realTime).ConfigureAwait(false);
            try
            {
                amountReadFromLocalBuffer = ReadFromBufferInternal(buffer, offset, count, cancelToken, realTime);
                amountStillNeededToFillBuffer = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.SampleRateHz, _desiredBufferLength) - _samplesPerChannelInBuffer;
            }
            finally
            {
                _bufferLock.Release();
            }

            // And queue a background read to fill the buffer for next time (if there's not already a read going)
            FillBufferInBackground(cancelToken, realTime, amountStillNeededToFillBuffer);
            OutputGraph.EndComponentInclusiveScope(realTime);
            return amountReadFromLocalBuffer;
        }

        /// <summary>
        /// Atomically starts a new background read task if 1) the buffer is less than the desired fullness and 2) there's not already a read task running.
        /// Generally used to prime the buffer before use. The synchronous latency of this method call should be almost instantaneous.
        /// </summary>
        /// <param name="cancelToken">Cancel token to use for the background task</param>
        /// <param name="realTime">Real time to use for the background thread</param>
        /// <param name="samplesPerChannelToRead">The desired samples per channel to read from input. If null, this will be set to the amount required to fill the buffer to the previously set desired size</param>
        public void FillBufferInBackground(CancellationToken cancelToken, IRealTimeProvider realTime, int? samplesPerChannelToRead = null)
        {
            int backgroundReadLength;
            if (samplesPerChannelToRead.HasValue)
            {
                backgroundReadLength = samplesPerChannelToRead.Value;
            }
            else
            {
                _bufferLock.GetLock(cancelToken, realTime);
                try
                {
                    backgroundReadLength = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.SampleRateHz, _desiredBufferLength) - _samplesPerChannelInBuffer;
                }
                finally
                {
                    _bufferLock.Release();
                }
            }

            if (backgroundReadLength > 0 && !_playbackFinished && _backgroundTaskFinished.TryGetAndClear())
            {
                BackgroundTaskClosure closure = new BackgroundTaskClosure(this, _backgroundTaskCancel.Token, realTime.Fork("AsyncAudioReadBuffer"), backgroundReadLength);
                Task.Run(closure.Run).Forget(_logger);
            }
        }
        
        /// <summary>
        /// Waits for any current background read task (pulling async from the input) to finish. If no task is running,
        /// this returns instantly.
        /// </summary>
        /// <param name="cancelToken">A cancel token for the wait.</param>
        /// <returns>An async task.</returns>
        public async Task WaitForCurrentReadToFinish(CancellationToken cancelToken)
        {
            await _backgroundTaskFinished.WaitAsync(cancelToken).ConfigureAwait(false);
            _backgroundTaskFinished.Set();
        }

        // Closure class for the background task which reads from 
        private class BackgroundTaskClosure
        {
            private readonly CancellationToken _cancelToken;
            private readonly IRealTimeProvider _realTime;
            private readonly int _inputReadLength;
            private readonly AsyncAudioReadBuffer _parent;

            public BackgroundTaskClosure(AsyncAudioReadBuffer parent, CancellationToken threadCancelToken, IRealTimeProvider threadLocalTime, int inputReadLength)
            {
                _parent = parent;
                _inputReadLength = inputReadLength;
                _cancelToken = threadCancelToken;
                _realTime = threadLocalTime;
            }

            public async Task Run()
            {
                try
                {
                    PooledBuffer<float> buf = null;
                    int samplesPerChannelReadFromInput = 0;

                    await _parent.InputGraph.LockGraphAsync(_cancelToken, _realTime).ConfigureAwait(false);
                    try
                    {
                        _parent.InputGraph.BeginInstrumentedScope(_realTime, _parent.NodeFullName);

                        // Check if the input was disconnected while we were waiting for the input graph lock
                        if (_parent.Input == null)
                        {
                            return;
                        }

                        // Do the read from upstream
                        buf = BufferPool<float>.Rent(_inputReadLength * _parent.InputFormat.NumChannels);
                        samplesPerChannelReadFromInput = await _parent.Input.ReadAsync(buf.Buffer, 0, _inputReadLength, _cancelToken, _realTime).ConfigureAwait(false);
                    }
                    finally
                    {
                        _parent.InputGraph.EndInstrumentedScope(_realTime, _parent._desiredBufferLength);
                        _parent.InputGraph.UnlockGraph();
                    }

                    await _parent._bufferLock.GetLockAsync(_cancelToken, _realTime).ConfigureAwait(false);
                    try
                    {
                        if (samplesPerChannelReadFromInput <= 0)
                        {
                            buf?.Dispose();
                        }
                        else
                        {
                            BufferSegment newSegment = AllocateBufferSegment(buf, samplesPerChannelReadFromInput);
                            _parent._buffers.Enqueue(newSegment);
                            _parent._samplesPerChannelInBuffer += samplesPerChannelReadFromInput;
                        }

                        // Check for playback finished. We have to do it here because this is point
                        // where we can tell for sure that input has finished and our buffer is empty.
                        if (_parent._samplesPerChannelInBuffer == 0 && _parent.Input.PlaybackFinished)
                        {
                            _parent._playbackFinished = true;
                        }
                    }
                    finally
                    {
                        _parent._bufferLock.Release();
                    }
                }
                catch (Exception e)
                {
                    _parent._logger.Log(e);
                }
                finally
                {
                    // Signal that the thread is finished
                    _parent._backgroundTaskFinished.Set();
                    _realTime.Merge();
                }
            }
        }

        /// <inheritdoc/>
        public ValueTask WriteAsync(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotSupportedException("Cannot write to an async read buffer");
        }

        // This method does NOT acquire bufferlock - it relies on the caller to manage that
        private int ReadFromBufferInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int amountReadPerChannel = 0;
            if (_samplesPerChannelInBuffer == 0)
            {
                // Buffer underrun but input has not finished playback yet. Return zero samples available
                _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "{0} underflowed by {1} samples", _nodeFullName, count);
                _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_AsyncAudioReadBuffer_Underflow_Samples, _metricDimensions, count);
                return 0;
            }

            while (_samplesPerChannelInBuffer > 0 && amountReadPerChannel < count)
            {
                // Read from buffer as much as possible
                BufferSegment nextSegment = _buffers.Peek();
                int amountCanReadFromThisBuffer = nextSegment.SamplesPerChannel - nextSegment.AmountReadPerChannel;
                int amountLeftToReadTotal = count - amountReadPerChannel;
                int amountToReadFromThisBuffer = FastMath.Min(amountCanReadFromThisBuffer, amountLeftToReadTotal);
                ArrayExtensions.MemCopy(
                    nextSegment.Buffer.Buffer,
                    nextSegment.AmountReadPerChannel * InputFormat.NumChannels,
                    buffer,
                    (offset + (amountReadPerChannel * InputFormat.NumChannels)),
                    amountToReadFromThisBuffer * InputFormat.NumChannels);

                nextSegment.AmountReadPerChannel += amountToReadFromThisBuffer;
                if (nextSegment.AmountReadPerChannel >= nextSegment.SamplesPerChannel)
                {
                    nextSegment.Buffer.Dispose();
                    nextSegment.Buffer = null;
                    _bufferAllocationPool.TryEnqueue(nextSegment);
                    _buffers.Dequeue();
                }

                amountReadPerChannel += amountToReadFromThisBuffer;
                _samplesPerChannelInBuffer -= amountToReadFromThisBuffer;
            }

            return amountReadPerChannel;
        }

        private static BufferSegment AllocateBufferSegment(PooledBuffer<float> wrappedBuf, int validSamplesPerChannel)
        {
            BufferSegment returnVal = _bufferAllocationPool.TryDequeue();

            if (returnVal != null)
            {
                returnVal.Buffer = wrappedBuf;
                returnVal.SamplesPerChannel = validSamplesPerChannel;
                returnVal.AmountReadPerChannel = 0;
            }
            else
            {
                returnVal = new BufferSegment()
                {
                    Buffer = wrappedBuf,
                    SamplesPerChannel = validSamplesPerChannel,
                    AmountReadPerChannel = 0
                };
            }

            return returnVal;
        }

        private class BufferSegment
        {
            public PooledBuffer<float> Buffer; // Pooled buffer of data
            public int SamplesPerChannel; // Total number of samples in this buffer
            public int AmountReadPerChannel; // Cursor for samples we have drained from this buffer to output
        }
    }
}
