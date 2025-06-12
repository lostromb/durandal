using Durandal.Common.Collections;
using Durandal.Common.Events;
using Durandal.Common.Instrumentation;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
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
    /// The same functionality as an <see cref="LinearMixer"/>, except that if the input sample format doesn't match,
    /// it will be conformed automatically to the output format, which means you won't have to worry about manually conforming all of your streams.
    /// </summary>
    public sealed class LinearMixerAutoConforming : IAudioSampleSource
    {
        private readonly LinearMixer _secondStageMixer;
        private readonly FastConcurrentDictionary<AudioSampleFormat, Tuple<LinearMixer, AudioConformer>> _firstStageMixers;
        private readonly bool _readForever;
        private readonly ILogger _logger;
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly AudioProcessingQuality _resamplerQuality;
        private readonly string _nodeName;
        private readonly string _nodeFullName;
        private readonly Guid _uniqueId = Guid.NewGuid();
        private readonly WeakPointer<IAudioGraph> _outputGraph;
        private int _disposed = 0;

        public LinearMixerAutoConforming(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat outputFormat,
            string nodeCustomName,
            bool readForever = true,
            ILogger logger = null,
            WeakPointer<IMetricCollector> metrics = default(WeakPointer<IMetricCollector>),
            DimensionSet dimensions = null,
            AudioProcessingQuality resamplerQuality = AudioProcessingQuality.Balanced)
        {
            _readForever = readForever;
            _logger = logger ?? NullLogger.Singleton;
            _metrics = metrics;
            _metricDimensions = dimensions;
            _resamplerQuality = resamplerQuality;
            _secondStageMixer = new LinearMixer(graph, outputFormat, nodeCustomName, _readForever, _logger, _metrics, _metricDimensions);
            _firstStageMixers = new FastConcurrentDictionary<AudioSampleFormat, Tuple<LinearMixer, AudioConformer>>();
            _outputGraph = graph;
            AudioHelpers.BuildAudioNodeNames(nameof(LinearMixerAutoConforming), nodeCustomName, out _nodeName, out _nodeFullName);
            ChannelFinishedEvent = new AsyncEvent<PlaybackFinishedEventArgs>();
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~LinearMixerAutoConforming()
        {
            Dispose(false);
        }
#endif

        /// <inheritdoc/>
        public Guid NodeId => _uniqueId;

        public IAudioGraph OutputGraph => _outputGraph.Value;

        /// <inheritdoc/>
        public IAudioSampleTarget Output => _secondStageMixer.Output;

        /// <inheritdoc/>
        public AudioSampleFormat OutputFormat => _secondStageMixer.OutputFormat;

        /// <inheritdoc/>
        public bool PlaybackFinished => _secondStageMixer.PlaybackFinished;

        /// <inheritdoc/>
        public bool IsActiveNode => _secondStageMixer.IsActiveNode;

        /// <inheritdoc/>
        public string NodeName => _nodeName;

        /// <inheritdoc/>
        public string NodeFullName => _nodeFullName;

        /// <summary>
        /// Event which fires whenever a mixer input has finished playback.
        /// </summary>
        public AsyncEvent<PlaybackFinishedEventArgs> ChannelFinishedEvent { get; private set; }

        /// <summary>
        /// Connects a sample source to this mixer. If the sample format does not match, it
        /// will be automatically conformed to this mixer's output format.
        /// <para>
        /// Important!!!! Make sure that no single audio graph component has multiple routes to the same mixer
        /// (in other words, that the same thing is not connected multiple times). It will cause very strange
        /// bugs as the mixer tries to read from all of its inputs in parallel.
        /// </para>
        /// </summary>
        /// <param name="source">The sample source to add</param>
        /// <param name="channelToken">An optional channel token to identify this source.
        /// The presence of this token will cause an event to be raised when the channel finishes, and the token will be passed in that eventargs</param>
        /// <param name="takeOwnership">If true, the mixer will take ownership of the input and will dispose of it when its playback has finished.</param>
        /// <returns></returns>
        public void AddInput(IAudioSampleSource source, object channelToken = null, bool takeOwnership = false)
        {
            Tuple<LinearMixer, AudioConformer> inputPair;
            _firstStageMixers.TryGetValueOrSet(source.OutputFormat, out inputPair, () =>
            {
                LinearMixer firstStageMixer = new LinearMixer(_outputGraph, source.OutputFormat, _nodeName, _readForever, _logger, _metrics, _metricDimensions);
                AudioConformer conformer = new AudioConformer(_outputGraph, source.OutputFormat, OutputFormat, _nodeName, _logger, _resamplerQuality);
                firstStageMixer.ConnectOutput(conformer);
                _secondStageMixer.AddInput(conformer);
                firstStageMixer.ChannelFinishedEvent.Subscribe(ChannelFinishedEvent.Fire);
                return new Tuple<LinearMixer, AudioConformer>(firstStageMixer, conformer);
            });

            inputPair.Item1.AddInput(source, channelToken, takeOwnership);
        }

        /// <summary>
        /// Removes all inputs from this mixer and disposes of them if necessary
        /// </summary>
        public void DisconnectAllInputs()
        {
            foreach (var mixer in _firstStageMixers)
            {
                mixer.Value.Item1.DisconnectAllInputs();
            }
        }

        /// <inheritdoc/>
        public void ConnectOutput(IAudioSampleTarget target, bool noRecursiveConnection = false)
        {
            _secondStageMixer.ConnectOutput(target, noRecursiveConnection);
        }

        /// <inheritdoc/>
        public void DisconnectOutput(bool noRecursiveConnection = false)
        {
            _secondStageMixer.DisconnectOutput(noRecursiveConnection);
        }

        /// <inheritdoc/>
        public ValueTask<int> ReadAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _secondStageMixer.ReadAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
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
                _secondStageMixer?.Dispose();
                foreach (var firstStageMixer in _firstStageMixers)
                {
                    firstStageMixer.Value?.Item1?.Dispose();
                    firstStageMixer.Value?.Item2?.Dispose();
                }
            }
        }
    }
}
