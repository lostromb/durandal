using Durandal.Common.Audio.WebRtc;
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
    /// A combination of resampler and channel mixer which adapts two audio components with different formats
    /// </summary>
    public sealed class AudioConformer : IAudioSampleSource, IAudioSampleTarget, IAudioDelayingFilter
    {
        private readonly AbstractAudioSampleFilter _firstStage;
        private readonly AbstractAudioSampleFilter _secondStage;
        private readonly WeakPointer<ResamplingFilter> _resampler;
        private readonly string _nodeName;
        private readonly string _nodeFullName;
        private HashSet<IDisposable> _extraDisposables;
        private int _disposed = 0;

        /// <summary>
        /// Creates a new audio conformer.
        /// </summary>
        /// <param name="graph">The audio graph that this component is a part of.</param>
        /// <param name="inputFormat">The format of the input audio</param>
        /// <param name="outputFormat">The format of the output audio</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="logger">A logger</param>
        /// <param name="resamplerQuality">The quality parameter to use for the resampler, from 0 to 10</param>
        public AudioConformer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat inputFormat,
            AudioSampleFormat outputFormat,
            string nodeCustomName,
            ILogger logger,
            AudioProcessingQuality resamplerQuality = AudioProcessingQuality.Balanced)
        {
            graph = graph.AssertNonNull(nameof(graph));
            inputFormat = inputFormat.AssertNonNull(nameof(inputFormat));
            outputFormat = outputFormat.AssertNonNull(nameof(outputFormat));

            // Determine which internal arrangement would require less resampler processing
            int inputSampleRate = inputFormat.SampleRateHz * inputFormat.NumChannels;
            int outputSampleRate = outputFormat.SampleRateHz * outputFormat.NumChannels;
            if (inputSampleRate < outputSampleRate)
            {
                // Resampler comes in first stage
                _resampler = new WeakPointer<ResamplingFilter>(
                    new ResamplingFilter(
                        graph,
                        nodeCustomName,
                        inputFormat.NumChannels,
                        inputFormat.ChannelMapping,
                        inputFormat.SampleRateHz,
                        outputFormat.SampleRateHz,
                        logger,
                        resamplerQuality));
                _firstStage = _resampler.Value;
                _secondStage = new ChannelMixer(graph, outputFormat.SampleRateHz, inputFormat.ChannelMapping, outputFormat.ChannelMapping, nodeCustomName);
            }
            else
            {
                // Resampler comes in second stage
                _resampler = new WeakPointer<ResamplingFilter>(
                    new ResamplingFilter(
                        graph,
                        nodeCustomName,
                        outputFormat.NumChannels,
                        outputFormat.ChannelMapping,
                        inputFormat.SampleRateHz,
                        outputFormat.SampleRateHz,
                        logger,
                        resamplerQuality));
                _firstStage = new ChannelMixer(graph, inputFormat.SampleRateHz, inputFormat.ChannelMapping, outputFormat.ChannelMapping, nodeCustomName);
                _secondStage  = _resampler.Value;
            }

            _firstStage.ConnectOutput(_secondStage);
            AudioHelpers.BuildAudioNodeNames(nameof(AudioConformer), nodeCustomName, out _nodeName, out _nodeFullName);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~AudioConformer()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Gets the algorithmic delay introduced by this conformer (mostly due to resampling)
        /// </summary>
        public TimeSpan AlgorithmicDelay => _resampler.Value.AlgorithmicDelay;

        /// <inheritdoc/>
        public IAudioGraph InputGraph => _firstStage.InputGraph;

        /// <inheritdoc/>
        public IAudioGraph OutputGraph => _secondStage.OutputGraph;

        /// <inheritdoc/>
        public IAudioSampleTarget Output => _secondStage.Output;

        /// <inheritdoc/>
        public AudioSampleFormat OutputFormat => _secondStage.OutputFormat;

        /// <inheritdoc/>
        public IAudioSampleSource Input => _firstStage.Input;

        /// <inheritdoc/>
        public AudioSampleFormat InputFormat => _firstStage.InputFormat;

        /// <inheritdoc/>
        public bool PlaybackFinished => _secondStage.PlaybackFinished;

        /// <inheritdoc/>
        public bool IsActiveNode => false;

        /// <inheritdoc/>
        public string NodeName => _nodeName;

        /// <inheritdoc/>
        public string NodeFullName => _nodeFullName;

        /// <inheritdoc/>
        public void ConnectInput(IAudioSampleSource source, bool noRecursiveConnection = false)
        {
            _firstStage.ConnectInput(source, noRecursiveConnection);
        }

        /// <inheritdoc/>
        public void DisconnectInput(bool noRecursiveConnection = false)
        {
            _firstStage.DisconnectInput(noRecursiveConnection);
        }

        /// <inheritdoc/>
        public void ConnectOutput(IAudioSampleTarget target, bool noRecursiveConnection = false)
        {
            _secondStage.ConnectOutput(target, noRecursiveConnection);
        }

        /// <inheritdoc/>
        public void DisconnectOutput(bool noRecursiveConnection = false)
        {
            _secondStage.DisconnectOutput(noRecursiveConnection);
        }

        /// <inheritdoc/>
        public ValueTask FlushAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _firstStage.FlushAsync(cancelToken, realTime);
        }

        /// <inheritdoc/>
        public ValueTask<int> ReadAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _secondStage.ReadAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
        }

        /// <inheritdoc/>
        public ValueTask WriteAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _firstStage.WriteAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
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

        private void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _firstStage.Dispose();
                _secondStage.Dispose();
                
                if (_extraDisposables != null)
                {
                    foreach (IDisposable b in _extraDisposables)
                    {
                        b?.Dispose();
                    }
                }
            }
        }
    }
}
