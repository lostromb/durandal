using Durandal.Common.Logger;
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
    /// Abstract implementation of <see cref="IAudioSampleTarget"/> which covers boilerplate code for connection, disconnection, and thread safety.
    /// </summary>
    public abstract class AbstractAudioSampleTarget : IAudioSampleTarget
    {
        private readonly string _nodeName;
        private readonly string _nodeFullName;
        private Task _backgroundTask = null;
        private CancellationTokenSource _backgroundTaskCancelizer = null;
        private int _isActiveNode = 0;
        private HashSet<IDisposable> _extraDisposables;
        protected WeakPointer<IAudioGraph> _inputGraph;
        private int _disposed = 0;
        
        public AbstractAudioSampleTarget(WeakPointer<IAudioGraph> graph, string implementingTypeName, string nodeCustomName)
        {
            _inputGraph = graph.AssertNonNull(nameof(graph));
            AudioHelpers.BuildAudioNodeNames(implementingTypeName, nodeCustomName, out _nodeName, out _nodeFullName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AbstractAudioSampleTarget()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc/>
        public IAudioGraph InputGraph => _inputGraph.Value;

        /// <inheritdoc/>
        public IAudioSampleSource Input { get; protected set; }

        /// <inheritdoc/>
        public AudioSampleFormat InputFormat { get; protected set; }

        /// <inheritdoc/>
        public virtual bool IsActiveNode => _isActiveNode != 0;

        /// <inheritdoc/>
        public string NodeName => _nodeName;

        /// <inheritdoc/>
        public string NodeFullName => _nodeFullName;

        /// <inheritdoc/>
        public void ConnectInput(IAudioSampleSource source, bool noRecursiveConnection = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleTarget));
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
                if (Input != null)
                {
                    OnInputConnected();
                }
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

                        if (Input != null)
                        {
                            OnInputConnected();
                        }
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
                bool disconnectHappened = Input != null;
                Input = null;

                if (disconnectHappened)
                {
                    OnInputDisconnected();
                }
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
                        OnInputDisconnected();
                    }
                }
                finally
                {
                    InputGraph.UnlockGraph();
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
                StopActivelyReading();

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
        /// Begins a background thread which will continuously read input from the audio graph and write it to the target.
        /// Do not call this on implementations which already have a thread that drives processing (for example, a microphone device which has an active callback)
        /// </summary>
        /// <param name="logger">A logger to use for the background thread</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="limitToRealTime">If true, the read thread will be limited to real time (as opposed to reading as fast as possible).</param>
        public void BeginActivelyReading(ILogger logger, IRealTimeProvider realTime, bool limitToRealTime = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleTarget));
            }

            if (Interlocked.CompareExchange(ref _isActiveNode, 1, 0) != 0)
            {
                throw new InvalidOperationException("Thread has already been started");
            }
            
            _backgroundTaskCancelizer = new CancellationTokenSource();
            CancellationToken backgroundTaskCancelToken = _backgroundTaskCancelizer.Token;
            IRealTimeProvider backgroundThreadTime = realTime.Fork(nameof(AbstractAudioSampleTarget));
            _backgroundTask = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(
                async () => await RunActiveReadThread(logger, backgroundThreadTime, backgroundTaskCancelToken, limitToRealTime).ConfigureAwait(false));
        }

        public void StopActivelyReading()
        {
            if (Interlocked.CompareExchange(ref _isActiveNode, 0, 1) == 1)
            {
                _backgroundTaskCancelizer?.Cancel();
                _backgroundTaskCancelizer?.Dispose();
                _backgroundTaskCancelizer = null;
            }
        }

        /// <inheritdoc/>
        public ValueTask FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleTarget));
            }

            if (IsActiveNode)
            {
                throw new InvalidOperationException("Cannot flush an active graph node. Generally there should only be one active node per graph. If more than one is required, you should consider putting a push-pull buffer between them.");
            }
            
            return FlushAsyncInternal(cancelToken, realTime);
        }

        /// <inheritdoc/>
        public async ValueTask WriteAsync(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleTarget));
            }

            if (IsActiveNode)
            {
                throw new InvalidOperationException("Cannot write audio samples to an active graph node. Generally there should only be one active node per graph. If more than one is required, you should consider putting a push-pull buffer between them.");
            }

            InputGraph.BeginComponentInclusiveScope(realTime, _nodeFullName);
            try
            {
                await WriteAsyncInternal(buffer, offset, count, cancelToken, realTime);
            }
            finally
            {
                InputGraph.EndComponentInclusiveScope(realTime);
            }
        }

        /// <summary>
        /// Internal implementation of WriteAsync(). The base class provides you with the following guarantees: 1) Read/Write are protected by a mutex
        /// </summary>
        /// <param name="buffer">The buffer to write samples to</param>
        /// <param name="bufferOffset">The array offset when writing to output buffer</param>
        /// <param name="numSamplesPerChannel">The number of samples per channel to write</param>
        /// <param name="cancelToken">A cancellation token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        protected abstract ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Internal implementation of FlushAsync(). The base class provides you with the following guarantees: 1) Read/Write are protected by a mutex
        /// </summary>
        /// <param name="cancelToken">A cancellation token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        protected virtual ValueTask FlushAsyncInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return new ValueTask();
        }

        /// <summary>
        /// Fired internally when an input is connected. The graph is locked while this executes!
        /// </summary>
        protected virtual void OnInputConnected() { }

        /// <summary>
        /// Fired internally when the input is disconnected. The graph is locked while this executes!
        /// </summary>
        protected virtual void OnInputDisconnected() { }

        private async Task RunActiveReadThread(ILogger logger, IRealTimeProvider threadLocalTime, CancellationToken cancelToken, bool limitToRealTime)
        {
            try
            {
                logger.Log("Active audio read thread started", LogLevel.Vrb);
                const int LOOP_LENGTH_MS = 10;
                int bufferSizePerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.SampleRateHz, TimeSpan.FromMilliseconds(LOOP_LENGTH_MS));
                float[] buffer = new float[bufferSizePerChannel * InputFormat.NumChannels];
                bool playbackFinished = false;
                long playbackStartTimeMs = threadLocalTime.TimestampMilliseconds;
                long totalSamplesRead = 0;
                while (!playbackFinished && !cancelToken.IsCancellationRequested)
                {
                    await InputGraph.LockGraphAsync(cancelToken, threadLocalTime).ConfigureAwait(false);
                    InputGraph.BeginInstrumentedScope(threadLocalTime, _nodeFullName);
                    if (Input == null)
                    {
                        InputGraph.EndInstrumentedScope(threadLocalTime, TimeSpan.FromMilliseconds(LOOP_LENGTH_MS));
                        InputGraph.UnlockGraph();
                        await threadLocalTime.WaitAsync(TimeSpan.FromMilliseconds(LOOP_LENGTH_MS), cancelToken).ConfigureAwait(false);
                        totalSamplesRead += bufferSizePerChannel;
                    }
                    else
                    {
                        try
                        {
                            int thisBatchSize = await Input.ReadAsync(buffer, 0, bufferSizePerChannel, cancelToken, threadLocalTime).ConfigureAwait(false);

                            if (thisBatchSize < 0)
                            {
                                playbackFinished = true;
                            }
                            else if (thisBatchSize > 0)
                            {
                                await WriteAsyncInternal(buffer, 0, thisBatchSize, cancelToken, threadLocalTime).ConfigureAwait(false);
                                totalSamplesRead += thisBatchSize;
                            }
                        }
                        finally
                        {
                            InputGraph.EndInstrumentedScope(threadLocalTime, TimeSpan.FromMilliseconds(LOOP_LENGTH_MS));
                            InputGraph.UnlockGraph();
                        }
                    }

                    // Limit this thread's speed to real time if possible. This speed is determined by how long we've been running vs. how much audio we have read in total
                    TimeSpan durationOfAudioRead = AudioMath.ConvertSamplesPerChannelToTimeSpan(InputFormat.SampleRateHz, totalSamplesRead);
                    TimeSpan timeToWait = durationOfAudioRead - TimeSpan.FromMilliseconds(threadLocalTime.TimestampMilliseconds - playbackStartTimeMs);
                    if (limitToRealTime && timeToWait > TimeSpan.Zero)
                    {
                        await threadLocalTime.WaitAsync(timeToWait, cancelToken).ConfigureAwait(false);
                    }
                }

                logger.Log("Active audio read thread finished", LogLevel.Vrb);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                logger.Log(e, LogLevel.Err);
            }
            finally
            {
                _isActiveNode = 0;
                threadLocalTime.Merge();
            }
        }
    }
}
