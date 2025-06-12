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
    /// Abstract implementation of <see cref="IAudioSampleSource"/> which covers boilerplate code for connection, disconnection, and thread safety.
    /// </summary>
    public abstract class AbstractAudioSampleSource : IAudioSampleSource
    {
        private readonly string _nodeName;
        private readonly string _nodeFullName;
        private Task _backgroundTask = null;
        private CancellationTokenSource _backgroundTaskCancelizer = null;
        private int _isActiveNode = 0;
        private HashSet<IDisposable> _extraDisposables;
        protected WeakPointer<IAudioGraph> _outputGraph;
        private int _disposed = 0;

        public AbstractAudioSampleSource(WeakPointer<IAudioGraph> graph, string implementingTypeName, string nodeCustomName)
        {
            _outputGraph = graph.AssertNonNull(nameof(graph));
            AudioHelpers.BuildAudioNodeNames(implementingTypeName, nodeCustomName, out _nodeName, out _nodeFullName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AbstractAudioSampleSource()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc/>
        public IAudioGraph OutputGraph => _outputGraph.Value;

        /// <inheritdoc/>
        public IAudioSampleTarget Output { get; protected set; }

        /// <inheritdoc/>
        public AudioSampleFormat OutputFormat { get; protected set; }

        /// <inheritdoc/>
        public abstract bool PlaybackFinished { get; }

        /// <inheritdoc/>
        public virtual bool IsActiveNode => _isActiveNode != 0;

        /// <inheritdoc/>
        public string NodeName => _nodeName;

        /// <inheritdoc/>
        public string NodeFullName => _nodeFullName;

        /// <inheritdoc/>
        public void ConnectOutput(IAudioSampleTarget target, bool noRecursiveConnection = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleSource));
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
                bool disconnectHappened = Output != null;
                Output = null;

                if (disconnectHappened)
                {
                    OnOutputDisconnected();
                }
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
                        OnOutputDisconnected();
                    }
                }
                finally
                {
                    OutputGraph.UnlockGraph();
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
                StopActivelyWriting();

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
        /// Begins a background thread which will continuously write output from this source to the audio graph.
        /// Do not call this on implementations which already have a thread that drives processing (for example, a microphone device which has an active callback)
        /// </summary>
        /// <param name="logger">A logger to use for the background thread</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="limitToRealTime">If true, the write thread will be limited to real time (as opposed to writing as fast as possible).</param>
        public void BeginActivelyWriting(ILogger logger, IRealTimeProvider realTime, bool limitToRealTime = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleSource));
            }

            if (Interlocked.CompareExchange(ref _isActiveNode, 1, 0) != 0)
            {
                throw new InvalidOperationException("Thread has already been started");
            }

            _backgroundTaskCancelizer = new CancellationTokenSource();
            CancellationToken backgroundTaskCancelToken = _backgroundTaskCancelizer.Token;
            IRealTimeProvider backgroundThreadTime = realTime.Fork(nameof(AbstractAudioSampleSource));
            _backgroundTask = DurandalTaskExtensions.LongRunningTaskFactory.StartNew(
                async () => await RunActiveWriteThread(logger, backgroundThreadTime, backgroundTaskCancelToken, limitToRealTime).ConfigureAwait(false));
        }

        public void StopActivelyWriting()
        {
            if (Interlocked.CompareExchange(ref _isActiveNode, 0, 1) == 1)
            {
                _backgroundTaskCancelizer?.Cancel();
                _backgroundTaskCancelizer?.Dispose();
                _backgroundTaskCancelizer = null;
            }
        }

        /// <inheritdoc/>
        public async ValueTask<int> ReadAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleSource));
            }

            if (IsActiveNode)
            {
                throw new InvalidOperationException("Cannot read audio samples from an active graph node. Generally there should only be one active node per graph. If more than one is required, you should consider putting a push-pull buffer between them.");
            }

            OutputGraph.BeginComponentInclusiveScope(realTime, _nodeFullName);
            try
            {
                return await ReadAsyncInternal(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime).ConfigureAwait(false);
            }
            finally
            {
                OutputGraph.EndComponentInclusiveScope(realTime);
            }
        }

        /// <summary>
        /// Fired internally when the output is disconnected. The graph is locked while this executes!
        /// </summary>
        protected virtual void OnOutputDisconnected() { }

        /// <summary>
        /// Internal implementation of ReadAsync(). The base class provides you with the following guarantees: 1) Read/Write are protected by a mutex
        /// </summary>
        /// <param name="buffer">The buffer to read samples from</param>
        /// <param name="bufferOffset">The array offset when reading from input buffer</param>
        /// <param name="numSamplesPerChannel">The number of samples per channel to read</param>
        /// <param name="cancelToken">A cancellation token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>The number of samples per channel that were actually read.
        /// This does NOT follow C# stream semantics. A return value of 0 means no samples are currently available but try again later. A return value of -1 indicates end of stream.
        /// The implementation should avoid blocking until samples to become available, opting instead to return 0 and return control to the caller (this is to prevent stutters from long blocking calls)</returns>
        protected abstract ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime);
        
        private async Task RunActiveWriteThread(ILogger logger, IRealTimeProvider threadLocalTime, CancellationToken cancelToken, bool limitToRealTime)
        {
            const int LOOP_LENGTH_MS = 10;

            try
            {
                logger.Log("Active write thread started", LogLevel.Vrb);
                int bufferSizePerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(OutputFormat.SampleRateHz, TimeSpan.FromMilliseconds(LOOP_LENGTH_MS));
                float[] buffer = new float[bufferSizePerChannel * OutputFormat.NumChannels];
                bool playbackFinished = false;
                long playbackStartTimeMs = threadLocalTime.TimestampMilliseconds;
                long totalSamplesWritten = 0;
                while (!playbackFinished && !cancelToken.IsCancellationRequested)
                {
                    await OutputGraph.LockGraphAsync(cancelToken, threadLocalTime).ConfigureAwait(false);
                    OutputGraph.BeginInstrumentedScope(threadLocalTime, NodeFullName);
                    if (Output == null)
                    {
                        OutputGraph.EndInstrumentedScope(threadLocalTime, TimeSpan.FromMilliseconds(LOOP_LENGTH_MS));
                        OutputGraph.UnlockGraph();
                        await threadLocalTime.WaitAsync(TimeSpan.FromMilliseconds(LOOP_LENGTH_MS), cancelToken).ConfigureAwait(false);
                        totalSamplesWritten += bufferSizePerChannel;
                    }
                    else
                    {
                        try
                        {
                            int thisBatchSize = await ReadAsyncInternal(buffer, 0, bufferSizePerChannel, cancelToken, threadLocalTime).ConfigureAwait(false);

                            if (thisBatchSize < 0)
                            {
                                // End of stream
                                await Output.FlushAsync(cancelToken, threadLocalTime).ConfigureAwait(false);
                                break;
                            }
                            else if (thisBatchSize > 0)
                            {
                                await Output.WriteAsync(buffer, 0, thisBatchSize, cancelToken, threadLocalTime).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            OutputGraph.EndInstrumentedScope(threadLocalTime, TimeSpan.FromMilliseconds(LOOP_LENGTH_MS));
                            OutputGraph.UnlockGraph();
                        }
                    }

                    // Limit this thread's speed to real time if desired. This speed is determined by how long we've been running vs. how much of the sample we should have output
                    totalSamplesWritten += bufferSizePerChannel;
                    TimeSpan durationOfAudioWritten = AudioMath.ConvertSamplesPerChannelToTimeSpan(OutputFormat.SampleRateHz, totalSamplesWritten);
                    TimeSpan timeToWait = durationOfAudioWritten - TimeSpan.FromMilliseconds(threadLocalTime.TimestampMilliseconds - playbackStartTimeMs);
                    if (limitToRealTime && timeToWait > TimeSpan.Zero)
                    {
                        await threadLocalTime.WaitAsync(timeToWait, cancelToken).ConfigureAwait(false);
                    }
                }

                logger.Log("Active write thread finished", LogLevel.Vrb);
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
