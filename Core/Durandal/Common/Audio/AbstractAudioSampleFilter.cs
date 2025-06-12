using Durandal.Common.Collections;
using Durandal.Common.IO;
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
    /// Abstract base class for an audio graph filter, which is a component with one input and one output.
    /// </summary>
    public abstract class AbstractAudioSampleFilter : IAudioSampleSource, IAudioSampleTarget
    {
        protected readonly WeakPointer<IAudioGraph> _graph;
        private readonly string _nodeName;
        private readonly string _nodeFullName;
        protected bool _playbackFinished = false;
        private HashSet<IDisposable> _extraDisposables;
        private int _disposed = 0;

        public AbstractAudioSampleFilter(WeakPointer<IAudioGraph> graph, string implementingTypeName, string nodeCustomName)
        {
            _graph = graph.AssertNonNull(nameof(graph));
            AudioHelpers.BuildAudioNodeNames(implementingTypeName, nodeCustomName, out _nodeName, out _nodeFullName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AbstractAudioSampleFilter()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc/>
        public IAudioGraph InputGraph => _graph.Value;

        /// <inheritdoc/>
        public IAudioGraph OutputGraph => _graph.Value;

        /// <inheritdoc/>
        public IAudioSampleTarget Output { get; protected set; }

        /// <inheritdoc/>
        public AudioSampleFormat OutputFormat { get; protected set; }

        /// <inheritdoc/>
        public IAudioSampleSource Input { get; protected set; }

        /// <inheritdoc/>
        public AudioSampleFormat InputFormat { get; protected set; }

        /// <inheritdoc/>
        public virtual bool PlaybackFinished => _playbackFinished;

        /// <inheritdoc/>
        public virtual bool IsActiveNode => false;

        /// <inheritdoc/>
        public string NodeName => _nodeName;

        /// <inheritdoc/>
        public string NodeFullName => _nodeFullName;

        /// <inheritdoc/>
        public void ConnectOutput(IAudioSampleTarget target, bool noRecursiveConnection = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleFilter));
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
        public void ConnectInput(IAudioSampleSource source, bool noRecursiveConnection = false)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleFilter));
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
                _graph.Value.LockGraph();
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
        public async ValueTask FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleFilter));
            }

            if (IsActiveNode)
            {
                throw new InvalidOperationException("Cannot flush an active graph node. Generally there should only be one active node per graph. If more than one is required, you should consider putting a push-pull buffer between them.");
            }

            await FlushAsyncInternal(cancelToken, realTime).ConfigureAwait(false);
            if (Output != null)
            {
                await Output.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async ValueTask<int> ReadAsync(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleFilter));
            }

            if (PlaybackFinished)
            {
                return -1;
            }

            if (IsActiveNode)
            {
                throw new InvalidOperationException("Cannot read audio samples from an active graph node. Generally there should only be one active node per graph. If more than one is required, you should consider putting a push-pull buffer between them.");
            }

            _graph.Value.BeginComponentInclusiveScope(realTime, _nodeFullName);
            try
            {
                if (Input == null)
                {
                    return 0;
                }
                else
                {
                    int returnVal = await ReadAsyncInternal(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);
                    if (returnVal < 0)
                    {
                        _playbackFinished = true;
                    }

                    return returnVal;
                }
            }
            finally
            {
                _graph.Value.EndComponentInclusiveScope(realTime);
            }
        }

        /// <inheritdoc/>
        public async ValueTask WriteAsync(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(AbstractAudioSampleFilter));
            }

            if (IsActiveNode)
            {
                throw new InvalidOperationException("Cannot write audio samples to an active graph node. Generally there should only be one active node per graph. If more than one is required, you should consider putting a push-pull buffer between them.");
            }

            _graph.Value.BeginComponentInclusiveScope(realTime, _nodeFullName);
            try
            {
                if (Output != null)
                {
                    await WriteAsyncInternal(buffer, offset, count, cancelToken, realTime).ConfigureAwait(false);

                    if (Input != null)
                    {
                        _playbackFinished = _playbackFinished || Input.PlaybackFinished;
                    }
                }
            }
            finally
            {
                _graph.Value.EndComponentInclusiveScope(realTime);
            }
        }

        /// <summary>
        /// Internal implementation of ReadAsync(). The base class provides you with the following guarantees: 1) Read/Write are protected by a mutex 2) Input is non-null and constant.
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

        /// <summary>
        /// Internal implementation of WriteAsync(). The base class provides you with the following guarantees: 1) Read/Write are protected by a mutex 2) Output is non-null and constant.
        /// </summary>
        /// <param name="buffer">The buffer to write samples to</param>
        /// <param name="bufferOffset">The array offset when writing to output buffer</param>
        /// <param name="numSamplesPerChannel">The number of samples per channel to write</param>
        /// <param name="cancelToken">A cancellation token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        protected abstract ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime);

        /// <summary>
        /// Internal implementation of FlushAsync(). The base class provides you with the following guarantees: 1) Read/Write are protected by a mutex 2) Output is non-null and constant.
        /// You do NOT need to flush downstream components in this implementation! That is handled for you!
        /// </summary>
        /// <param name="cancelToken">A cancellation token for the operation</param>
        /// <param name="realTime">A definition of real time</param>
        /// <returns>An async task</returns>
        protected virtual ValueTask FlushAsyncInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return new ValueTask();
        }
    }
}
