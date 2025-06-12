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

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Implements a buffer which acts as a boundary between two active nodes (a push and a pull) in an audio graph.
    /// This comes at the expense of buffering latency and the potential for over/underflow, but it allows graphs to have
    /// more than one active node when it would otherwise be impossible.
    /// For example, a microphone could be actively pushing samples and a speaker could be actively reading them.
    /// Putting this buffer in between will reconcile the different rates / sizes required by each end of the graph.
    /// This class is quite similar to <see cref="AsyncAudioReadBuffer"/>. The difference is that this component
    /// will not trigger any reads across the "air gap" - it can only read what samples have already been pushed from the input.
    /// </summary>
    public sealed class PushPullBuffer : IAudioSampleSource, IAudioSampleTarget, IAudioDelayingFilter
    {
        private static readonly LockFreeCache<BufferSegment> _bufferAllocationPool = new LockFreeCache<BufferSegment>(128); // Reuse old buffer segments to avoid allocations

        private readonly AsyncLockSlim _thisComponentLock = new AsyncLockSlim();
        private readonly int? _maximumBufferSizeSamplesPerChannel; // Total max amount of samples to buffer
        private readonly Queue<BufferSegment> _buffers = new Queue<BufferSegment>(); // The buffer itself
        private readonly IMetricCollector _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly string _nodeName;
        private readonly string _nodeFullName;
        private readonly WeakPointer<IAudioGraph> _inputGraph;
        private readonly WeakPointer<IAudioGraph> _outputGraph;
        private bool _playbackFinished = false;
        private int _samplesPerChannelInBuffer = 0; // Number of samples per channel currently buffered
        private int _disposed = 0;

        /// <summary>
        /// Creates a new <see cref="PushPullBuffer"/>.
        /// </summary>
        /// <param name="inputGraph">The input audio graph.</param>
        /// <param name="outputGraph"></param>
        /// <param name="format"></param>
        /// <param name="nodeCustomName"></param>
        /// <param name="maxBufferLength"></param>
        /// <param name="metrics"></param>
        /// <param name="metricDimensions"></param>
        public PushPullBuffer(
            WeakPointer<IAudioGraph> inputGraph,
            WeakPointer<IAudioGraph> outputGraph,
            AudioSampleFormat format,
            string nodeCustomName,
            TimeSpan? maxBufferLength = null,
            IMetricCollector metrics = null,
            DimensionSet metricDimensions = null)
        {
            _inputGraph = inputGraph.AssertNonNull(nameof(inputGraph));
            _outputGraph = outputGraph.AssertNonNull(nameof(outputGraph));

            _metrics = metrics ?? NullMetricCollector.Singleton;
            _metricDimensions = metricDimensions ?? DimensionSet.Empty;
            InputFormat = format.AssertNonNull(nameof(format));
            OutputFormat = format;
            if (maxBufferLength.HasValue)
            {
                _maximumBufferSizeSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, maxBufferLength.Value);
            }

            AudioHelpers.BuildAudioNodeNames(nameof(PushPullBuffer), nodeCustomName, out _nodeName, out _nodeFullName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~PushPullBuffer()
        {
            Dispose(false);
        }
#endif

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
                _thisComponentLock.GetLock();
                try
                {
                    return _playbackFinished;
                }
                finally
                {
                    _thisComponentLock.Release();
                }
            }
        }

        public TimeSpan AlgorithmicDelay => AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, AvailableSamples);

        /// <summary>
        /// Gets the amount of buffered audio currently available, in samples per channel
        /// </summary>
        public int AvailableSamples
        {
            get
            {
                _thisComponentLock.GetLock();
                try
                {
                    return _samplesPerChannelInBuffer;
                }
                finally
                {
                    _thisComponentLock.Release();
                }
            }
        }

        /// <summary>
        /// Removes all buffered samples.
        /// This will drop data but can lower long-term latency if there was a period where the buffer got too full.
        /// </summary>
        public void ClearBuffer()
        {
            _thisComponentLock.GetLock();
            try
            {
                _buffers.Clear();
                _samplesPerChannelInBuffer = 0;
            }
            finally
            {
                _thisComponentLock.Release();
            }
        }

        /// <inheritdoc/>
        public void ConnectOutput(IAudioSampleTarget target, bool noRecursiveConnection = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PushPullBuffer));
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
                _thisComponentLock.GetLock();
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
                    _thisComponentLock.Release();
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
                _thisComponentLock.GetLock();
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
                    _thisComponentLock.Release();
                    OutputGraph.UnlockGraph();
                }
            }
        }

        /// <inheritdoc/>
        public void ConnectInput(IAudioSampleSource source, bool noRecursiveConnection = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PushPullBuffer));
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
                _thisComponentLock.GetLock();
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
                    _thisComponentLock.Release();
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
                _thisComponentLock.GetLock();
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
                    _thisComponentLock.Release();
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

                _thisComponentLock?.Dispose();
            }
        }

        /// <inheritdoc/>
        public ValueTask FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PushPullBuffer));
            }

            return new ValueTask();
        }

        /// <inheritdoc/>
        public async ValueTask<int> ReadAsync(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PushPullBuffer));
            }

            if (_playbackFinished)
            {
                return -1;
            }

            OutputGraph.BeginComponentInclusiveScope(realTime, _nodeFullName);
            await _thisComponentLock.GetLockAsync(/*cancelToken, realTime*/).ConfigureAwait(false); // using realTime here actually harms unit tests, so don't bother
            try
            {
                if (Input == null)
                {
                    return 0;
                }
                else
                {
                    return ReadAsyncInternal(buffer, offset, count, cancelToken, realTime);
                }
            }
            finally
            {
                _thisComponentLock.Release();
                OutputGraph.EndComponentInclusiveScope(realTime);
            }
        }

        /// <inheritdoc/>
        public async ValueTask WriteAsync(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(PushPullBuffer));
            }

            InputGraph.BeginComponentInclusiveScope(realTime, _nodeFullName);
            await _thisComponentLock.GetLockAsync(/*cancelToken, realTime*/).ConfigureAwait(false); // using realTime here actually harms unit tests, so don't bother
            try
            {
                if (Output != null)
                {
                    WriteAsyncInternal(buffer, offset, count, cancelToken, realTime);
                }
            }
            finally
            {
                _thisComponentLock.Release();
                InputGraph.EndComponentInclusiveScope(realTime);
            }
        }

        private int ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_samplesPerChannelInBuffer == 0)
            {
                if (Input.PlaybackFinished)
                {
                    // Playback has finished entirely.
                    _playbackFinished = true;
                    return -1;
                }

                // Buffer underrun but input has not finished playback yet. Return zero samples available
                _metrics.ReportInstant(CommonInstrumentation.Key_Counter_PushPullBuffer_Underflow_Samples, _metricDimensions, count);
                return 0;
            }

            int amountReadPerChannel = 0;
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

        private void WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            PooledBuffer<float> buf = BufferPool<float>.Rent(count * InputFormat.NumChannels);
            ArrayExtensions.MemCopy(buffer, offset, buf.Buffer, 0, count * InputFormat.NumChannels);
            BufferSegment newSegment = _bufferAllocationPool.TryDequeue();

            if (newSegment != null)
            {
                newSegment.Buffer = buf;
                newSegment.SamplesPerChannel = count;
                newSegment.AmountReadPerChannel = 0;
            }
            else
            {
                newSegment = new BufferSegment()
                {
                    Buffer = buf,
                    SamplesPerChannel = count,
                    AmountReadPerChannel = 0
                };
            }

            _buffers.Enqueue(newSegment);

            _samplesPerChannelInBuffer += count;

            // Prune any samples in buffer that exceed the maximum buffer size (if configured)
            if (_maximumBufferSizeSamplesPerChannel.HasValue && _samplesPerChannelInBuffer > _maximumBufferSizeSamplesPerChannel.Value)
            {
                _metrics.ReportInstant(CommonInstrumentation.Key_Counter_PushPullBuffer_Overflow_Samples, _metricDimensions, _samplesPerChannelInBuffer - _maximumBufferSizeSamplesPerChannel.Value);

                while (_samplesPerChannelInBuffer > _maximumBufferSizeSamplesPerChannel.Value)
                {
                    BufferSegment nextSegment = _buffers.Peek();
                    int amountStillNeededToPrune = _samplesPerChannelInBuffer - _maximumBufferSizeSamplesPerChannel.Value;
                    int amountCanPruneFromNextSegment = nextSegment.SamplesPerChannel - nextSegment.AmountReadPerChannel;
                    int amountToPruneFromNextSegment = FastMath.Min(amountStillNeededToPrune, amountCanPruneFromNextSegment);
                    nextSegment.AmountReadPerChannel += amountToPruneFromNextSegment;
                    if (nextSegment.AmountReadPerChannel >= nextSegment.SamplesPerChannel)
                    {
                        nextSegment.Buffer.Dispose();
                        nextSegment.Buffer = null;
                        _bufferAllocationPool.TryEnqueue(nextSegment);
                        _buffers.Dequeue();
                    }

                    _samplesPerChannelInBuffer -= amountToPruneFromNextSegment;
                }
            }
        }
        
        private class BufferSegment
        {
            public PooledBuffer<float> Buffer;
            public int SamplesPerChannel;
            public int AmountReadPerChannel;
        }
    }
}
