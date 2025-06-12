using Durandal.Common.Audio.WebRtc;
using Durandal.Common.Collections;
using Durandal.Common.Events;
using Durandal.Common.Instrumentation;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Components
{
    /// <summary>
    /// Audio graph component which performs a linear mix of one or more inputs into a single output.
    /// </summary>
    public class LinearMixer : AbstractAudioSampleSource
    {
        // Each mixer input holds a small buffer of samples which is only used if multiple reads to the mixer do not align.
        // For example, if one input reads 1000 samples but another input reads zero, the read samples need to be cached somewhere until the
        // mixer can return the fully mixed output
        private static readonly TimeSpan MIXER_INPUT_BUFFER_LENGTH = TimeSpan.FromMilliseconds(500);

        private readonly HashSet<LinearMixerInputStream> _inputs = new HashSet<LinearMixerInputStream>();
        private readonly List<WorkClosure> _parallelTasks = new List<WorkClosure>(); // used to avoid reallocations
        private readonly List<LinearMixerInputStream> _prunedInputs = new List<LinearMixerInputStream>(); // used to avoid reallocations
        private readonly WeakPointer<IMetricCollector> _metrics;
        private readonly DimensionSet _metricDimensions;
        private readonly ILogger _logger;
        private readonly bool _readForever;
        private bool _playbackFinished = false;
        private bool _firstInputConnected = false;
        private int _disposed = 0;

        public LinearMixer(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            bool readForever = true,
            ILogger logger = null,
            WeakPointer<IMetricCollector> metrics = default(WeakPointer<IMetricCollector>),
            DimensionSet metricDimensions = null) : base(graph, nameof(LinearMixer), nodeCustomName)
        {
            if (format == null)
            {
                throw new ArgumentNullException(nameof(format));
            }

            _logger = logger ?? NullLogger.Singleton;
            _metrics = metrics.DefaultIfNull(() => NullMetricCollector.Singleton);
            _metricDimensions = metricDimensions ?? DimensionSet.Empty;
            _readForever = readForever;
            OutputFormat = format;
            ChannelFinishedEvent = new AsyncEvent<PlaybackFinishedEventArgs>();
        }
        
        public AsyncEvent<PlaybackFinishedEventArgs> ChannelFinishedEvent { get; private set; }

        public override bool PlaybackFinished => _playbackFinished;

        /// <summary>
        /// Connects a sample source to this mixer.
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
        public void AddInput(IAudioSampleSource source, object channelToken = null, bool takeOwnership = false)
        {
            if (_playbackFinished)
            {
                throw new InvalidOperationException("Cannot add an input to a mixer after its playback has finished (you probably want to enable readForever mode)");
            }

            LinearMixerInputStream internalEndpoint = new LinearMixerInputStream(new WeakPointer<IAudioGraph>(OutputGraph), new WeakPointer<LinearMixer>(this), channelToken, MIXER_INPUT_BUFFER_LENGTH, takeOwnership);
            source.ConnectOutput(internalEndpoint);

            OutputGraph.LockGraph();
            try
            {
                _firstInputConnected = true;
                _inputs.Add(internalEndpoint);
            }
            finally
            {
                OutputGraph.UnlockGraph();
            }
        }

        public void DisconnectAllInputs()
        {
            OutputGraph.LockGraph();
            try
            {
                foreach (var input in _inputs)
                {
                    if (input.OwnsInput)
                    {
                        input.Input?.Dispose();
                    }

                    input?.Dispose();
                }

                _inputs.Clear();
            }
            finally
            {
                OutputGraph.UnlockGraph();
            }
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
            if (_inputs.Count == 0)
            {
                // No inputs
                if (_readForever)
                {
                    // Pad with silence if we are in read forever mode (the default)
                    ArrayExtensions.WriteZeroes(buffer, offset, count * OutputFormat.NumChannels);
                    return count;
                }
                else if (_firstInputConnected)
                {
                    // There used to be inputs but they are all exhausted. Return end of stream.
                    _playbackFinished = true;
                    return -1;
                }
                else
                {
                    // No inputs have been configured for this mixer, so return "nothing available yet"
                    return 0;
                }
            }

            while (samplesPerChannelWrittenToOutput < count)
            {
                if (_inputs.Count == 1)
                {
                    // Special fast path for single input -> single output. No need for the parallel task pool
                    LinearMixerInputStream input = _inputs.Single();
                    await input.TryGetSamplesIntoBuffer(count - samplesPerChannelWrittenToOutput, cancelToken, realTime).ConfigureAwait(false);

                    bool inputFinished = input.SamplesPerChannelInBuffer == 0 && input.PlaybackFinished;
                    if (inputFinished)
                    {
                        _inputs.Clear();
                        if (input.PlaybackFinished && input.ChannelToken != null)
                        {
                            ChannelFinishedEvent.FireInBackground(this, new PlaybackFinishedEventArgs(input.ChannelToken, realTime), _logger, realTime);
                        }

                        // Disposal of the input will happen when it is disconnected from its source, so don't worry about it now
                        // Unless, of course, this mixer input takes ownership of the source that it's connected to
                        if (input.OwnsInput)
                        {
                            // we need to disconnect it in this weird way because we currently hold the graph lock
                            input.Input?.DisconnectOutput(true);
                            input.Input?.Dispose();
                            input.DisconnectInput(true);
                        }
                    }
                }
                else
                {
                    _parallelTasks.Clear();
                    foreach (LinearMixerInputStream input in _inputs)
                    {
                        if (input.Input != null)
                        {
                            _parallelTasks.Add(new WorkClosure()
                            {
                                Input = input,
                                Task = input.TryGetSamplesIntoBuffer(count - samplesPerChannelWrittenToOutput, cancelToken, realTime)
                            });
                        }
                    }

                    foreach (WorkClosure workItem in _parallelTasks)
                    {
                        await workItem.Task.ConfigureAwait(false);
                    }

                    // Check for inputs that have finished playback
                    _prunedInputs.Clear();
                    foreach (LinearMixerInputStream input in _inputs)
                    {
                        // check if source has disconnected or has finished playback
                        if (input.Input == null ||
                            (input.SamplesPerChannelInBuffer == 0 &&
                            input.PlaybackFinished))
                        {
                            _prunedInputs.Add(input);
                        }
                    }

                    foreach (LinearMixerInputStream input in _prunedInputs)
                    {
                        _inputs.Remove(input);
                        if (input.PlaybackFinished && input.ChannelToken != null)
                        {
                            ChannelFinishedEvent.FireInBackground(this, new PlaybackFinishedEventArgs(input.ChannelToken, realTime), _logger, realTime);
                        }

                        // Disposal of the input will happen when it is disconnected from its source, so don't worry about it now
                        // Unless, of course, this mixer input takes ownership of the source that it's connected to
                        if (input.OwnsInput)
                        {
                            // we need to disconnect it in this weird way because we currently hold the graph lock
                            input.Input?.DisconnectOutput(true);
                            input.Input?.Dispose();
                            input.DisconnectInput(true);
                        }
                    }
                }

                // Did we just prune all of our inputs?
                if (_inputs.Count == 0)
                {
                    if (samplesPerChannelWrittenToOutput == 0)
                    {
                        if (_readForever)
                        {
                            // Pad with silence if we are in read forever mode (the default)
                            ArrayExtensions.WriteZeroes(buffer, offset, count * OutputFormat.NumChannels);
                            return count;
                        }
                        else
                        {
                            // There used to be inputs but they are all exhausted. Return end of stream.
                            _playbackFinished = true;
                            return -1;
                        }
                    }
                    else
                    {
                        return samplesPerChannelWrittenToOutput;
                    }
                }

                // All inputs should have attempted to read by now, or else been pruned. See if we can output anything
                int minSamplesPerChannelAvailable = count - samplesPerChannelWrittenToOutput;
                foreach (LinearMixerInputStream input in _inputs)
                {
                    minSamplesPerChannelAvailable = FastMath.Min(minSamplesPerChannelAvailable, input.SamplesPerChannelInBuffer);
                }

                if (minSamplesPerChannelAvailable == 0)
                {
                    // If we have got this far but failed to produce any output samples, just give up
                    return samplesPerChannelWrittenToOutput;
                }
                else
                {
                    // there's no longer any need to zero the output explicitly since we rely on the first
                    // copy from input -> mixer to do that
                    //ArrayExtensions.WriteZeroes(
                    //    buffer,
                    //    offset + (samplesPerChannelWrittenToOutput * OutputFormat.NumChannels),
                    //    minSamplesPerChannelAvailable * OutputFormat.NumChannels);
                    bool shouldInputInitializeBuffer = true;
                    foreach (LinearMixerInputStream input in _inputs)
                    {
                        input.WriteFromBufferToMixerOutput(buffer, offset + (samplesPerChannelWrittenToOutput * OutputFormat.NumChannels), minSamplesPerChannelAvailable, shouldInputInitializeBuffer);
                        shouldInputInitializeBuffer = false;
                    }
                }

                samplesPerChannelWrittenToOutput += minSamplesPerChannelAvailable;
            }

            return samplesPerChannelWrittenToOutput;
        }

        private async ValueTask DriveInput(LinearMixerInputStream origin, float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // If this is a flush, check first that the input that's driving the flush is actually still connected
            if (count == 0 && !_inputs.Contains(origin))
            {
                return;
            }

            bool firstLoop = true;
            using (PooledBuffer<float> mixingBuffer = BufferPool<float>.Rent((count + origin.SamplesPerChannelInBuffer) * OutputFormat.NumChannels))
            {
                int samplesPerChannelReadFromInput = 0;
                while (samplesPerChannelReadFromInput < count || (count == 0 && firstLoop)) // The second clause here is to allow for flushing the buffer by passing a count of zero
                {
                    firstLoop = false;
                    // Is the origin buffer already full? Then there's nothing we can do; we have to drop the samples.
                    if (origin.SamplesPerChannelInBuffer == origin.BufferSizePerChannel)
                    {
                        _metrics.Value.ReportInstant(CommonInstrumentation.Key_Counter_LinearMixer_Overflow_Samples, _metricDimensions, count - samplesPerChannelReadFromInput);
                        return;
                    }

                    // Read from input into the source stream's buffer
                    if (count > 0)
                    {
                        int samplesPerChannelReadIntoOrigin = origin.WriteIntoBufferFromMixerInput(buffer, offset + (samplesPerChannelReadFromInput * OutputFormat.NumChannels), count - samplesPerChannelReadFromInput);
                        samplesPerChannelReadFromInput += samplesPerChannelReadIntoOrigin;
                    }

                    int samplesPerChannelInOriginBuffer = origin.SamplesPerChannelInBuffer;
                    _parallelTasks.Clear();
                    foreach (LinearMixerInputStream input in _inputs)
                    {
                        if (input != origin &&
                            input.Input != null)
                        {
                            _parallelTasks.Add(new WorkClosure()
                            {
                                Input = input,
                                Task = input.TryGetSamplesIntoBuffer(samplesPerChannelInOriginBuffer, cancelToken, realTime)
                            });
                        }
                    }

                    foreach (WorkClosure workItem in _parallelTasks)
                    {
                        await workItem.Task.ConfigureAwait(false);
                    }

                    // Check for inputs that have finished playback
                    _prunedInputs.Clear();
                    foreach (LinearMixerInputStream input in _inputs)
                    {
                        if (input.Input == null ||
                            (input.SamplesPerChannelInBuffer == 0 &&
                            input.PlaybackFinished))
                        {
                            _prunedInputs.Add(input);
                        }
                    }

                    foreach (LinearMixerInputStream input in _prunedInputs)
                    {
                        _inputs.Remove(input);
                        if (input.PlaybackFinished && input.ChannelToken != null)
                        {
                            ChannelFinishedEvent.FireInBackground(this, new PlaybackFinishedEventArgs(input.ChannelToken, realTime), _logger, realTime);
                        }

                        // Disposal of the input will happen when it is disconnected from its source, so don't worry about it now
                        // Unless, of course, this mixer input takes ownership of the source that it's connected to
                        if (input.OwnsInput)
                        {
                            // we need to disconnect it in this weird way because we currently hold the graph lock
                            input.Input?.DisconnectOutput(true);
                            input.Input?.Dispose();
                            input.DisconnectInput(true);
                        }
                    }

                    // All inputs should have attempted to read by now, or else been pruned. See if we can output anything
                    if (_inputs.Count > 0)
                    {
                        int minSamplesPerChannelAvailable = int.MaxValue;
                        foreach (LinearMixerInputStream input in _inputs)
                        {
                            minSamplesPerChannelAvailable = FastMath.Min(minSamplesPerChannelAvailable, input.SamplesPerChannelInBuffer);
                        }

                        if (minSamplesPerChannelAvailable > 0)
                        {
                            // no need to write zeroes here since the first input -> output copy will initialize the buffer
                            //ArrayExtensions.WriteZeroes(mixingBuffer.Buffer, 0, minSamplesPerChannelAvailable * OutputFormat.NumChannels);
                            bool shouldInputInitializeBuffer = true;
                            foreach (LinearMixerInputStream input in _inputs)
                            {
                                input.WriteFromBufferToMixerOutput(mixingBuffer.Buffer, 0, minSamplesPerChannelAvailable, shouldInputInitializeBuffer);
                                shouldInputInitializeBuffer = false;
                            }

                            if (Output != null)
                            {
                                await Output.WriteAsync(mixingBuffer.Buffer, 0, minSamplesPerChannelAvailable, cancelToken, realTime).ConfigureAwait(false);
                            }
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.Assert(samplesPerChannelReadFromInput == count, "Audio mixer must finish the batch before pruning all inputs");
                        return;
                    }
                }
            }
        }

        private async Task FlushMixerOutputAsync(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (Output != null)
            {
                await Output.FlushAsync(cancelToken, realTime).ConfigureAwait(false);
            }
        }

        private struct WorkClosure
        {
            public LinearMixerInputStream Input;
            public ValueTask Task;
        }

        /// <summary>
        /// Internal endpoint class for input to a <see cref="LinearMixer"/>.
        /// </summary>
        private class LinearMixerInputStream : AbstractAudioSampleTarget
        {
            private readonly WeakPointer<LinearMixer> _parent;
            private readonly Guid _streamId;
            private readonly PooledBuffer<float> _buffer;
            private int _disposed = 0;

            public LinearMixerInputStream(WeakPointer<IAudioGraph> graph, WeakPointer<LinearMixer> parent, object channelToken, TimeSpan bufferLength, bool ownsInput)
                : base(graph, nameof(LinearMixerInputStream), nodeCustomName: null)
            {
                if (bufferLength <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException("Mixer input buffer length must be a positive quantity");
                }

                _parent = parent.AssertNonNull(nameof(parent));
                OwnsInput = ownsInput;
                _streamId = Guid.NewGuid();
                InputFormat = parent.Value.OutputFormat;
                ChannelToken = channelToken;
                BufferSizePerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(InputFormat.SampleRateHz, bufferLength);
                _buffer = BufferPool<float>.Rent(BufferSizePerChannel * InputFormat.NumChannels);
            }
            
            public bool PlaybackFinished { get; private set; }

            public int SamplesPerChannelInBuffer { get; private set; }

            public int BufferSizePerChannel { get; private set; }

            public object ChannelToken { get; private set; }

            public bool OwnsInput { get; private set; }

            /// <summary>
            /// Attempts to read data from the input such that the requested number of samples are in the buffer.
            /// </summary>
            /// <param name="desiredSamplesPerChannel"></param>
            /// <param name="cancelToken"></param>
            /// <param name="realTime"></param>
            /// <returns></returns>
            public async ValueTask TryGetSamplesIntoBuffer(int desiredSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                int amountUntilBufferFull = BufferSizePerChannel - SamplesPerChannelInBuffer;
                int amountToTryAndRead = FastMath.Min(amountUntilBufferFull, desiredSamplesPerChannel);
                if (amountToTryAndRead > 0)
                {
                    int samplesPerChannelActuallyRead = await Input.ReadAsync(
                        _buffer.Buffer, SamplesPerChannelInBuffer * InputFormat.NumChannels, amountToTryAndRead, cancelToken, realTime).ConfigureAwait(false);
                    if (samplesPerChannelActuallyRead > 0)
                    {
                        SamplesPerChannelInBuffer += samplesPerChannelActuallyRead;
                    }
                    else if (samplesPerChannelActuallyRead < 0)
                    {
                        PlaybackFinished = true;
                    }
                }
            }

            public int WriteIntoBufferFromMixerInput(float[] source, int readOffset, int samplesPerChannelToWrite)
            {
                int amountToActuallyRead = FastMath.Min(samplesPerChannelToWrite, BufferSizePerChannel - SamplesPerChannelInBuffer);
                ArrayExtensions.MemCopy(
                    source,
                    readOffset,
                    _buffer.Buffer,
                    SamplesPerChannelInBuffer * InputFormat.NumChannels,
                    amountToActuallyRead * InputFormat.NumChannels);
                SamplesPerChannelInBuffer += amountToActuallyRead;
                return amountToActuallyRead;
            }

            public void WriteFromBufferToMixerOutput(float[] target, int targetOffset, int samplesPerChannelToWrite, bool isFirst)
            {
                if (isFirst)
                {
                    // If we're the first one to touch this buffer, we can just memcopy straight across
                    ArrayExtensions.MemCopy(
                        _buffer.Buffer,
                        0,
                        target,
                        targetOffset,
                        samplesPerChannelToWrite * InputFormat.NumChannels);
                }
                else
                {
                    // If we're not the first one to write to the output buffer, then we do the linear mix here
                    int sampleIdx = 0;
                    int fullStopIdx = samplesPerChannelToWrite * InputFormat.NumChannels;

                    // Vectorize!
#if DEBUG
                    if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
                    if (Vector.IsHardwareAccelerated)
#endif
                    {
                        int vectorStopIdx = fullStopIdx - (fullStopIdx % Vector<float>.Count);
                        while (sampleIdx < vectorStopIdx)
                        {
                            // this would technically be more efficient if we did the accumulation across
                            // all inputs at once, rather then processing the entire buffer for one input,
                            // then iterate the whole buffer again for the next input, etc.
                            // But that would require a rather large redesign
                            Vector.Add(
                                new Vector<float>(_buffer.Buffer, sampleIdx),
                                new Vector<float>(target, targetOffset + sampleIdx))
                                .CopyTo(target, targetOffset + sampleIdx);
                            sampleIdx += Vector<float>.Count;
                        }
                    }

                    while (sampleIdx < fullStopIdx)
                    {
                        target[targetOffset + sampleIdx] += _buffer.Buffer[sampleIdx];
                        sampleIdx++;
                    }
                }

                if (samplesPerChannelToWrite == SamplesPerChannelInBuffer)
                {
                    // We wrote out the entire buffer (ideal case)
                    SamplesPerChannelInBuffer = 0;
                }
                else
                {
                    // Buffer still has samples. MemMove() them to the left (we could do a wraparound buffer to avoid this but it seems more trouble than it's worth)
                    ArrayExtensions.MemMove(
                        _buffer.Buffer,
                        samplesPerChannelToWrite * InputFormat.NumChannels,
                        0,
                        (SamplesPerChannelInBuffer - samplesPerChannelToWrite) * InputFormat.NumChannels);
                    SamplesPerChannelInBuffer -= samplesPerChannelToWrite;
                }
            }

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }

                LinearMixerInputStream other = (LinearMixerInputStream)obj;
                return _streamId.Equals(other._streamId);
            }

            public override int GetHashCode()
            {
                return _streamId.GetHashCode();
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
                        _buffer?.Dispose();
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }

            protected override async ValueTask FlushAsyncInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                // Write all samples currently in our buffer out to the mixer
                // This is accomplished by just driving an input of zero length
                await _parent.Value.DriveInput(this, null, 0, 0, cancelToken, realTime).ConfigureAwait(false);

                // Flush through the mixer output.
                await _parent.Value.FlushMixerOutputAsync(cancelToken, realTime).ConfigureAwait(false);
            }

            protected override ValueTask WriteAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                return _parent.Value.DriveInput(this, buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
            }

            protected override void OnInputDisconnected()
            {
                Dispose();
            }
        }
    }
}
