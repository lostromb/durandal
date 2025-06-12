using Durandal.API;
using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Codecs.Opus.Enums;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs
{
    /// <summary>
    /// Encoder which encodes to a stream of self-delimited Opus audio packets.
    /// </summary>
    public sealed class OpusRawEncoder : AudioEncoder
    {
        private static readonly byte[] CHANNEL_MAPPING_VORBIS_QUAD = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        private static readonly byte[] CHANNEL_MAPPING_VORBIS_5_1 = new byte[] { 0x00, 0x04, 0x01, 0x02, 0x03, 0x05 };

        private const int MAX_OPUS_PACKET_SIZE = 1275;
        private readonly IOpusEncoder _encoder = null;
        private readonly ILogger _logger;
        private readonly float[] _frameBuffer;
        private readonly int _frameLengthSamplesPerChannel;
        private readonly byte[] _outputPacketScratchBuffer;
        private readonly TimeSpan _frameSizePerChannel;
        private int _samplesPerChannelInBuffer;
        private int _disposed = 0;

        /// <summary>Constructs a new Opus encoder</summary>
        /// <param name="graph">The audio graph that this component is a part of.</param>
        /// <param name="format">The audio sample format of the input</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="logger">A logger for codec messages</param>
        /// <param name="complexity">The encoder complexity, from 0 to 10</param>
        /// <param name="bitrateKbps">The target bitrate of the output data. Around 40 is fine for speech, 128 is nearly lossless for most audio</param>
        /// <param name="forceMode">Forces a specific mode in the opus encoder; can be used as a speed optimization for advanced use</param>
        /// <param name="audioTypeHint">Hint to the encoder of which type of audio is being processed</param>
        /// <param name="frameSize">The frame size of output packets; used to trade off latency vs. quality.</param>
        /// <returns>A newly constructed object.</returns>
        public OpusRawEncoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            ILogger logger = null,
            int complexity = 0,
            int bitrateKbps = 40,
            OpusMode forceMode = OpusMode.MODE_AUTO,
            OpusApplication audioTypeHint = OpusApplication.OPUS_APPLICATION_VOIP,
            OpusFramesize frameSize = OpusFramesize.OPUS_FRAMESIZE_5_MS)
            : base(graph, format, nameof(OpusRawEncoder), nodeCustomName)
        {
            _logger = logger ?? NullLogger.Singleton;

            // Assert that the input format conforms to what Opus allows
            if (bitrateKbps < 6 || bitrateKbps > 510)
            {
                throw new ArgumentOutOfRangeException("Opus bitrate must be between 6 and 510 Kbps");
            }

            if (complexity < 0 || complexity > 10)
            {
                throw new ArgumentOutOfRangeException("Opus complexity ranges from 0 to 10");
            }

            AssertSampleRateIsValidForOpus(InputFormat.SampleRateHz);
            _frameSizePerChannel = ConvertFrameSizeToTimeSpan(frameSize);
            _frameLengthSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, _frameSizePerChannel);
            _frameBuffer = new float[_frameLengthSamplesPerChannel * format.NumChannels];
            _samplesPerChannelInBuffer = 0;

            _logger.LogFormat(LogLevel.Std, DataPrivacyClassification.SystemMetadata, "Initializing Opus compression stream with samplerate={0}, bitrate={1}, complexity={2} layout={3}",
                format.SampleRateHz,
                bitrateKbps,
                complexity,
                (int)format.ChannelMapping);

            if (format.NumChannels <= 2)
            {
                _encoder = OpusCodecFactory.Provider.CreateEncoder(format.SampleRateHz, format.NumChannels, audioTypeHint);
            }
            else
            {
                int streams;
                int coupledStreams;
                if (format.ChannelMapping == MultiChannelMapping.Quadraphonic)
                {
                    _encoder = OpusCodecFactory.Provider.CreateMultistreamEncoder(format.SampleRateHz, format.NumChannels, CHANNEL_MAPPING_VORBIS_QUAD, out streams, out coupledStreams, audioTypeHint);
                }
                if (format.ChannelMapping == MultiChannelMapping.Surround_5_1ch_Vorbis_Layout)
                {
                    _encoder = OpusCodecFactory.Provider.CreateMultistreamEncoder(format.SampleRateHz, format.NumChannels, CHANNEL_MAPPING_VORBIS_5_1, out streams, out coupledStreams, audioTypeHint);
                }
                else
                {
                    throw new ArgumentException($"Opus codec does not support channel mapping {format.ChannelMapping}");
                }
            }

            _encoder.Bitrate = bitrateKbps * 1024;
            _encoder.Complexity = complexity;
            _encoder.UseVBR = true;
            if (forceMode != OpusMode.MODE_AUTO)
            {
                _encoder.ForceMode = forceMode;
            }

            _outputPacketScratchBuffer = new byte[MAX_OPUS_PACKET_SIZE + 2];
        }

        public override string Codec => OpusRawCodecFactory.CODEC_NAME;

        public override string CodecParams => CreateCodecParams(InputFormat);

        public override Task<AudioInitializationResult> Initialize(NonRealTimeStream outputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(OpusRawEncoder));
            }

            if (IsInitialized)
            {
                return Task.FromResult(AudioInitializationResult.Already_Initialized);
            }

            OutputStream = outputStream.AssertNonNull(nameof(outputStream));
            OwnsStream = ownsStream;
            IsInitialized = true;
            return Task.FromResult(AudioInitializationResult.Success);
        }

        protected override async ValueTask FinishInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(OpusRawEncoder));
            }

            // Pad frame with silence if there's anything left in the buffer
            if (_samplesPerChannelInBuffer > 0)
            {
                ArrayExtensions.WriteZeroes(_frameBuffer,
                    _samplesPerChannelInBuffer * InputFormat.NumChannels,
                    (_frameLengthSamplesPerChannel - _samplesPerChannelInBuffer) * InputFormat.NumChannels);
                short thisPacketSize = (short)_encoder.Encode(_frameBuffer, 0, _frameLengthSamplesPerChannel, _outputPacketScratchBuffer, 2, MAX_OPUS_PACKET_SIZE);
                BinaryHelpers.Int16ToByteArrayLittleEndian(thisPacketSize, _outputPacketScratchBuffer, 0); // Add 2-byte length prefix
                await OutputStream.WriteAsync(_outputPacketScratchBuffer, 0, thisPacketSize + 2, cancelToken, realTime).ConfigureAwait(false);
                _samplesPerChannelInBuffer = 0;
            }
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(OpusRawEncoder));
            }

            int samplesPerChannelReadFromInput = 0;
            while (samplesPerChannelReadFromInput < count)
            {
                // Try and fill the frame.
                int samplesPerChannelCanReadFromInput = FastMath.Min(_frameLengthSamplesPerChannel - _samplesPerChannelInBuffer, count - samplesPerChannelReadFromInput);
                if (samplesPerChannelCanReadFromInput > 0)
                {
                    ArrayExtensions.MemCopy(
                        buffer,
                        (offset + (samplesPerChannelReadFromInput * InputFormat.NumChannels)),
                        _frameBuffer,
                        _samplesPerChannelInBuffer * InputFormat.NumChannels,
                        samplesPerChannelCanReadFromInput * InputFormat.NumChannels);
                    samplesPerChannelReadFromInput += samplesPerChannelCanReadFromInput;
                    _samplesPerChannelInBuffer += samplesPerChannelCanReadFromInput;
                }

                // If frame is full, encode an opus packet
                if (_samplesPerChannelInBuffer == _frameLengthSamplesPerChannel)
                {
                    short thisPacketSize = (short)_encoder.Encode(_frameBuffer, 0, _frameLengthSamplesPerChannel, _outputPacketScratchBuffer, 2, MAX_OPUS_PACKET_SIZE);
                    BinaryHelpers.Int16ToByteArrayLittleEndian(thisPacketSize, _outputPacketScratchBuffer, 0); // Add 2-byte length prefix
                    await OutputStream.WriteAsync(_outputPacketScratchBuffer, 0, thisPacketSize + 2, cancelToken, realTime).ConfigureAwait(false);
                    _samplesPerChannelInBuffer = 0;
                }
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
                    _encoder?.Dispose();
                }
            }
            catch (Exception e)
            {
                // don't let exceptions in the native layer kill the finalizer thread
                _logger.Log(e);
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private string CreateCodecParams(AudioSampleFormat format)
        {
            return string.Format("samplerate={0} q={1} framesize={2} channels={3} layout={4}",
                format.SampleRateHz,
                _encoder.Complexity,
                _frameSizePerChannel.TotalMilliseconds,
                format.NumChannels,
                (int)format.ChannelMapping);
        }

        private static void AssertSampleRateIsValidForOpus(int sampleRate)
        {
            if (sampleRate != 8000 &&
                sampleRate != 12000 &&
                sampleRate != 16000 &&
                sampleRate != 24000 &&
                sampleRate != 48000)
            {
                throw new ArgumentOutOfRangeException("Opus codec can only operate at 8, 12, 16, 24, or 48 Khz sample rates");
            }
        }

        private static TimeSpan ConvertFrameSizeToTimeSpan(OpusFramesize frameSize)
        {
            switch (frameSize)
            {
                case OpusFramesize.OPUS_FRAMESIZE_2_5_MS:
                    return TimeSpan.FromMilliseconds(2.5);
                case OpusFramesize.OPUS_FRAMESIZE_5_MS:
                    return TimeSpan.FromMilliseconds(5);
                case OpusFramesize.OPUS_FRAMESIZE_10_MS:
                    return TimeSpan.FromMilliseconds(10);
                case OpusFramesize.OPUS_FRAMESIZE_20_MS:
                    return TimeSpan.FromMilliseconds(20);
                case OpusFramesize.OPUS_FRAMESIZE_40_MS:
                    return TimeSpan.FromMilliseconds(40);
                case OpusFramesize.OPUS_FRAMESIZE_60_MS:
                    return TimeSpan.FromMilliseconds(60);
                case OpusFramesize.OPUS_FRAMESIZE_ARG:
                    return TimeSpan.FromMilliseconds(20);
                default:
                    throw new ArgumentException($"Opus framesize {frameSize} is not supported");
            }
        }
    }
}
