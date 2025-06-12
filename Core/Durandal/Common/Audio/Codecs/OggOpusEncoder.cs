using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Codecs.Opus.Enums;
using Durandal.Common.Audio.Codecs.Opus.Ogg;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs
{
    /// <summary>
    /// Encoder which encodes to a Ogg file containing a single Opus stream.
    /// </summary>
    public class OggOpusEncoder : AudioEncoder
    {
        //static const VorbisLayout vorbis_mappings[8] = {
        //      {1, 0, {0}},                      /* 1: mono */
        //      {1, 1, {0, 1}},                   /* 2: stereo */
        //      {2, 1, {0, 2, 1}},                /* 3: 1-d surround */
        //      {2, 2, {0, 1, 2, 3}},             /* 4: quadraphonic surround */
        //      {3, 2, {0, 4, 1, 2, 3}},          /* 5: 5-channel surround */
        //      {4, 2, {0, 4, 1, 2, 3, 5}},       /* 6: 5.1 surround */
        //      {4, 3, {0, 4, 1, 2, 3, 5, 6}},    /* 7: 6.1 surround */
        //      {5, 3, {0, 6, 1, 2, 3, 4, 5, 7}}, /* 8: 7.1 surround */
        //};

        private static readonly byte[] CHANNEL_MAPPING_VORBIS_QUAD = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        private static readonly byte[] CHANNEL_MAPPING_VORBIS_5 = new byte[] { 0x00, 0x04, 0x01, 0x02, 0x03 };
        private static readonly byte[] CHANNEL_MAPPING_VORBIS_5_1 = new byte[] { 0x00, 0x04, 0x01, 0x02, 0x03, 0x05 };
        private static readonly byte[] CHANNEL_MAPPING_VORBIS_6_1 = new byte[] { 0x00, 0x04, 0x01, 0x02, 0x03, 0x05, 0x06 };
        private static readonly byte[] CHANNEL_MAPPING_VORBIS_7_1 = new byte[] { 0x00, 0x06, 0x01, 0x02, 0x03, 0x04, 0x05, 0x07 };

        private const int SCRATCH_BUFFER_LENGTH_PER_CHANNEL = 1024;
        private readonly ILogger _logger;
        private readonly float[] _scratchBuffer;
        private readonly TimeSpan? _oggPageSize;
        private IOpusEncoder _encoder; // ownership of this object is passed to write stream after a successful call to Initialize()
        private int _disposed = 0;

        private OpusOggWriteStream _writeStream;

        public override string Codec => OggOpusCodecFactory.CODEC_NAME;

        /// <summary>
        /// Creates a new OggOpusEncoder.
        /// </summary>
        /// <param name="graph">The audio graph that this encoder will be a part of</param>
        /// <param name="format">The input audio format</param>
        /// <param name="nodeCustomName">The name of this node in the audio graph, or null</param>
        /// <param name="logger">A logger</param>
        /// <param name="complexity">The opus encoder complexity from 0 to 10</param>
        /// <param name="bitrateKbps">The encoder bitrate in kilobits per second</param>
        /// <param name="forceMode">(Advanced) Force Opus encoder into a specific mode, see opus docs for information</param>
        /// <param name="audioTypeHint">Give a hint to the Opus encoder for the type of audio signal being encoded.</param>
        /// <param name="oggPageLength">Specifies the maximum length of audio that can be included in a single Ogg page.
        /// By default, the encoder can pack 4 - 10 seconds of audio into a page, depending on bitrate. However, this
        /// can translate into streaming latency and can also cause quirks with seeking and playback percentage calculation.
        /// Setting this value to something small can alleviate these issues at the expense of container size overhead.</param>
        public OggOpusEncoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            ILogger logger = null,
            int complexity = 2,
            int bitrateKbps = 96,
            OpusMode forceMode = OpusMode.MODE_AUTO,
            OpusApplication audioTypeHint = OpusApplication.OPUS_APPLICATION_AUDIO,
            TimeSpan? oggPageLength = null)
            : base(graph, format, nameof(OggOpusEncoder), nodeCustomName)
        {
            _logger = logger ?? NullLogger.Singleton;
            _oggPageSize = oggPageLength;

            // Assert that the input format conforms to what Opus allows
            if (bitrateKbps < 6 || bitrateKbps > 510)
            {
                throw new ArgumentOutOfRangeException("Opus bitrate must be between 6 and 510 Kbps");
            }

            AssertSampleRateIsValidForOpus(InputFormat.SampleRateHz);

            _logger.Log($"Initializing OggOpus compression stream with samplerate={format.SampleRateHz}, bitrate={bitrateKbps}, complexity={complexity}");
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
                else if (format.ChannelMapping == MultiChannelMapping.Surround_5ch_Vorbis_Layout)
                {
                    _encoder = OpusCodecFactory.Provider.CreateMultistreamEncoder(format.SampleRateHz, format.NumChannels, CHANNEL_MAPPING_VORBIS_5, out streams, out coupledStreams, audioTypeHint);
                }
                else if (format.ChannelMapping == MultiChannelMapping.Surround_5_1ch_Vorbis_Layout)
                {
                    _encoder = OpusCodecFactory.Provider.CreateMultistreamEncoder(format.SampleRateHz, format.NumChannels, CHANNEL_MAPPING_VORBIS_5_1, out streams, out coupledStreams, audioTypeHint);
                }
                //else if (format.ChannelMapping == MultiChannelMapping.Surround_6_1ch_Vorbis_Layout)
                //{
                //    _encoder = OpusCodecFactory.Provider.CreateMultistreamEncoder(format.SampleRateHz, format.NumChannels, CHANNEL_MAPPING_VORBIS_6_1, out streams, out coupledStreams, audioTypeHint);
                //}
                else if (format.ChannelMapping == MultiChannelMapping.Surround_7_1ch_Vorbis_Layout)
                {
                    _encoder = OpusCodecFactory.Provider.CreateMultistreamEncoder(format.SampleRateHz, format.NumChannels, CHANNEL_MAPPING_VORBIS_7_1, out streams, out coupledStreams, audioTypeHint);
                }
                else
                {
                    throw new NotImplementedException("Encoding multistream OggOpus is currently only supported for quadraphonic or 5.1 (vorbis) input formats");
                }
            }

            _encoder.Bitrate = bitrateKbps * 1024;
            _encoder.Complexity = complexity;
            _encoder.UseVBR = true;
            if (forceMode != OpusMode.MODE_AUTO)
            {
                _encoder.ForceMode = forceMode;
            }
            
            _scratchBuffer = new float[format.NumChannels * SCRATCH_BUFFER_LENGTH_PER_CHANNEL];
        }

        public override async Task<AudioInitializationResult> Initialize(NonRealTimeStream outputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (IsInitialized)
            {
                return AudioInitializationResult.Already_Initialized;
            }

            OutputStream = outputStream.AssertNonNull(nameof(outputStream));
            OwnsStream = ownsStream;
            _writeStream = await OpusOggWriteStream.Create(_encoder, InputFormat, cancelToken, realTime, OutputStream, OwnsStream).ConfigureAwait(false);
            
            if (_oggPageSize.HasValue)
            {
                _writeStream.MaxAudioLengthPerPage = _oggPageSize.Value;
            }

            _encoder = null;
            IsInitialized = true;
            return AudioInitializationResult.Success;
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
                    _writeStream?.Dispose();
                    _encoder?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        protected override async ValueTask FinishInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await _writeStream.Finish(cancelToken, realTime);
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(OggOpusEncoder));
            }
            
            int samplesPerChannelReadFromInput = 0;
            while (samplesPerChannelReadFromInput < count)
            {
                // Read into scratch buffer
                int blockSizeSamplesPerChannel = Math.Min(SCRATCH_BUFFER_LENGTH_PER_CHANNEL, count - samplesPerChannelReadFromInput);
                int readOffset = (offset + (samplesPerChannelReadFromInput * InputFormat.NumChannels));
                int readSize = blockSizeSamplesPerChannel * InputFormat.NumChannels;
                ArrayExtensions.MemCopy(buffer, readOffset, _scratchBuffer, 0, readSize);
                await _writeStream.WriteSamples(_scratchBuffer, 0, blockSizeSamplesPerChannel, cancelToken, realTime).ConfigureAwait(false);
                samplesPerChannelReadFromInput += blockSizeSamplesPerChannel;
            }
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
    }
}
