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
    /// Paired component which detects speaker-microphone feedback and applies drastic volume reduction to compensate.
    /// </summary>
    public sealed class FeedbackCircuitBreaker : IDisposable
    {
        private const float RMS_THRESHOLD = 0.55f;
        private static readonly TimeSpan SUPPRESSION_TIME = TimeSpan.FromSeconds(3);

        private readonly PassiveVolumeMeter _outputMeter;
        private readonly PassiveVolumeMeter _inputMeter;
        private readonly VolumeFilter _outputVolume;
        private readonly FeedbackWatchdog _inputWatchdog;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="FeedbackCircuitBreaker"/>, using the same graph and audio format for both input and output.
        /// </summary>
        /// <param name="graph">The graph associated with audio input and output</param>
        /// <param name="format">The format of the audio input and output.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        public FeedbackCircuitBreaker(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName) : this(graph, graph, format, format, nodeCustomName)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="FeedbackCircuitBreaker"/>, with potentially separate graphs and audio formats for the input and output ends.
        /// </summary>
        /// <param name="inputGraph">The graph associated with audio input (microphone end)</param>
        /// <param name="outputGraph">The graph associated with audio output (speaker end).</param>
        /// <param name="inputFormat">The format of input audio.</param>
        /// <param name="outputFormat">The format of output audio.</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        public FeedbackCircuitBreaker(WeakPointer<IAudioGraph> inputGraph, WeakPointer<IAudioGraph> outputGraph, AudioSampleFormat inputFormat, AudioSampleFormat outputFormat, string nodeCustomName)
        {
            _outputMeter = new PassiveVolumeMeter(outputGraph, outputFormat, nodeCustomName);
            _outputVolume = new VolumeFilter(outputGraph, outputFormat, nodeCustomName);
            _inputMeter = new PassiveVolumeMeter(inputGraph, inputFormat, nodeCustomName);
            _inputWatchdog = new FeedbackWatchdog(inputGraph, inputFormat, nodeCustomName, this);
            _outputMeter.ConnectOutput(_outputVolume);
            _inputMeter.ConnectOutput(_inputWatchdog);
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        /// <summary>
        /// The thing that would normally connect to the speaker should connect here
        /// </summary>
        public IAudioSampleTarget SpeakerFilterInput => _outputMeter;

        /// <summary>
        /// The speaker connects to this one here
        /// </summary>
        public IAudioSampleSource SpeakerFilterOutput => _outputVolume;

        /// <summary>
        /// The microphone connects to this one here
        /// </summary>
        public IAudioSampleTarget MicrophoneFilterInput => _inputMeter;

        /// <summary>
        /// The thing that would normally connect to the microphone should connect here
        /// </summary>
        public IAudioSampleSource MicrophoneFilterOutput => _inputWatchdog;

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
                _outputMeter?.Dispose();
                _inputMeter?.Dispose();
                _outputVolume?.Dispose();
                _inputWatchdog?.Dispose();
            }
        }

        public class FeedbackWatchdog : AbstractAudioSampleFilter
        {
            private readonly WeakPointer<FeedbackCircuitBreaker> _parent;

            public FeedbackWatchdog(WeakPointer<IAudioGraph> graph, AudioSampleFormat format, string nodeCustomName, FeedbackCircuitBreaker parent) : base(graph, nameof(FeedbackWatchdog), nodeCustomName)
            {
                InputFormat = format.AssertNonNull(nameof(format));
                OutputFormat = format;
                _parent = new WeakPointer<FeedbackCircuitBreaker>(parent);
            }

            protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                CheckForFeedback();
                return Input.ReadAsync(buffer, offset, count, cancelToken, realTime);
            }

            protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                CheckForFeedback();
                return Output.WriteAsync(buffer, offset, count, cancelToken, realTime);
            }

            private bool CheckForFeedback()
            {
                if (_parent.Value._inputMeter.GetLoudestRmsVolume() > RMS_THRESHOLD &&
                    _parent.Value._outputMeter.GetLoudestRmsVolume() > RMS_THRESHOLD)
                {
                    _parent.Value._outputVolume.SetVolumeDecibels(VolumeFilter.MIN_VOLUME_DBA);
                    _parent.Value._outputVolume.SetVolumeDecibels(0.0f, SUPPRESSION_TIME);
                    return true;
                }

                return false;
            }
        }
    }
}
