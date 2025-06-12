using Durandal.Common.Audio.Components.Noise;
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
    public sealed class FeedbackSimulator : IAudioSampleSource, IAudioSampleTarget
    {
        private readonly WeakPointer<IAudioGraph> _graph;
        private readonly NoiseSampleSource _noise;
        private readonly BiquadFilter _bandFilter;
        private readonly BiquadFilter _bandFilter2;
        private readonly AudioDelayBuffer _delay;
        private readonly ReverbFilter _reverb;
        private readonly VolumeFilter _volume;
        private readonly SilencePaddingFilter _underrunProtection;
        private readonly LinearMixer _noiseMixer;
        private readonly string _nodeName;
        private readonly string _nodeFullName;
        private int _disposed = 0;

        public FeedbackSimulator(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, TimeSpan delay)
        {
            _graph = graph.AssertNonNull(nameof(graph));
            _noiseMixer = new LinearMixer(graph, format, nodeCustomName, false);
            _underrunProtection = new SilencePaddingFilter(graph, format, nodeCustomName);
            _noise = new NoiseSampleSource(graph, format, new WhiteNoiseGenerator(format, maxAmplitude: 0.02f), nodeCustomName);
            _bandFilter = new HighShelfFilter(graph, format, nodeCustomName, 8000f, -20.0f);
            _bandFilter2 = new PeakFilter(graph, format, nodeCustomName, 1000f, -20.0f, 10);
            _reverb = new ReverbFilter(graph, format, nodeCustomName, TimeSpan.FromMilliseconds(5), 0.8f, 0.7f);
            _delay = new AudioDelayBuffer(graph, format, nodeCustomName, delay);
            _volume = new VolumeFilter(graph, format, nodeCustomName);
            _volume.VolumeDecibels = -20.0f;

            _delay.ConnectOutput(_volume);
            _volume.ConnectOutput(_reverb);
            _noiseMixer.AddInput(_reverb);
            _noiseMixer.AddInput(_noise);
            _noiseMixer.ConnectOutput(_bandFilter);
            _bandFilter.ConnectOutput(_bandFilter2);
            _bandFilter2.ConnectOutput(_underrunProtection);
            AudioHelpers.BuildAudioNodeNames(nameof(FeedbackSimulator), nodeCustomName, out _nodeName, out _nodeFullName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        // References to whatever components above serve as the inputs / output to the entire filter
        // They're only here to make it easier in case the code changes
        private IAudioSampleTarget _inputComponent => _delay;
        private IAudioSampleSource _outputComponent => _underrunProtection;

        /// <summary>
        /// Gets or sets the amount of feedback to allow relative to the input volume.
        /// </summary>
        public float FeedbackGainDb
        {
            get
            {
                return _volume.VolumeDecibels;
            }
            set
            {
                _volume.VolumeDecibels = value;
            }
        }

        /// <inheritdoc/>
        public IAudioGraph InputGraph => _graph.Value;

        /// <inheritdoc/>
        public IAudioGraph OutputGraph => _graph.Value;

        /// <inheritdoc/>
        public IAudioSampleTarget Output => _outputComponent.Output;

        /// <inheritdoc/>
        public AudioSampleFormat OutputFormat => _outputComponent.OutputFormat;

        /// <inheritdoc/>
        public IAudioSampleSource Input => _inputComponent.Input;

        /// <inheritdoc/>
        public AudioSampleFormat InputFormat => _inputComponent.InputFormat;

        /// <inheritdoc/>
        public bool PlaybackFinished => _outputComponent.PlaybackFinished;

        /// <inheritdoc/>
        public bool IsActiveNode => false;

        /// <inheritdoc/>
        public string NodeName => _nodeName;

        /// <inheritdoc/>
        public string NodeFullName => _nodeFullName;

        /// <inheritdoc/>
        public void ConnectInput(IAudioSampleSource source, bool noRecursiveConnection = false)
        {
            _inputComponent.ConnectInput(source, noRecursiveConnection);
        }

        /// <inheritdoc/>
        public void ConnectOutput(IAudioSampleTarget target, bool noRecursiveConnection = false)
        {
            _outputComponent.ConnectOutput(target, noRecursiveConnection);
        }

        /// <inheritdoc/>
        public void DisconnectInput(bool noRecursiveConnection = false)
        {
            _inputComponent.DisconnectInput(noRecursiveConnection);
        }

        /// <inheritdoc/>
        public void DisconnectOutput(bool noRecursiveConnection = false)
        {
            _outputComponent.DisconnectOutput(noRecursiveConnection);
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

        /// <inheritdoc/>
        public ValueTask FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _inputComponent.FlushAsync(cancelToken, realTime);
        }

        /// <inheritdoc/>
        public ValueTask<int> ReadAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _outputComponent.ReadAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
        }

        /// <inheritdoc/>
        public ValueTask WriteAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _inputComponent.WriteAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
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
                _noise?.Dispose();
                _bandFilter?.Dispose();
                _bandFilter2?.Dispose();
                _delay?.Dispose();
                _reverb?.Dispose();
                _volume?.Dispose();
                _noiseMixer?.Dispose();
            }
        }
    }
}