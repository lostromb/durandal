using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Beamforming
{
    public sealed class BeamFormer : AbstractAudioSampleFilter
    {
        /// <summary>
        /// The target maximum amount of error we want to have in source origination detection,
        /// in degrees. Lower numbers take more computation, higher numbers are less accurate;
        /// </summary>
        private const float MIN_DISCRIMINATION_ANGLE_DEGREES = 1f;
        private const float MIN_DISCRIMINATION_ANGLE_RADIANS = MIN_DISCRIMINATION_ANGLE_DEGREES * (float)Math.PI / 180f;

        /// <summary>
        /// The farthest distance from the microphone that we can assume for a source, for steering purposes.
        /// Values larger than this will be normalized.
        /// </summary>
        private const float MAX_DIST_METERS = 5;

        /// <summary>
        /// The amount of time that we process in each slice. It's kept fixed to avoid edge cases
        /// and to make sure the behavior of how often to update hypotheses, etc. is the same every time.
        /// Note that something which still can vary is the input sampling rate, which affects the amount
        /// of samples going into each correlation.
        /// </summary>
        private readonly TimeSpan PROCESSING_SLICE_TIME = TimeSpan.FromMilliseconds(20);

        /// <summary>
        /// The size of our work area, which is dictated by processing slice time above.
        /// </summary>
        private readonly int _workAreaSizeSamplesPerChannel;

        /// <summary>
        /// Overflow area to the right of work area in the input buffer
        /// </summary>
        private readonly int _overlapAreaSizeSamplesPerChannel;

        /// <summary>
        /// Length is equal to work area size + overlap area size
        /// </summary>
        private readonly int _inputBufferLengthPerChannel;

        /// <summary>
        /// The input buffer. Each array holds 1 channel worth of samples
        /// as a work area + additional overflow area.
        /// </summary>
        private readonly float[][] _inputBuffersPerChannel;

        /// <summary>
        /// The output buffer. Size is equal to work area size. No channel count calculation since output is mono.
        /// This holds the processed mono output signal which is ready to send to output. It's possible that this
        /// doesn't get read in an entire slice, in which case we have to make sure that Read() passes along that data first.
        /// </summary>
        private readonly float[] _outputBuffer;

        /// <summary>
        /// The current offsets used for 
        /// </summary>
        private readonly int[] _currentSteeringOffsets;

        /// <summary>
        /// The largest possible offset that a signal could generate between two microphone pairs, which is dictated
        /// by the geometry of the array, the input sample rate, and the speed of sound between each mic
        /// </summary>
        private readonly int _largestPossibleOffset;

        /// <summary>
        /// The geometry of the array microphone which is producing the input stream.
        /// </summary>
        private readonly ArrayMicrophoneGeometry _geometry;

        private readonly AttentionPattern _attentionPattern;

        public ArrayMicrophoneGeometry MicGeometry => _geometry;

        public AttentionPattern AttentionPattern => _attentionPattern;

        /// <summary>
        /// A logger
        /// </summary>
        private readonly ILogger _logger;

        private readonly List<MicPair> _micPairs;

        /// <summary>
        /// The current excitation value for each vector in the AttentionPattern. The value represents the likelihood that we are picking up a sound originating from that vector.
        /// </summary>
        private readonly float[] _excitationVectors;

        /// <summary>
        /// The current steering vector or hypothesized origin of the sound we are interested in, measured in meters
        /// </summary>
        private Vector3f _currentFocalPointMeters;

        /// <summary>
        /// number of samples per channel currently in our work area
        /// </summary>
        private int _numValidSamplesPerChannelInInputBuffers;

        /// <summary>
        /// number of samples currently in the output buffer ready to send
        /// </summary>
        private int _numValidSamplesInOutputBuffer;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="graph">The graph that this component is a part of.</param>
        /// <param name="logger">A logger</param>
        /// <param name="sampleRate">The sample rate of the pipe.</param>
        /// <param name="inputChannelLayout">The input channel layout.</param>
        /// <param name="geometry">The array microphone geometry to simulate</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        public BeamFormer(
            WeakPointer<IAudioGraph> graph,
            ILogger logger,
            int sampleRate,
            MultiChannelMapping inputChannelLayout,
            ArrayMicrophoneGeometry geometry,
            AttentionPattern attentionPattern,
            string nodeCustomName)
            : base(graph, nameof(BeamFormer), nodeCustomName)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            inputChannelLayout.AssertNonNull(nameof(inputChannelLayout));
            _logger = logger.AssertNonNull(nameof(logger));
            _geometry = geometry.AssertNonNull(nameof(geometry));
            _attentionPattern = attentionPattern.AssertNonNull(nameof(attentionPattern));
            InputFormat = new AudioSampleFormat(sampleRate, AudioSampleFormat.GetNumChannelsForLayout(inputChannelLayout), inputChannelLayout);
            OutputFormat = AudioSampleFormat.Mono(sampleRate); 

            if (InputFormat.NumChannels != _geometry.NumElements)
            {
                throw new ArgumentException("Microphone geometry does not match the number of input channels given");
            }
            if (_geometry.NumElements <= 0)
            {
                throw new ArgumentException("Array microphone geometry must have at least 1 element");
            }

            _largestPossibleOffset = (int)Math.Ceiling(_geometry.MaxElementSeparation / AudioMath.SpeedOfSoundMillimetersPerSample(sampleRate));
            _currentSteeringOffsets = new int[_geometry.NumElements];
            _overlapAreaSizeSamplesPerChannel = _largestPossibleOffset + 1;
            _workAreaSizeSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(sampleRate, PROCESSING_SLICE_TIME);
            _inputBufferLengthPerChannel = _workAreaSizeSamplesPerChannel + _overlapAreaSizeSamplesPerChannel;

            _outputBuffer = new float[_workAreaSizeSamplesPerChannel];
            _inputBuffersPerChannel = new float[InputFormat.NumChannels][];
            for (int chan = 0; chan < InputFormat.NumChannels; chan++)
            {
                _inputBuffersPerChannel[chan] = new float[_inputBufferLengthPerChannel];
            }

            _numValidSamplesPerChannelInInputBuffers = 0;
            IEnumerable<Tuple<int, int>> pairings = _geometry.MicrophonePairings;
            // If no pairings are prespecified, make a comprehensive set
            if (_geometry.MicrophonePairings == null || _geometry.MicrophonePairings.Count == 0)
            {
                List<Tuple<int, int>> totalPairings = new List<Tuple<int, int>>();
                for (int x = 0; x < _geometry.NumElements - 1; x++)
                {
                    for (int y = x + 1; y < _geometry.NumElements; y++)
                    {
                        totalPairings.Add(new Tuple<int, int>(x, y));
                    }
                }

                pairings = totalPairings;
                _micPairs = new List<MicPair>(totalPairings.Count);
            }
            else
            {
                _micPairs = new List<MicPair>(_geometry.MicrophonePairings.Count);
            }

            foreach (Tuple<int, int> pair in pairings)
            {
                MicPair toAdd = new MicPair(
                    pair.Item1,
                    pair.Item2,
                    _geometry.MicrophonePositions[pair.Item1],
                    _geometry.MicrophonePositions[pair.Item2],
                    InputFormat.SampleRateHz,
                    angleResolutionDegrees: MIN_DISCRIMINATION_ANGLE_DEGREES);
                _micPairs.Add(toAdd);
                if (toAdd.VectorAngleOffsets.Count < 5)
                {
                    logger.LogFormat(
                        LogLevel.Wrn,
                        DataPrivacyClassification.SystemMetadata,
                        "Microphone pair {0}-{1} has only {2} vectors of discrimination, which will probably give inaccurate results. Increase the input sample rate to get better beamforming results.",
                        toAdd.AIndex,
                        toAdd.BIndex,
                        toAdd.VectorAngleOffsets.Count);
                }
            }

            // Find out which mic pair angles are actually needed to cover the attention pattern
            List<ContributingFactor> cf = new List<ContributingFactor>();
            foreach (MicPair micPair in _micPairs)
            {
                int attentionIdx = 0;
                foreach (Vector3f attentionPoint in _attentionPattern.Positions)
                {
                    float angle = micPair.PrimaryAxis.AngleBetween(attentionPoint - micPair.Centroid);
                    float highestAngleBelow = 0;
                    int highestAngleSampleOffset = micPair.VectorAngleOffsets[micPair.VectorAngleOffsets.Count - 1].Item2;
                    float lowestAngleAbove = FastMath.PI;
                    int lowestAngleSampleOffset = micPair.VectorAngleOffsets[0].Item2;
                    foreach (var angleOffset in micPair.VectorAngleOffsets)
                    {
                        if (angleOffset.Item1 <= angle &&
                            angleOffset.Item1 > highestAngleBelow)
                        {
                            highestAngleBelow = angleOffset.Item1;
                            highestAngleSampleOffset = angleOffset.Item2;
                        }

                        if (angleOffset.Item1 >= angle &&
                            angleOffset.Item1 < lowestAngleAbove)
                        {
                            lowestAngleAbove = angleOffset.Item1;
                            lowestAngleSampleOffset = angleOffset.Item2;
                        }
                    }

                    logger.Log($"Attention point {attentionPoint} has a delay between {highestAngleSampleOffset} and {lowestAngleSampleOffset}");
                    float blend = (angle - highestAngleBelow) / (lowestAngleAbove - highestAngleBelow);
                    cf.Add(new ContributingFactor()
                    {
                        AttentionPointIndex = attentionIdx,
                        InputChannelA = micPair.AIndex,
                        InputChannelB = micPair.BIndex,
                        DelayOne = lowestAngleSampleOffset,
                        DelayTwo = highestAngleSampleOffset,
                        DelayMixTowardsTwo = blend
                    });

                    attentionIdx++;
                }
            }

            _excitationVectors = new float[_attentionPattern.NumElements];
        }

        [DebuggerDisplay("[ {DelayOne}, {DelayTwo}, {DelayMixTowardsTwo} ]")]
        private class ContributingFactor
        {
            public int AttentionPointIndex { get; set; }
            public int InputChannelA { get; set; }
            public int InputChannelB { get; set; }
            public int DelayOne { get; set; }
            public int DelayTwo { get; set; }
            public float DelayMixTowardsTwo { get; set; }
        }

        /// <summary>
        /// Temp
        /// </summary>
        public Vector3f FocusPositionMeters
        {
            get
            {
                return _currentFocalPointMeters;
            }
            set
            {
                _currentFocalPointMeters = value;
                if (_currentFocalPointMeters.Magnitude > MAX_DIST_METERS)
                {
                    _currentFocalPointMeters = _currentFocalPointMeters.OfLength(MAX_DIST_METERS);
                }

                RecalculateChannelOffsets();
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int totalSamplesPerChannelReturned = 0;

            // First, read anything that may be in our output buffer ready to send
            // This is guaranteed to either fulfill the entire read, or completely drain the output buffer.
            // Good because we don't want to handle partial-buffer edge cases later on in this function.
            int samplesCanReadFromExistingOutputBuffer = FastMath.Min(count - totalSamplesPerChannelReturned, _numValidSamplesInOutputBuffer);
            if (samplesCanReadFromExistingOutputBuffer > 0)
            {
                ArrayExtensions.MemCopy(
                    _outputBuffer,
                    0,
                    buffer,
                    offset,
                    samplesCanReadFromExistingOutputBuffer);
                offset += samplesCanReadFromExistingOutputBuffer;
                totalSamplesPerChannelReturned += samplesCanReadFromExistingOutputBuffer;

                if (_numValidSamplesInOutputBuffer > samplesCanReadFromExistingOutputBuffer)
                {
                    // move output buffer left on partial read
                    // OPT we could eliminate this by storing the start index of output data in addition to length
                    ArrayExtensions.MemMove(
                        _outputBuffer,
                        samplesCanReadFromExistingOutputBuffer,
                        0,
                        _numValidSamplesInOutputBuffer - samplesCanReadFromExistingOutputBuffer);
                }
                _numValidSamplesInOutputBuffer -= samplesCanReadFromExistingOutputBuffer;
            }

            if (totalSamplesPerChannelReturned == count)
            {
                return totalSamplesPerChannelReturned;
            }

            using (PooledBuffer<float> scratch = BufferPool<float>.Rent())
            {
                int scratchBufferCapacityPerChannel = scratch.Length / InputFormat.NumChannels;
                while (totalSamplesPerChannelReturned < count)
                {
                    int inputSizeReadPerChannel = await Input.ReadAsync(
                        scratch.Buffer,
                        0,
                        FastMath.Min(
                            FastMath.Min(
                                scratchBufferCapacityPerChannel,
                                count - totalSamplesPerChannelReturned),
                                _inputBufferLengthPerChannel - _numValidSamplesPerChannelInInputBuffers),
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    if (inputSizeReadPerChannel < 0)
                    {
                        // End of input. Return -1 end of stream if we haven't read any samples in this loop at all
                        return totalSamplesPerChannelReturned == 0 ? -1 : totalSamplesPerChannelReturned;
                    }
                    else if (inputSizeReadPerChannel == 0)
                    {
                        // Input is exhausted so just return what we've got
                        return totalSamplesPerChannelReturned;
                    }
                    else
                    {
                        // Split the channels apart
                        for (int chan = 0; chan < InputFormat.NumChannels; chan++)
                        {
                            float[] targetBuf = _inputBuffersPerChannel[chan];
                            int inIdx = chan;
                            int outIdx = _numValidSamplesPerChannelInInputBuffers;
                            while (outIdx < _numValidSamplesPerChannelInInputBuffers + inputSizeReadPerChannel)
                            {
                                targetBuf[outIdx] = scratch.Buffer[inIdx];
                                outIdx++;
                                inIdx += InputFormat.NumChannels;
                            }
                        }

                        _numValidSamplesPerChannelInInputBuffers += inputSizeReadPerChannel;
                        if (_numValidSamplesPerChannelInInputBuffers == _inputBufferLengthPerChannel)
                        {
                            // We have a full input buffer ready
                            ProcessInputBuffer();
                            GenerateOutput();

                            int samplesCanCopyToCaller = FastMath.Min(count - totalSamplesPerChannelReturned, _workAreaSizeSamplesPerChannel);
                            ArrayExtensions.MemCopy(
                                _outputBuffer,
                                0,
                                buffer,
                                offset,
                                samplesCanCopyToCaller);

                            totalSamplesPerChannelReturned += samplesCanCopyToCaller;
                            offset += samplesCanCopyToCaller;

                            if (_numValidSamplesInOutputBuffer > samplesCanCopyToCaller)
                            {
                                // move output buffer left on partial read
                                // OPT we could eliminate this by storing the start index of output data in addition to length
                                ArrayExtensions.MemMove(
                                    _outputBuffer,
                                    samplesCanCopyToCaller,
                                    0,
                                    _numValidSamplesInOutputBuffer - samplesCanCopyToCaller);
                            }
                            _numValidSamplesInOutputBuffer -= samplesCanCopyToCaller;
                        }
                    }
                }

                return totalSamplesPerChannelReturned;
            }
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // First, clear our output buffer if there's anything partial in there
            if (_numValidSamplesInOutputBuffer > 0)
            {
                await Output.WriteAsync(
                    _outputBuffer,
                    0,
                    _numValidSamplesInOutputBuffer,
                    cancelToken,
                    realTime).ConfigureAwait(false);
                _numValidSamplesInOutputBuffer = 0;
            }

            int samplesPerChannelReadFromCaller = 0;
            while (samplesPerChannelReadFromCaller < count)
            {
                int inputReadSizePerChannel = 
                    FastMath.Min(
                        count - samplesPerChannelReadFromCaller,
                        _inputBufferLengthPerChannel - _numValidSamplesPerChannelInInputBuffers);

                // Read data from caller and deinterlace the channels at the same time
                for (int chan = 0; chan < InputFormat.NumChannels; chan++)
                {
                    float[] targetBuf = _inputBuffersPerChannel[chan];
                    int inIdx = offset + (samplesPerChannelReadFromCaller * InputFormat.NumChannels) + chan;
                    int outIdx = _numValidSamplesPerChannelInInputBuffers;
                    while (outIdx < inputReadSizePerChannel + _numValidSamplesPerChannelInInputBuffers)
                    {
                        targetBuf[outIdx] = buffer[inIdx];
                        outIdx++;
                        inIdx += InputFormat.NumChannels;
                    }
                }

                _numValidSamplesPerChannelInInputBuffers += inputReadSizePerChannel;
                if (_numValidSamplesPerChannelInInputBuffers == _inputBufferLengthPerChannel)
                {
                    ProcessInputBuffer();
                    GenerateOutput();

                    System.Diagnostics.Debug.Assert(_workAreaSizeSamplesPerChannel == _numValidSamplesInOutputBuffer);
                    await Output.WriteAsync(
                        _outputBuffer,
                        0,
                        _workAreaSizeSamplesPerChannel,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    _numValidSamplesInOutputBuffer = 0;
                }

                samplesPerChannelReadFromCaller += inputReadSizePerChannel;
            }
        }

        /// <summary>
        /// Does the main work of estimating source positions and calculating steering vectors.
        /// Assumes that the input buffer is fully populated. Does not touch the output buffer.
        /// </summary>
        private void ProcessInputBuffer()
        {
            // If this method is called, it assumes that the input buffer is fully populated
            int correlationLength = _workAreaSizeSamplesPerChannel;

            foreach (MicPair micPair in _micPairs)
            {
                // Run some correlations
                List<Tuple<int, float>> data = new List<Tuple<int, float>>();
                //_logger.Log("Mic pair " + micPair.AIndex + "-" + micPair.BIndex);
                StringBuilder builder = new StringBuilder();
                foreach (var warpedVectorDefinition in micPair.VectorAngleOffsets)
                {
                    float correlation;
                    if (warpedVectorDefinition.Item2 > 0)
                    {
                        correlation = Correlation.NormalizedCrossCorrelation(
                            _inputBuffersPerChannel[micPair.AIndex],
                            warpedVectorDefinition.Item2,
                            _inputBuffersPerChannel[micPair.BIndex],
                            0,
                            correlationLength);
                    }
                    else
                    {
                        correlation = Correlation.NormalizedCrossCorrelation(
                            _inputBuffersPerChannel[micPair.AIndex],
                            0,
                            _inputBuffersPerChannel[micPair.BIndex],
                            0 - warpedVectorDefinition.Item2,
                            correlationLength);
                    }

                    // Iterate through all the output vectors and update their excitement based on this correlation
                    const float excitationWindowMinAngle = (10 /* degrees */ * (float)Math.PI / 180.0f);
                    const float excitationWindowMaxAngle = (30 /* degrees */ * (float)Math.PI / 180.0f);
                    const float decayRate = 1.0f; // higher value = more transient decay in the excitation graph
                    //foreach (ExcitationVector outputVector in _outputVectors)
                    //{
                    //    // angle between output vector and the primary axis of this pair (which correlates to a 0 degree angle)
                    //    float primaryAxisAngle = outputVector.Direction.AngleBetweenUnitVectors(micPair.PrimaryAxis);

                    //    // Then add the actual angle difference of the vector to figure out how close this correlation is
                    //    // with the actual output vector that is not along the primary axis.
                    //    // (we can't just take a single "source vector" from the microphone pair
                    //    // because it is a cone represented only by angle)
                    //    float angleDif = Math.Abs(primaryAxisAngle - warpedVectorDefinition.Item1);

                    //    // Apply a windowing function - here it is a basic linear pyramid, but we could get more fancy with it
                    //    float windowedExcitation;
                    //    if (angleDif < excitationWindowMinAngle)
                    //    {
                    //        windowedExcitation = 1.0f;
                    //    }
                    //    else if (angleDif < excitationWindowMaxAngle)
                    //    {
                    //        windowedExcitation = 1.0f - ((angleDif - excitationWindowMinAngle) / (excitationWindowMaxAngle - excitationWindowMinAngle));
                    //    }
                    //    else
                    //    {
                    //        windowedExcitation = 0;
                    //    }

                    //    if (primaryAxisAngle > excitationWindowMaxAngle && primaryAxisAngle < excitationWindowMinAngle)
                    //    {
                    //        windowedExcitation += 1.0f - (primaryAxisAngle / excitationWindowMinAngle);
                    //    }

                    //    if (correlation > 0 && windowedExcitation > 0)
                    //    {
                    //        //builder.AppendFormat("{0},", correlation * windowedExcitation);
                    //        //correlation = (correlation + 1) / 2;
                    //        //correlation = correlation * correlation * correlation;
                    //        outputVector.Excitation += (correlation * windowedExcitation * decayRate);
                    //    }
                    //    else
                    //    {
                    //        //builder.AppendFormat("0,", correlation * windowedExcitation);
                    //    }
                    //}

                    //_logger.Log(builder.ToString());
                    builder.Clear();
                }
            }

            // Normalize excitation to get us the final output
            //float maxExcitation = 0;
            //foreach (ExcitationVector outputVector in _outputVectors)
            //{
            //    maxExcitation = Math.Max(maxExcitation, outputVector.Excitation);
            //}
            //if (maxExcitation > 0)
            //{
            //    foreach (ExcitationVector outputVector in _outputVectors)
            //    {
            //        outputVector.Excitation /= maxExcitation;
            //    }
            //}

            //TinyHistogram histogram = new TinyHistogram();
            //for (int outputIdx = 0; outputIdx < _outputVectorCount; outputIdx++)
            //{
            //    histogram.AddValue(outputIdx, _outputVectors[outputIdx].Excitation);
            //}

            //_logger.Log(string.Format("{0}",
            //    histogram.RenderAsOneLine(_outputVectorCount, false)));
        }

        private void RecalculateChannelOffsets()
        {
            float[] absoluteOffsets = new float[_geometry.NumElements];
            float minOffset = float.MaxValue;
            for (int channel = 0; channel < _geometry.NumElements; channel++)
            {
                absoluteOffsets[channel] = _geometry.MicrophonePositions[channel].Distance(_currentFocalPointMeters * 1000)
                    / AudioMath.SpeedOfSoundMillimetersPerSample(InputFormat.SampleRateHz);
                minOffset = Math.Min(minOffset, absoluteOffsets[channel]);
            }

            for (int channel = 0; channel < _geometry.NumElements; channel++)
            {
                _currentSteeringOffsets[channel] = (int)Math.Round(absoluteOffsets[channel] - minOffset);
            }
        }

        /// <summary>
        /// Fills the output buffer with valid data based on current steering parameters.
        /// Assumes that the input buffer is fully populated. Consumes data from the input
        /// buffer (shifting overlap area to the left as needed) and fully populates the
        /// output buffer.
        /// </summary>
        private void GenerateOutput()
        {
            float normalizer = 1.0f / (float)_geometry.NumElements;
#if DEBUG
            if (Vector.IsHardwareAccelerated && FastRandom.Shared.NextBool())
#else
            if (Vector.IsHardwareAccelerated)
#endif
            {
                // Do a simple warped average of each channel based on the calculated per-channel
                // offsets determined by source estimation earlier.
                // With vectorization, we accumulate 8 samples from all channels and then write
                // it to output once, rather than iterating each channel individually, because it's faster
                int idx = 0;
                int stop = _workAreaSizeSamplesPerChannel - (_workAreaSizeSamplesPerChannel % Vector<float>.Count);

                // vector loop
                while (idx < stop)
                {
                    Vector<float> accumulator = new Vector<float>(0);
                    for (int channel = 0; channel < _geometry.NumElements; channel++)
                    {
                        accumulator = Vector.Add(
                            accumulator,
                            new Vector<float>(
                                _inputBuffersPerChannel[channel],
                                idx + _currentSteeringOffsets[channel]));
                    }

                    Vector.Multiply(accumulator, normalizer)
                        .CopyTo(_outputBuffer, idx);

                    idx += Vector<float>.Count;
                }

                // residual loop
                while (idx < _workAreaSizeSamplesPerChannel)
                {
                    float accum = 0;
                    for (int channel = 0; channel < _geometry.NumElements; channel++)
                    {
                        accum += _inputBuffersPerChannel[channel][idx + _currentSteeringOffsets[channel]];
                    }

                    _outputBuffer[idx] += accum * normalizer;
                    idx++;
                }
            }
            else
            {
                // We can just copy the data from channel 0 straight across to initialize the accumulator
                ArrayExtensions.MemCopy(
                    _inputBuffersPerChannel[0],
                    _currentSteeringOffsets[0],
                    _outputBuffer,
                    0,
                    _workAreaSizeSamplesPerChannel);

                // Do a simple warped average of each channel based on the calculated per-channel
                // offsets determined by source estimation earlier.
                // Use the output buffer as an accumulator as we go
                for (int channel = 1; channel < _geometry.NumElements; channel++)
                {
                    float[] thisChannelInput = _inputBuffersPerChannel[channel];
                    int inIdx = _currentSteeringOffsets[channel];
                    for (int outIdx = 0; outIdx < _workAreaSizeSamplesPerChannel; outIdx++)
                    {
                        _outputBuffer[outIdx] += thisChannelInput[inIdx];
                        inIdx++;
                    }
                }

                // Average the output
                for (int idx = 0; idx < _workAreaSizeSamplesPerChannel; idx++)
                {
                    _outputBuffer[idx] *= normalizer;
                }
            }

            _numValidSamplesInOutputBuffer = _workAreaSizeSamplesPerChannel;

            // Now we're done using the work region of the input buffer.
            // Move overlap area to the beginning of the next slice's work area so our processing remains continuous
            for (int channel = 0; channel < _geometry.NumElements; channel++)
            {
                ArrayExtensions.MemMove(
                    _inputBuffersPerChannel[channel],
                    _workAreaSizeSamplesPerChannel,
                    0,
                    _overlapAreaSizeSamplesPerChannel);
            }

            _numValidSamplesPerChannelInInputBuffers -= _workAreaSizeSamplesPerChannel;
        }

        private void RecalculateSteeringOffsets()
        {
            float[] absoluteOffsets = new float[_geometry.NumElements];
            float minOffset = float.MaxValue;
            for (int channel = 0; channel < _geometry.NumElements; channel++)
            {
                absoluteOffsets[channel] = _geometry.MicrophonePositions[channel].Distance(_currentFocalPointMeters * 1000)
                    / AudioMath.SpeedOfSoundMillimetersPerSample(InputFormat.SampleRateHz);
                minOffset = Math.Min(minOffset, absoluteOffsets[channel]);
            }

            for (int channel = 0; channel < _geometry.NumElements; channel++)
            {
                _currentSteeringOffsets[channel] = (int)Math.Round(absoluteOffsets[channel] - minOffset);
            }
        }
    }
}
