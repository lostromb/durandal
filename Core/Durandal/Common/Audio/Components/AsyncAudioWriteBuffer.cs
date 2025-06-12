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
    /// The purpose of this class is to provide a buffer between a realtime p"pushing" audio component (such as a microphone)
    /// and a potentially high-latency processing pipeline such as an audio encoder, speech recognition, writing to a network, etc.
    /// This class is counterparts to the <see cref="AsyncAudioReadBuffer"/>; whereas that one is read-only, this is write-only.
    /// </summary>
    public sealed class AsyncAudioWriteBuffer : IAudioSampleSource, IAudioSampleTarget, IAudioDelayingFilter
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

        private TimeSpan _maxBufferLength;
        private int _disposed = 0;

        /// <summary>
        /// Creates a new <see cref="AsyncAudioWriteBuffer"/>.
        /// </summary>
        /// <param name="inputGraph">The input audio graph.</param>
        /// <param name="outputGraph">The output audio graph. Must be different from the input graph.</param>
        /// <param name="format">The input/output format of audio.</param>
        /// <param name="nodeCustomName">A name to assign to this component, or null</param>
        /// <param name="maxBufferLength">The desired length of data to keep buffered</param>
        /// <param name="logger">A logger</param>
        /// <param name="metrics">Metrics for reporting buffer status</param>
        /// <param name="metricDimensions">Dimensions for metrics</param>
        public AsyncAudioWriteBuffer(
            WeakPointer<IAudioGraph> inputGraph,
            WeakPointer<IAudioGraph> outputGraph,
            AudioSampleFormat format,
            string nodeCustomName,
            TimeSpan maxBufferLength,
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
            MaxBufferLength = maxBufferLength;

            AudioHelpers.BuildAudioNodeNames(nameof(AsyncAudioWriteBuffer), nodeCustomName, out _nodeName, out _nodeFullName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AsyncAudioWriteBuffer()
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
                    IAudioSampleSource source = Input;
                    return source == null ? false : source.PlaybackFinished && _samplesPerChannelInBuffer == 0;
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
        /// Gets or sets the maximum amount of audio you want to allow to back up on a slow write downstream.
        /// If more than this gets written quickly to the buffer, samples will start getting dropped.
        /// This directly corresponds to the expected algorithmic delay of this component.
        /// </summary>
        public TimeSpan MaxBufferLength
        {
            get
            {
                return _maxBufferLength;
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException("Buffer size must be greater than zero");
                }

                _maxBufferLength = value;
            }
        }

        /// <inheritdoc/>
        public void ConnectOutput(IAudioSampleTarget target, bool noRecursiveConnection = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AsyncAudioWriteBuffer));
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
                throw new ObjectDisposedException(nameof(AsyncAudioWriteBuffer));
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
        public async ValueTask WriteAsync(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AsyncAudioWriteBuffer));
            }

            InputGraph.BeginComponentInclusiveScope(realTime, _nodeFullName);

            // Queue data to the buffer
            await _bufferLock.GetLockAsync(cancelToken, realTime).ConfigureAwait(false);
            try
            {
                // enforce maximum buffer limits
                int samplesPerChannelCanAcceptFromCaller = Math.Min(count, (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.SampleRateHz, _maxBufferLength) - _samplesPerChannelInBuffer);
                if (samplesPerChannelCanAcceptFromCaller < count)
                {
                    _logger.LogFormat(LogLevel.Wrn, DataPrivacyClassification.SystemMetadata, "{0} overflowed by {1} samples", _nodeFullName, count - samplesPerChannelCanAcceptFromCaller);
                    _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_AsyncAudioReadBuffer_Underflow_Samples, _metricDimensions, count - samplesPerChannelCanAcceptFromCaller);
                }

                if (samplesPerChannelCanAcceptFromCaller > 0)
                {
                    PooledBuffer<float> pooledBuf = BufferPool<float>.Rent(samplesPerChannelCanAcceptFromCaller * InputFormat.NumChannels);
                    ArrayExtensions.MemCopy(buffer, offset, pooledBuf.Buffer, 0, samplesPerChannelCanAcceptFromCaller * InputFormat.NumChannels);
                    BufferSegment newSegment = AllocateBufferSegment(pooledBuf, samplesPerChannelCanAcceptFromCaller);
                    _buffers.Enqueue(newSegment);
                    _samplesPerChannelInBuffer += samplesPerChannelCanAcceptFromCaller;

                    // And queue the thread to write asynchronously to output (if there's not already a thread going)
                    if (_backgroundTaskFinished.TryGetAndClear())
                    {
                        BackgroundTaskClosure closure = new BackgroundTaskClosure(this, _backgroundTaskCancel.Token, realTime.Fork("AsyncAudioWriteBuffer"));
                        Task.Run(closure.Run).Forget(_logger);
                    }
                }
            }
            finally
            {
                _bufferLock.Release();
            }

            InputGraph.EndComponentInclusiveScope(realTime);
        }

        /// <inheritdoc/>
        public async ValueTask FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // flushing == waiting for current write thread to finish
            await _backgroundTaskFinished.WaitAsync(cancelToken).ConfigureAwait(false);
            _backgroundTaskFinished.Set();
        }

        /// <inheritdoc/>
        public ValueTask<int> ReadAsync(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            throw new NotSupportedException("Cannot read from an async write buffer");
        }

        // Closure class for the background task which writes to the output graph from our local buffer
        private class BackgroundTaskClosure
        {
            private readonly CancellationToken _cancelToken;
            private readonly IRealTimeProvider _realTime;
            private readonly AsyncAudioWriteBuffer _parent;

            public BackgroundTaskClosure(AsyncAudioWriteBuffer parent, CancellationToken threadCancelToken, IRealTimeProvider threadLocalTime)
            {
                _parent = parent;
                _cancelToken = threadCancelToken;
                _realTime = threadLocalTime;
            }

            public async Task Run()
            {
                try
                {
                    BufferSegment segmentToWrite;
                    await _parent._bufferLock.GetLockAsync(_cancelToken, _realTime).ConfigureAwait(false);
                    try
                    {
                        if (_parent._buffers.Count > 0)
                        {
                            segmentToWrite = _parent._buffers.Dequeue();
                        }
                        else
                        {
                            segmentToWrite = null;
                        }
                    }
                    finally
                    {
                        _parent._bufferLock.Release();
                    }

                    // Continue looping this same thread as long as there is data to write
                    while (segmentToWrite != null)
                    {
                        await _parent.OutputGraph.LockGraphAsync(_cancelToken, _realTime).ConfigureAwait(false);
                        try
                        {
                            _parent.OutputGraph.BeginInstrumentedScope(_realTime, _parent.NodeFullName);

                            // Check if the output was disconnected while we were waiting for the output graph lock
                            // If we did get disconnected, this thread will just drain the current buffer to empty void
                            if (_parent.Output != null)
                            {
                                // Write upstream
                                await _parent.Output.WriteAsync(segmentToWrite.Buffer.Buffer, 0, segmentToWrite.SamplesPerChannel, _cancelToken, _realTime).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            _parent.OutputGraph.EndInstrumentedScope(_realTime, _parent._maxBufferLength);
                            _parent.OutputGraph.UnlockGraph();
                        }

                        await _parent._bufferLock.GetLockAsync(_cancelToken, _realTime).ConfigureAwait(false);
                        try
                        {
                            // update the count after writing the previous buffer - have to do this after the actual write to line up properly
                            _parent._samplesPerChannelInBuffer -= segmentToWrite.SamplesPerChannel;
                            segmentToWrite.Buffer.Dispose();
                            segmentToWrite.Buffer = null;
                            _bufferAllocationPool.TryEnqueue(segmentToWrite);

                            if (_parent._buffers.Count > 0)
                            {
                                segmentToWrite = _parent._buffers.Dequeue();
                            }
                            else
                            {
                                segmentToWrite = null;
                            }
                        }
                        finally
                        {
                            _parent._bufferLock.Release();
                        }
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

        private static BufferSegment AllocateBufferSegment(PooledBuffer<float> wrappedBuf, int validSamplesPerChannel)
        {
            BufferSegment returnVal = _bufferAllocationPool.TryDequeue();

            if (returnVal != null)
            {
                returnVal.Buffer = wrappedBuf;
                returnVal.SamplesPerChannel = validSamplesPerChannel;
            }
            else
            {
                returnVal = new BufferSegment()
                {
                    Buffer = wrappedBuf,
                    SamplesPerChannel = validSamplesPerChannel,
                };
            }

            return returnVal;
        }

        private class BufferSegment
        {
            public PooledBuffer<float> Buffer; // Pooled buffer of data
            public int SamplesPerChannel; // Total number of samples in this buffer
        }
    }
}
