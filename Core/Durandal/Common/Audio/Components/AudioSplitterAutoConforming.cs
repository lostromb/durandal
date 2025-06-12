using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
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
    /// The same functionality as an <see cref="AudioSplitter"/>, except that if the output sample format doesn't match,
    /// it will be conformed automatically to the input format, which means you won't have to worry about manually conforming all of your streams.
    /// </summary>
    public sealed class AudioSplitterAutoConforming : IAudioSampleTarget
    {
        private readonly AudioSplitter _firstStageSplitter;
        private readonly FastConcurrentDictionary<AudioSampleFormat, Tuple<AudioConformer, AudioSplitter>> _secondStageSplitters;
        private readonly AudioProcessingQuality _resamplerQuality;
        private readonly string _nodeName;
        private readonly string _nodeFullName;
        private readonly ILogger _logger;
        private readonly WeakPointer<IAudioGraph> _inputGraph;
        private int _disposed = 0;

        public AudioSplitterAutoConforming(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat inputFormat,
            string nodeCustomName,
            ILogger logger,
            AudioProcessingQuality resamplerQuality = AudioProcessingQuality.Balanced)
        {
            _inputGraph = graph.AssertNonNull(nameof(graph));
            _resamplerQuality = resamplerQuality;
            _logger = logger.AssertNonNull(nameof(logger));
            _firstStageSplitter = new AudioSplitter(graph, inputFormat, nodeCustomName);
            _secondStageSplitters = new FastConcurrentDictionary<AudioSampleFormat, Tuple<AudioConformer, AudioSplitter>>();
            AudioHelpers.BuildAudioNodeNames(nameof(AudioSplitterAutoConforming), nodeCustomName, out _nodeName, out _nodeFullName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AudioSplitterAutoConforming()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc/>
        public IAudioGraph InputGraph => _inputGraph.Value;

        /// <inheritdoc/>
        public IAudioSampleSource Input => _firstStageSplitter.Input;

        /// <inheritdoc/>
        public AudioSampleFormat InputFormat => _firstStageSplitter.InputFormat;

        /// <inheritdoc/>
        public bool IsActiveNode => _firstStageSplitter.IsActiveNode;

        /// <inheritdoc/>
        public string NodeName => _nodeName;

        /// <inheritdoc/>
        public string NodeFullName => _nodeFullName;

        /// <summary>
        /// Connects a component to the output of the splitter. The format of the target can
        /// be whatever you want; resampling and channel mixing will be done automatically
        /// and in a way which minimizes processing overhead.
        /// You can add as many outputs as you like. Please disconnect the output when you are done using it.
        /// Returned object lifetime is managed by the splitter class, just make sure you disconnect.
        /// <para>
        /// Important!!!! Make sure that no single audio graph component has multiple routes to the same splitter
        /// (in other words, that the same thing is not connected multiple times). It will cause very strange
        /// bugs as the splitter tries to write to all of its outputs in parallel.
        /// </para>
        /// </summary>
        public void AddOutput(IAudioSampleTarget target)
        {
            Tuple<AudioConformer, AudioSplitter> outputPair;
            _secondStageSplitters.TryGetValueOrSet(target.InputFormat, out outputPair, () =>
            {
                AudioConformer conformer = new AudioConformer(_inputGraph, InputFormat, target.InputFormat, nodeCustomName: null, logger: _logger, _resamplerQuality);
                AudioSplitter secondStageSplitter = new AudioSplitter(_inputGraph, target.InputFormat, nodeCustomName: _nodeName);
                _firstStageSplitter.AddOutput(conformer);
                conformer.ConnectOutput(secondStageSplitter);
                return new Tuple<AudioConformer, AudioSplitter>(conformer, secondStageSplitter);
            });

            outputPair.Item2.AddOutput(target);
        }

        /// <inheritdoc/>
        public void ConnectInput(IAudioSampleSource source, bool noRecursiveConnection = false)
        {
            _firstStageSplitter.ConnectInput(source, noRecursiveConnection);
        }

        /// <inheritdoc/>
        public void DisconnectInput(bool noRecursiveConnection = false)
        {
            _firstStageSplitter.DisconnectInput(noRecursiveConnection);
        }

        /// <inheritdoc/>
        public ValueTask FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _firstStageSplitter.FlushAsync(cancelToken, realTime);
        }

        /// <inheritdoc/>
        public ValueTask WriteAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _firstStageSplitter.WriteAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return _nodeFullName;
        }

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
                _firstStageSplitter?.Dispose();
                foreach (var secondStageSplitter in _secondStageSplitters)
                {
                    secondStageSplitter.Value?.Item1?.Dispose();
                    secondStageSplitter.Value?.Item2?.Dispose();
                }
            }
        }
    }
}
