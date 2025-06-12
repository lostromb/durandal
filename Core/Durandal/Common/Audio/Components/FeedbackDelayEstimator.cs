using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Statistics;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    public sealed class FeedbackDelayEstimator : IDisposable
    {
        private static readonly TimeSpan RECTIFIER_BLOCK_SIZE = TimeSpan.FromMilliseconds(10);

        // Larger values = more confident correlations that take longer to calculate
        private static readonly TimeSpan PATTERN_MATCH_LENGTH = TimeSpan.FromMilliseconds(200);

        // Maximum amount of delay that can be calculated by this estimator
        private static readonly TimeSpan MAX_DELAY = TimeSpan.FromMilliseconds(200);

        // Extra amount of space that we allocate in the peek buffer just as overscan area
        private static readonly TimeSpan PEEK_BUFFER_EXTRA = TimeSpan.FromMilliseconds(500);

        // Format of the aligned output stream
        private readonly AudioSampleFormat _outputFormat;

        // Speaker-side graph components
        private readonly WeakPointer<IAudioSampleSource> _speakerOutput;
        private readonly WeakPointer<IAudioSampleTarget> _speakerInput;
        private readonly AudioSplitter _speakerSplitter;
        private readonly AudioConformer _speakerPreConformer;
        private readonly AudioConformer _speakerHardwareConformer;
        private readonly AudioConformer _speakerToPeekBufferConformer;
        private readonly AudioPeekBuffer _speakerPeekbuffer;
        private readonly AudioBlockRectifier _speakerOutputRectifier;
        private readonly AudioFloatingDelayBuffer _speakerFeedbackFloatingDelay;
        private readonly PushPullBuffer _speakerPushPull;

        // Microphone-side graph components
        private readonly AudioConformer _micInputConformer;
        private readonly AudioBlockRectifier _micInputRectifier;
        private readonly DelayEstimatorEventDriver _eventDriver;
        private readonly FeedbackDelayEstimatorCore _delayEstimator;
        private readonly ChannelFaninMixer _alignedOutputMixer;

        private int _disposed = 0;

        public FeedbackDelayEstimator(
            WeakPointer<IAudioGraph> microphoneGraph,
            WeakPointer<IAudioGraph> speakerGraph,
            AudioSampleFormat microphoneFormat,
            AudioSampleFormat speakerInputFormat,
            AudioSampleFormat speakerHardwareFormat,
            int processedOutputSampleRate,
            string nodeCustomName = "Feedback",
            ILogger debugLogger = null)
        {
            _outputFormat = AudioSampleFormat.Mono(processedOutputSampleRate);
            nodeCustomName = nodeCustomName ?? "Feedback";
            debugLogger = debugLogger ?? NullLogger.Singleton;

            // Create components on speaker side
            _speakerPeekbuffer = new AudioPeekBuffer(speakerGraph, _outputFormat, nodeCustomName + "SpeakerPeekbuffer", MAX_DELAY + PEEK_BUFFER_EXTRA);
            _speakerFeedbackFloatingDelay = new AudioFloatingDelayBuffer(speakerGraph, _outputFormat, nodeCustomName + "FloatingDelay", TimeSpan.Zero, MAX_DELAY);
            _speakerOutputRectifier = new AudioBlockRectifier(speakerGraph, speakerHardwareFormat, nodeCustomName + "SpeakerOutputRectifier", RECTIFIER_BLOCK_SIZE);
            _speakerPushPull = new PushPullBuffer(speakerGraph, microphoneGraph, _outputFormat, nodeCustomName + "SpeakerPushPull", TimeSpan.FromMilliseconds(200));

            if (speakerHardwareFormat == _outputFormat)
            {
                // If the speaker hardware format is equal to the delay estimator's format, we can consolidate the two conformers into one
                _speakerPreConformer = new AudioConformer(speakerGraph, speakerInputFormat, _outputFormat, nodeCustomName + "SpeakerPreConformer", debugLogger.Clone("Resampler"), resamplerQuality: AudioProcessingQuality.Balanced);
                _speakerHardwareConformer = null;
                _speakerToPeekBufferConformer = null;
                _speakerSplitter = new AudioSplitter(speakerGraph, _outputFormat, nodeCustomName + "SpeakerSplitter");
                _speakerSplitter.AddOutput(_speakerPeekbuffer);
                _speakerSplitter.AddOutput(_speakerOutputRectifier);
                _speakerPreConformer.ConnectOutput(_speakerSplitter);
                _speakerInput = new WeakPointer<IAudioSampleTarget>(_speakerPreConformer);
                _speakerOutput = new WeakPointer<IAudioSampleSource>(_speakerOutputRectifier);
            }
            else
            {
                // Otherwise we need separate conformers, one for program -> hardware, and another for program -> delay estimator
                _speakerPreConformer = null;
                _speakerHardwareConformer = new AudioConformer(speakerGraph, speakerInputFormat, speakerHardwareFormat, nodeCustomName + "HardwareConformer", debugLogger.Clone("Resampler"), resamplerQuality: AudioProcessingQuality.Balanced);
                _speakerToPeekBufferConformer = new AudioConformer(speakerGraph, speakerInputFormat, _outputFormat, nodeCustomName + "SpeakerToPeekBufferConformer", debugLogger.Clone("Resampler"), resamplerQuality: AudioProcessingQuality.Balanced);
                _speakerSplitter = new AudioSplitter(speakerGraph, speakerInputFormat, nodeCustomName + "SpeakerSplitter");
                _speakerSplitter.AddOutput(_speakerHardwareConformer);
                _speakerSplitter.AddOutput(_speakerToPeekBufferConformer);
                _speakerHardwareConformer.ConnectOutput(_speakerOutputRectifier);
                _speakerToPeekBufferConformer.ConnectOutput(_speakerPeekbuffer);
                _speakerInput = new WeakPointer<IAudioSampleTarget>(_speakerSplitter);
                _speakerOutput = new WeakPointer<IAudioSampleSource>(_speakerOutputRectifier);
            }

            _speakerPeekbuffer.ConnectOutput(_speakerPushPull);
            _speakerPushPull.ConnectOutput(_speakerFeedbackFloatingDelay);

            // Create components on mic side
            _micInputConformer = new AudioConformer(microphoneGraph, microphoneFormat, _outputFormat, nodeCustomName + "MicInputConformer", debugLogger.Clone("Resampler"), resamplerQuality: AudioProcessingQuality.Balanced);
            _micInputRectifier = new AudioBlockRectifier(microphoneGraph, _outputFormat, nodeCustomName + "MicInputRectifier", RECTIFIER_BLOCK_SIZE);
            _delayEstimator = new FeedbackDelayEstimatorCore(microphoneGraph, _outputFormat, nodeCustomName + "DelayEstimator", PATTERN_MATCH_LENGTH, MAX_DELAY, new WeakPointer<AudioPeekBuffer>(_speakerPeekbuffer), debugLogger);
            _eventDriver = new DelayEstimatorEventDriver(microphoneGraph, _outputFormat, nodeCustomName + "EventDriver", debugLogger, _delayEstimator, _speakerFeedbackFloatingDelay, _speakerPushPull);
            _alignedOutputMixer = new ChannelFaninMixer(microphoneGraph, new AudioSampleFormat(_outputFormat.SampleRateHz, 2, MultiChannelMapping.Packed_2Ch), nodeCustomName + "AlignedOutputMixer");

            _micInputConformer.ConnectOutput(_micInputRectifier);
            _micInputRectifier.ConnectOutput(_delayEstimator);
            _delayEstimator.ConnectOutput(_eventDriver);
            _alignedOutputMixer.AddInput(_eventDriver, null, takeOwnership: false, 0, -1);
            _alignedOutputMixer.AddInput(_speakerFeedbackFloatingDelay, null, takeOwnership: false, -1, 0);

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~FeedbackDelayEstimator()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// The audio output that is normally connected to the speakers should instead go here
        /// </summary>
        public IAudioSampleTarget ProgramAudioInput => _speakerInput.Value;

        /// <summary>
        /// This output should connect directly to the speakers. You do NOT need to conform to the hardware format first
        /// </summary>
        public IAudioSampleSource ProgramAudioOutput => _speakerOutput.Value;

        /// <summary>
        /// This output contains the program audio aligned in time to the microphone input/output as a 2-channel packed stream
        /// </summary>
        public IAudioSampleSource AlignedFeedbackOutput => _alignedOutputMixer;

        /// <summary>
        /// The microphone should connect to this. You do NOT need to conform to the hardware format first
        /// </summary>
        public IAudioSampleTarget MicrophoneInput => _micInputConformer;

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
                _speakerSplitter?.Dispose();
                _speakerPreConformer?.Dispose();
                _speakerHardwareConformer?.Dispose();
                _speakerToPeekBufferConformer?.Dispose();
                _speakerPeekbuffer?.Dispose();
                _speakerOutputRectifier?.Dispose();
                _speakerFeedbackFloatingDelay?.Dispose();
                _speakerPushPull?.Dispose();
                _micInputConformer?.Dispose();
                _micInputRectifier?.Dispose();
                _eventDriver?.Dispose();
                _delayEstimator?.Dispose();
            }
        }

        private class DelayEstimatorEventDriver : AbstractAudioSampleFilter
        {
            // min confidence required to start altering floating delay
            private const float MIN_CONFIDENCE = 0.7f;

            // min amount of change in delay required to commit a change to floating delay
            private const float MIN_DELAY_DELTA_MS = 2.0f;

            private readonly WeakPointer<AudioFloatingDelayBuffer> _floatingDelayBuffer;
            private readonly WeakPointer<FeedbackDelayEstimatorCore> _delayEstimatorCore;
            private readonly WeakPointer<PushPullBuffer> _speakerPushPull;
            private readonly MovingAverageFloat _smoothedAverageDelay;
            private readonly ILogger _debugLogger;
            private float _currentEstimatedDelayMs;

            public DelayEstimatorEventDriver(
                WeakPointer<IAudioGraph> graph,
                AudioSampleFormat format,
                string nodeCustomName,
                ILogger debugLogger,
                FeedbackDelayEstimatorCore delayEstimatorCore,
                AudioFloatingDelayBuffer floatingDelayBuffer,
                PushPullBuffer speakerPushPull)
                : base(graph, nameof(DelayEstimatorEventDriver), nodeCustomName)
            {
                InputFormat = format.AssertNonNull(nameof(format));
                OutputFormat = format;
                _delayEstimatorCore = new WeakPointer<FeedbackDelayEstimatorCore>(delayEstimatorCore.AssertNonNull(nameof(delayEstimatorCore)));
                _floatingDelayBuffer = new WeakPointer<AudioFloatingDelayBuffer>(floatingDelayBuffer.AssertNonNull(nameof(floatingDelayBuffer)));
                _speakerPushPull = new WeakPointer<PushPullBuffer>(speakerPushPull.AssertNonNull(nameof(speakerPushPull)));
                _smoothedAverageDelay = new MovingAverageFloat(10, 0.0f);
                _debugLogger = debugLogger;
                _currentEstimatedDelayMs = 0;
            }

            protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                ProcessEvent();
                return Input.ReadAsync(buffer, offset, count, cancelToken, realTime);
            }

            protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                ProcessEvent();
                return Output.WriteAsync(buffer, offset, count, cancelToken, realTime);
            }

            private void ProcessEvent()
            {
                Hypothesis<TimeSpan> estimatedDelay = _delayEstimatorCore.Value.GetEstimatedDelay();
                if (estimatedDelay.Conf > MIN_CONFIDENCE)
                {
                    TimeSpan augmentedDelay = estimatedDelay.Value;

                    // Augment the delay based on components in the estimation pipeline
                    // This doesn't actually improve accuracy so ignore it
                    // augmentedDelay -= _speakerPushPull.Value.AlgorithmicDelay;

                    if (augmentedDelay < TimeSpan.Zero)
                    {
                        augmentedDelay = TimeSpan.Zero;
                    }
                    if (augmentedDelay > MAX_DELAY)
                    {
                        augmentedDelay = MAX_DELAY;
                    }

                    _smoothedAverageDelay.Add((float)augmentedDelay.TotalMilliseconds);
                }

                float delayDeltaMs = (float)Math.Abs(_currentEstimatedDelayMs - _smoothedAverageDelay.Average);
                if (delayDeltaMs > MIN_DELAY_DELTA_MS)
                {
                    if (_debugLogger != null)
                    {
                        _debugLogger.Log("Updating feedback delay estimate to " + _smoothedAverageDelay.Average + "ms");
                        _debugLogger.Log(
                            string.Format("Raw delays: Estimator +{0:F2} FeedbackPP -{1:F2}",
                                estimatedDelay.Value.TotalMilliseconds,
                                _speakerPushPull.Value.AlgorithmicDelay.TotalMilliseconds));
                    }

                    _currentEstimatedDelayMs = _smoothedAverageDelay.Average;
                    _floatingDelayBuffer.Value.AlgorithmicDelay = TimeSpan.FromMilliseconds(_currentEstimatedDelayMs);
                }
            }
        }
    }
}
