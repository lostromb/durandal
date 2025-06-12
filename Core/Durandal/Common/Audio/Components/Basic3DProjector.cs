using Durandal.Common.Audio.Beamforming;
using Durandal.Common.Collections;
using Durandal.Common.MathExt;
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
    /// Audio graph component which takes a mono input and a listener array definition, and outputs
    /// a multi-channel track of time-delayed signals. Doppler effect, volume attenuation, and distance
    /// from source to listener is ignored. And there are discontinuities when the source position changes.
    /// This is mainly to simulate inputs into a virtual array microphone device.
    /// </summary>
    // TODO: reuse this code for projecting sound sources to 5.1 / 7.1 surround
    public sealed class Basic3DProjector : AbstractAudioSampleFilter
    {
        private const float MAX_DIST_METERS = 5;
        private readonly ArrayMicrophoneGeometry _geometry;
        private readonly float[] _inputBuffer;
        private readonly int _inputBufferSizeSamplesPerChannel;
        private readonly float[] _outputBuffer;
        private readonly int _outputBufferSizeSamplesPerChannel;
        private readonly int[] _offsets;
        private readonly int _largestPossibleOffset;
        private Vector3f _sourcePositionMeters;

        /// <summary>
        /// Creates a component that "projects" a mono sound in a 3D space to a multichannel array mic
        /// output that simulates what an actual array would hear from that source (in terms of relative
        /// channel offsets because of speed of sound delay).
        /// </summary>
        /// <param name="graph">The graph that this component is a part of.</param>
        /// <param name="sampleRate">The sample rate of the pipe.</param>
        /// <param name="outputChannelLayout">The output channel layout.</param>
        /// <param name="geometry">The array microphone geometry to simulate</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        public Basic3DProjector(
            WeakPointer<IAudioGraph> graph,
            int sampleRate,
            MultiChannelMapping outputChannelLayout,
            ArrayMicrophoneGeometry geometry,
            string nodeCustomName)
            : base(graph, nameof(Basic3DProjector), nodeCustomName)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }

            outputChannelLayout.AssertNonNull(nameof(outputChannelLayout));
            _geometry = geometry.AssertNonNull(nameof(geometry));
            InputFormat = AudioSampleFormat.Mono(sampleRate);
            OutputFormat = new AudioSampleFormat(sampleRate, AudioSampleFormat.GetNumChannelsForLayout(outputChannelLayout), outputChannelLayout);

            if (OutputFormat.NumChannels != _geometry.NumElements)
            {
                throw new ArgumentException("Microphone geometry does not match the number of output channels given");
            }

            _largestPossibleOffset = (int)Math.Ceiling(_geometry.MaxElementSeparation / AudioMath.SpeedOfSoundMillimetersPerSample(sampleRate));
            _offsets = new int[_geometry.NumElements];

            // Create a scratch buffer to hold the lined-up input audio (mono), as well as the staggered offset output audio (multichannel)
            // Output needs to be long enough to hold at least the largest disparity between the sound reaching any pair of mic elements
            _inputBufferSizeSamplesPerChannel = 1024;
            _inputBuffer = new float[_inputBufferSizeSamplesPerChannel];
            _outputBufferSizeSamplesPerChannel = _largestPossibleOffset + _inputBufferSizeSamplesPerChannel;
            _outputBuffer = new float[_outputBufferSizeSamplesPerChannel * OutputFormat.NumChannels];
            _sourcePositionMeters = new Vector3f(0, 0, 0);
            RecalculateChannelOffsets();
        }

        /// <summary>
        /// Gets or sets the virtual position of the mono audio source, in 3D space measured in meters.
        /// </summary>
        public Vector3f SourcePositionMeters
        {
            get
            {
                return _sourcePositionMeters;
            }
            set
            {
                _sourcePositionMeters = value;
                if (_sourcePositionMeters.Magnitude > MAX_DIST_METERS)
                {
                    _sourcePositionMeters = _sourcePositionMeters.OfLength(MAX_DIST_METERS);
                }

                RecalculateChannelOffsets();
            }
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int totalSamplesPerChannelProcessed = 0;
            while (totalSamplesPerChannelProcessed < count)
            {
                int thisBatchSizePerChannel = await Input.ReadAsync(
                    _inputBuffer,
                    0,
                    FastMath.Min(count - totalSamplesPerChannelProcessed, _inputBufferSizeSamplesPerChannel),
                    cancelToken,
                    realTime).ConfigureAwait(false);

                if (thisBatchSizePerChannel < 0)
                {
                    // End of input. Return -1 end of stream if we haven't read any samples in this loop at all
                    return totalSamplesPerChannelProcessed == 0 ? -1 : totalSamplesPerChannelProcessed;
                }
                else if (thisBatchSizePerChannel == 0)
                {
                    // Input is exhausted so just return what we've got
                    return totalSamplesPerChannelProcessed;
                }
                else
                {
                    // Stagger the channels and copy them to output buffer,
                    // then flush samples from our out buffer equal to how many we just read
                    int stride = _geometry.NumElements;
                    for (int channel = 0; channel < _geometry.NumElements; channel++)
                    {
                        int outIdx = channel + (stride * _offsets[channel]);
                        for (int inIdx = 0; inIdx < thisBatchSizePerChannel; inIdx++)
                        {
                            _outputBuffer[outIdx] = _inputBuffer[inIdx];
                            outIdx += stride;
                        }
                    }

                    ArrayExtensions.MemCopy(
                        _outputBuffer,
                        0,
                        buffer,
                        offset,
                        thisBatchSizePerChannel * OutputFormat.NumChannels);
                    ArrayExtensions.MemMove(
                        _outputBuffer,
                        thisBatchSizePerChannel * OutputFormat.NumChannels,
                        0,
                        _largestPossibleOffset * OutputFormat.NumChannels);

                    totalSamplesPerChannelProcessed += thisBatchSizePerChannel;
                    offset += thisBatchSizePerChannel * OutputFormat.NumChannels;
                }
            }

            return totalSamplesPerChannelProcessed;
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int totalSamplesPerChannelProcessed = 0;
            while (totalSamplesPerChannelProcessed < count)
            {
                int thisBatchSizePerChannel = FastMath.Min(count - totalSamplesPerChannelProcessed, _inputBufferSizeSamplesPerChannel);
                ArrayExtensions.MemCopy(
                    buffer,
                    offset,
                    _inputBuffer,
                    0,
                    thisBatchSizePerChannel * InputFormat.NumChannels);

                // Stagger the channels and copy them to output buffer,
                // then flush samples from our out buffer equal to how many we just read
                int stride = _geometry.NumElements;
                for (int channel = 0; channel < _geometry.NumElements; channel++)
                {
                    int outIdx = channel + (stride * _offsets[channel]);
                    for (int inIdx = 0; inIdx < thisBatchSizePerChannel; inIdx++)
                    {
                        _outputBuffer[outIdx] = _inputBuffer[inIdx];
                        outIdx += stride;
                    }
                }

                await Output.WriteAsync(
                    _outputBuffer,
                    0,
                    thisBatchSizePerChannel,
                    cancelToken,
                    realTime).ConfigureAwait(false);
                ArrayExtensions.MemMove(
                    _outputBuffer,
                    thisBatchSizePerChannel * OutputFormat.NumChannels,
                    0,
                    _largestPossibleOffset * OutputFormat.NumChannels);

                totalSamplesPerChannelProcessed += thisBatchSizePerChannel;
                offset += thisBatchSizePerChannel * OutputFormat.NumChannels;
            }
        }

        private void RecalculateChannelOffsets()
        {
            float[] absoluteOffsets = new float[_geometry.NumElements];
            float minOffset = float.MaxValue;
            for (int channel = 0; channel < _geometry.NumElements; channel++)
            {
                absoluteOffsets[channel] = _geometry.MicrophonePositions[channel].Distance(_sourcePositionMeters * 1000)
                    / AudioMath.SpeedOfSoundMillimetersPerSample(InputFormat.SampleRateHz);
                minOffset = Math.Min(minOffset, absoluteOffsets[channel]);
            }

            for (int channel = 0; channel < _geometry.NumElements; channel++)
            {
                _offsets[channel] = (int)Math.Round(absoluteOffsets[channel] - minOffset);
            }
        }
    }
}
