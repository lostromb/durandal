using Durandal.Common.Collections;
using Durandal.Common.Events;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
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

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio graph component which strings several audio sources together end-to-end.
    /// The internal behavior is very similar to a <see cref="LinearMixer"/>, except only 1 input plays at a time.
    /// </summary>
    public sealed class AudioConcatenator : AbstractAudioSampleSource
    {
        private readonly Queue<ConcatenatorInputStream> _inputs = new Queue<ConcatenatorInputStream>();
        private readonly ILogger _logger;
        private readonly bool _readForever;
        private bool _playbackFinished = false;
        private int _disposed = 0;

        public AudioConcatenator(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            bool readForever = true,
            ILogger logger = null) : base(graph, nameof(AudioConcatenator), nodeCustomName)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }
            
            _readForever = readForever;
            OutputFormat = format;
            _logger = logger ?? NullLogger.Singleton;
            ChannelFinishedEvent = new AsyncEvent<PlaybackFinishedEventArgs>();
        }

        public AsyncEvent<PlaybackFinishedEventArgs> ChannelFinishedEvent { get; private set; }

        public override bool PlaybackFinished => _playbackFinished;

        /// <summary>
        /// Creates a new endpoint to act as an endpoint into this concatenator.
        /// </summary>
        /// <param name="source">The audio sample source to enqueue</param>
        /// <param name="channelToken">An optional channel token that is used to identify this input, used for events that fire when inputs have finished playback</param>
        /// <param name="takeOwnership">If true, the concatenator will take ownership of the input and will dispose of it when its playback has finished.</param>
        public void EnqueueInput(IAudioSampleSource source, object channelToken = null, bool takeOwnership = false)
        {
            ConcatenatorInputStream newInput = null;
            OutputGraph.LockGraph();
            try
            {
                if (_playbackFinished)
                {
                    throw new InvalidOperationException("Cannot add an input to a concatenator after its playback has finished (you probably want to enable readForever mode)");
                }

                newInput = new ConcatenatorInputStream(new WeakPointer<IAudioGraph>(OutputGraph), new WeakPointer<AudioConcatenator>(this), channelToken, takeOwnership);
                _inputs.Enqueue(newInput);
            }
            finally
            {
                OutputGraph.UnlockGraph();
            }

            newInput?.ConnectInput(source);
        }

        protected override void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            try
            {
                if (disposing)
                {
                    foreach (var input in _inputs)
                    {
                        if (input.OwnsInput)
                        {
                            input.Input?.Dispose();
                        }

                        input?.Dispose();
                    }
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_playbackFinished)
            {
                return -1;
            }

            int samplesPerChannelWrittenToOutput = 0;
            while (_inputs.Count > 0 &&
                samplesPerChannelWrittenToOutput < count)
            {
                // Try reading as much as we can from our inputs until they are all exhausted
                int samplesPerChannelActuallyReadFromInput = await _inputs.Peek().ReadFromInputAsync(
                    buffer,
                    offset + (samplesPerChannelWrittenToOutput * OutputFormat.NumChannels),
                    count - samplesPerChannelWrittenToOutput,
                    cancelToken,
                    realTime).ConfigureAwait(false);

                if (samplesPerChannelActuallyReadFromInput < 0)
                {
                    // Input is exhausted. Remove it
                    ConcatenatorInputStream finishedInput = _inputs.Dequeue();
                    if (finishedInput.ChannelToken != null)
                    {
                        ChannelFinishedEvent.FireInBackground(this, new PlaybackFinishedEventArgs(finishedInput.ChannelToken, realTime), _logger, realTime);
                    }

                    // And dispose of it if we own it
                    if (finishedInput.OwnsInput)
                    {
                        // we need to disconnect it in this weird way because we currently hold the graph lock
                        finishedInput.Input?.DisconnectOutput(true);
                        finishedInput.Input?.Dispose();
                        finishedInput.DisconnectInput(true);
                    }
                }
                else if (samplesPerChannelActuallyReadFromInput == 0)
                {
                    break;
                }
                else
                {
                    samplesPerChannelWrittenToOutput += samplesPerChannelActuallyReadFromInput;
                }
            }

            if (_inputs.Count == 0 &&
                samplesPerChannelWrittenToOutput == 0)
            {
                // Nothing was produced by our input, or no inputs are connected
                if (_readForever)
                {
                    // Pad with silence if we are in read forever mode (the default)
                    ArrayExtensions.WriteZeroes(buffer, offset, count * OutputFormat.NumChannels);
                    return count;
                }
                else
                {
                    // All inputs are exhausted (or we never had any to begin with). Return end of stream.
                    _playbackFinished = true;
                    return -1;
                }
            }

            return samplesPerChannelWrittenToOutput;
        }

        /// <summary>
        /// Internal endpoint class for input to a <see cref="AudioConcatenator"/>.
        /// </summary>
        private class ConcatenatorInputStream : AbstractAudioSampleTarget
        {
            private readonly WeakPointer<AudioConcatenator> _parent;
            private readonly Guid _streamId;
            private readonly object _channelToken;
            private bool _playbackFinished = false;

            public ConcatenatorInputStream(WeakPointer<IAudioGraph> graph, WeakPointer<AudioConcatenator> parent, object channelToken, bool ownsInput)
                : base(graph, nameof(ConcatenatorInputStream), nodeCustomName: null)
            {
                _parent = parent.AssertNonNull(nameof(parent));
                _streamId = Guid.NewGuid();
                InputFormat = parent.Value.OutputFormat;
                _channelToken = channelToken;
                OwnsInput = ownsInput;
            }

            public bool PlaybackFinished => _playbackFinished;

            public object ChannelToken => _channelToken;

            public bool OwnsInput { get; private set; }

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }

                ConcatenatorInputStream other = (ConcatenatorInputStream)obj;
                return _streamId.Equals(other._streamId);
            }

            public override int GetHashCode()
            {
                return _streamId.GetHashCode();
            }

            public async Task<int> ReadFromInputAsync(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                if (_playbackFinished)
                {
                    return -1;
                }

                int returnVal = await Input.ReadAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime).ConfigureAwait(false);
                if (returnVal < 0)
                {
                    _playbackFinished = true;
                }

                return returnVal;
            }

            protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new InvalidOperationException("Writing to an audio concatenator is not allowed; it is a one-way component");
            }

            protected override void OnInputDisconnected()
            {
                Dispose();
            }
        }
    }
}
