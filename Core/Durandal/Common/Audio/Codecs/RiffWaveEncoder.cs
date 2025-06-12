using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Tasks;
using System.IO;
using Durandal.Common.Utils;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.Audio.Components;
using Durandal.Common.Audio.Codecs.ADPCM;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Audio.Codecs
{
    /// <summary>
    /// Encoder which writes out audio samples to .wav-formatted stream. Usually PCM16 but can also support 24-bit, float32, ulaw, etc.
    /// </summary>
    public sealed class RiffWaveEncoder : AudioEncoder
    {
        private readonly RiffWaveFormat _sampleFormat;
        private readonly AudioEncoder _innerEncoder;
        private readonly ILogger _logger;
        private ushort? _adpcmBlockSize;
        private long _fileStart = 0; // Seek offset from the origin that the wav file starts. Will be non-zero only if the stream is initially non-empty.
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="RiffWaveEncoder"/>.
        /// </summary>
        /// <param name="graph">The audio graph that this component is a part of.</param>
        /// <param name="format">The audio sample format of the input</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="logger">A logger</param>
        /// <param name="sampleFormat">The actual encoding format to use for individual samples.</param>
        /// <returns>A newly constructed object.</returns>
        public RiffWaveEncoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            ILogger logger,
            RiffWaveFormat sampleFormat = RiffWaveFormat.PCM_S16LE)
            : base(graph, format, nameof(RiffWaveEncoder), nodeCustomName)
        {
            _logger = logger.AssertNonNull(nameof(logger));
            _sampleFormat = sampleFormat;
            switch (_sampleFormat)
            {
                case RiffWaveFormat.PCM_S16LE:
                case RiffWaveFormat.PCM_S24LE:
                case RiffWaveFormat.PCM_S32LE:
                case RiffWaveFormat.PCM_F32LE:
                    _innerEncoder = new RawPcmEncoder(graph, format, null, _sampleFormat);
                    break;
                case RiffWaveFormat.ALAW:
                    _innerEncoder = new ALawEncoder(graph, format, null);
                    break;
                case RiffWaveFormat.ULAW:
                    _innerEncoder = new ULawEncoder(graph, format, null);
                    break;
                case RiffWaveFormat.ADPCM_IMA:
                    _innerEncoder = new AdpcmEncoder(graph, format, null);
                    _adpcmBlockSize = (ushort)((AdpcmEncoder)_innerEncoder).BlockSizeBytes;
                    if (format.NumChannels > 2)
                    {
                        _logger.Log("Encoding more than 2 channels ADPCM is proprietary and likely won't work with most other decoders", LogLevel.Wrn);
                    }
                    break;
                default:
                    throw new ArgumentException($"RIFF encoder format {sampleFormat} is not supported");
            }
        }

        /// <inheritdoc />
        public override string Codec
        {
            get
            {
                if (_innerEncoder == null)
                {
                    return "riff";
                }
                else
                {
                    return $"riff-{_innerEncoder.Codec}";
                }
            }
        }

        /// <inheritdoc />
        public override string CodecParams => string.Empty;

        /// <inheritdoc />
        public override async Task<AudioInitializationResult> Initialize(NonRealTimeStream outputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (IsInitialized)
            {
                return AudioInitializationResult.Already_Initialized;
            }

            OutputStream = outputStream.AssertNonNull(nameof(outputStream));
            OwnsStream = ownsStream;

            // Write the riff header to the stream
            _fileStart = OutputStream.Position;
            byte[] riffHeader = BuildRiffHeader(0, InputFormat, _sampleFormat, _adpcmBlockSize);
            await OutputStream.WriteAsync(riffHeader, 0, riffHeader.Length, cancelToken, realTime).ConfigureAwait(false);

            // And initialize the inner codec with a handle to the output stream
            AudioInitializationResult innerEncoderResult = await _innerEncoder.Initialize(OutputStream, ownsStream: false, cancelToken, realTime).ConfigureAwait(false);

            if (innerEncoderResult == AudioInitializationResult.Success)
            {
                IsInitialized = true;
                return AudioInitializationResult.Success;
            }
            else
            {
                return innerEncoderResult;
            }
        }

        /// <inheritdoc />
        protected override async ValueTask FinishInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await _innerEncoder.Finish(cancelToken, realTime).ConfigureAwait(false);

            // Seek back to the beginning of the stream (if possible) and update the riff header
            if (OutputStream.CanSeek)
            {
                long endOfFilePos = OutputStream.Position;
                OutputStream.Seek(_fileStart, SeekOrigin.Begin);
                long startOfFilePos = OutputStream.Position;
                byte[] riffHeader = BuildRiffHeader((uint)(endOfFilePos - startOfFilePos), InputFormat, _sampleFormat, _adpcmBlockSize);
                await OutputStream.WriteAsync(riffHeader, 0, riffHeader.Length, cancelToken, realTime).ConfigureAwait(false);
                OutputStream.Seek(0, SeekOrigin.End);
            }
        }

        /// <inheritdoc />
        protected override ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _innerEncoder.WriteAsync(buffer, offset, count, cancelToken, realTime);
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
                    _innerEncoder?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Returns a riff header with the proper header values set.
        /// </summary>
        /// <param name="fileLengthBytes">The total raw size of the output file, including headers</param>
        /// <param name="format">The audio samplerate and channel layout</param>
        /// <param name="sampleFormat">The raw sample format being encoded</param>
        /// <param name="adpcmBlockSize">(For Adpcm-xq only) The block size used by the codec, in bytes</param>
        /// <returns>A byte array representing a RIFF header</returns>
        private static byte[] BuildRiffHeader(
            uint fileLengthBytes,
            AudioSampleFormat format,
            RiffWaveFormat sampleFormat,
            ushort? adpcmBlockSize)
        {
            bool needWaveFormatExtensible = format.ChannelMapping != MultiChannelMapping.Monaural && format.ChannelMapping != MultiChannelMapping.Stereo_L_R;
            byte[] header;

            if (needWaveFormatExtensible)
            {
                // 68-bytes extensible header
                // https://learn.microsoft.com/en-us/windows/win32/api/mmreg/ns-mmreg-waveformatextensible
                header = new byte[] {
                    0x52, 0x49, 0x46, 0x46,     // 00-03 "RIFF"
                    0x00, 0x00, 0x00, 0x00,     // 04-07 Total file size
                    0x57, 0x41, 0x56, 0x45,     // 08-13 "WAVE"
                    0x66, 0x6D, 0x74, 0x20,     // 12-15 "fmt "
                    0x28, 0x00, 0x00, 0x00,     // 16-19 Length of fmt data below (up until "data") (40 bytes)
                    // begin WAVEFORMATEXTENSIBLE
                    0x00, 0x00,                 // 20-21 PCM = mode 1, see RiffWaveConstants.WAVEFORMATTAG_PCM
                    0x00, 0x00,                 // 22-23 Channel count
                    0x00, 0x00, 0x00, 0x00,     // 24-27 Sample rate
                    0x00, 0x00, 0x00, 0x00,     // 28-31 Data rate bytes / sec
                    0x00, 0x00,                 // 32-33 Block alignment - how big is a single "frame" of audio
                    0x00, 0x00,                 // 34-35 Bits per sample
                    0x16, 0x00,                 // 36-37 cbSize - size of extra format information. For WAVEFORMATEXTENSIBLE this is 22
                    0x00, 0x00,                 // 38-39 validBitsPerSample
                    0x00, 0x00, 0x00, 0x00,     // 40-43 channelMask
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // 44-59 Format GUID
                    // end WAVEFORMATEXTENSIBLE
                    0x64, 0x61, 0x74, 0x61,     // 60-63 "data"
                    0x00, 0x00, 0x00, 0x00      // 64-67 Size of data section
                };

                ushort bitsPerSample = GetBitsPerSample(sampleFormat);

                // Size of the overall file - 8 bytes, in bytes (32-bit integer). Typically, you'd fill this in after creation. 
                BinaryHelpers.UInt32ToByteArrayLittleEndian((uint)(fileLengthBytes - 8), header, 4);
                // Subformat - 2 byte integer 
                BinaryHelpers.UInt16ToByteArrayLittleEndian(GetSubformatCode(sampleFormat), header, 20);
                // Number of Channels - 2 byte integer 
                BinaryHelpers.UInt16ToByteArrayLittleEndian((ushort)format.NumChannels, header, 22);
                // Sample Rate - 32 byte integer
                BinaryHelpers.UInt32ToByteArrayLittleEndian((uint)format.SampleRateHz, header, 24);
                // Data Rate in Bytes/sec = (Sample Rate * BitsPerSample * Channels) / 8.
                BinaryHelpers.UInt32ToByteArrayLittleEndian((uint)(format.SampleRateHz * format.NumChannels * bitsPerSample / 8), header, 28);
                // Frame size. Adpcm-xq encodes in blocks so it's special
                if (sampleFormat == RiffWaveFormat.ADPCM_IMA)
                {
                    BinaryHelpers.UInt16ToByteArrayLittleEndian(adpcmBlockSize.Value, header, 32);
                }
                else
                {
                    // For other PCM formats it's just the size of one frame across all channels.
                    // 1 = 8 bit mono, 2 = 16 bit mono or 8 bit stereo, 4 = 16 bit stereo.
                    BinaryHelpers.UInt16ToByteArrayLittleEndian((ushort)(bitsPerSample * format.NumChannels / 8), header, 32);
                }
                // Bits per sample and valid bits per sample
                BinaryHelpers.UInt16ToByteArrayLittleEndian(bitsPerSample, header, 34);
                BinaryHelpers.UInt16ToByteArrayLittleEndian(bitsPerSample, header, 38);
                // Channel mask specifying the speaker layout
                BinaryHelpers.UInt32ToByteArrayLittleEndian(GetChannelMask(format.ChannelMapping), header, 40);
                // Format GUID
                switch (sampleFormat)
                {
                    case RiffWaveFormat.PCM_S16LE:
                    case RiffWaveFormat.PCM_S24LE:
                    case RiffWaveFormat.PCM_S32LE:
                        RiffWaveConstants.KSDATAFORMAT_SUBTYPE_PCM.AsSpan().CopyTo(header.AsSpan(44, 16));
                        break;
                    case RiffWaveFormat.PCM_F32LE:
                        RiffWaveConstants.KSDATAFORMAT_SUBTYPE_IEEE_FLOAT.AsSpan().CopyTo(header.AsSpan(44, 16));
                        break;
                    case RiffWaveFormat.ADPCM_IMA:
                        RiffWaveConstants.KSDATAFORMAT_SUBTYPE_IMA_ADPCM.AsSpan().CopyTo(header.AsSpan(44, 16));
                        break;
                    case RiffWaveFormat.ALAW:
                        RiffWaveConstants.KSDATAFORMAT_SUBTYPE_ALAW.AsSpan().CopyTo(header.AsSpan(44, 16));
                        break;
                    case RiffWaveFormat.ULAW:
                        RiffWaveConstants.KSDATAFORMAT_SUBTYPE_ULAW.AsSpan().CopyTo(header.AsSpan(44, 16));
                        break;
                    default:
                        throw new ArgumentException($"Subformat GUID for {sampleFormat} is not known");
                }

                // Size of the data section.
                BinaryHelpers.UInt32ToByteArrayLittleEndian(fileLengthBytes - (uint)header.Length, header, 64);
            }
            else
            {
                // 44-byte boilerplate header
                // http://www.topherlee.com/software/pcm-tut-wavformat.html
                header = new byte[] {
                    0x52, 0x49, 0x46, 0x46,     // 00-03 "RIFF"
                    0x00, 0x00, 0x00, 0x00,     // 04-07 Total file size
                    0x57, 0x41, 0x56, 0x45,     // 08-13 "WAVE"
                    0x66, 0x6D, 0x74, 0x20,     // 12-15 "fmt "
                    0x10, 0x00, 0x00, 0x00,     // 16-19 Length of fmt data below (up until "data") (16 bytes)
                    // begin WAVEFORMAT
                    0x00, 0x00,                 // 20-21 PCM = mode 1, see RiffWaveConstants.WAVEFORMATTAG_PCM
                    0x00, 0x00,                 // 22-23 Channel count
                    0x00, 0x00, 0x00, 0x00,     // 24-27 Sample rate
                    0x00, 0x00, 0x00, 0x00,     // 28-31 Data rate bytes / sec
                    0x00, 0x00,                 // 32-33 Block alignment - how big is a single "frame" of audio
                    0x00, 0x00,                 // 34-35 Bits per sample
                    // end WAVEFORMAT
                    0x64, 0x61, 0x74, 0x61,     // 36-39 "data"
                    0x00, 0x00, 0x00, 0x00      // 40-43 Size of data section
                };

                ushort bitsPerSample = GetBitsPerSample(sampleFormat);

                // Size of the overall file - 8 bytes, in bytes (32-bit integer). Typically, you'd fill this in after creation. 
                BinaryHelpers.UInt32ToByteArrayLittleEndian((uint)(fileLengthBytes - 8), header, 4);
                // Subformat - 2 byte integer 
                BinaryHelpers.UInt16ToByteArrayLittleEndian(GetSubformatCode(sampleFormat), header, 20);
                // Number of Channels - 2 byte integer 
                BinaryHelpers.UInt16ToByteArrayLittleEndian((ushort)format.NumChannels, header, 22);
                // Sample Rate - 32 byte integer
                BinaryHelpers.UInt32ToByteArrayLittleEndian((uint)format.SampleRateHz, header, 24);
                // Data Rate in Bytes/sec = (Sample Rate * BitsPerSample * Channels) / 8.
                BinaryHelpers.UInt32ToByteArrayLittleEndian((uint)(format.SampleRateHz * format.NumChannels * bitsPerSample / 8), header, 28);
                // Frame size. Adpcm-xq encodes in blocks so it's special
                if (sampleFormat == RiffWaveFormat.ADPCM_IMA)
                {
                    BinaryHelpers.UInt16ToByteArrayLittleEndian(adpcmBlockSize.Value, header, 32);
                }
                else
                {
                    // For other PCM formats it's just the size of one frame across all channels.
                    // 1 = 8 bit mono, 2 = 16 bit mono or 8 bit stereo, 4 = 16 bit stereo.
                    BinaryHelpers.UInt16ToByteArrayLittleEndian((ushort)(bitsPerSample * format.NumChannels / 8), header, 32);
                }
                // Bits per sample
                BinaryHelpers.UInt16ToByteArrayLittleEndian(bitsPerSample, header, 34);
                // Size of the data section.
                BinaryHelpers.UInt32ToByteArrayLittleEndian(fileLengthBytes - (uint)header.Length, header, 40);
            }

            return header;
        }

        private static ushort GetBitsPerSample(RiffWaveFormat sampleFormat)
        {
            switch (sampleFormat)
            {
                case RiffWaveFormat.PCM_S16LE:
                    return 16;
                case RiffWaveFormat.PCM_S24LE:
                    return 24;
                case RiffWaveFormat.PCM_S32LE:
                case RiffWaveFormat.PCM_F32LE:
                    return 32;
                case RiffWaveFormat.ULAW:
                case RiffWaveFormat.ALAW:
                    return 8;
                case RiffWaveFormat.ADPCM_IMA:
                    return 4;
                default:
                    throw new ArgumentException($"Unknown RIFF sample format {sampleFormat}");
            }
        }

        private static ushort GetSubformatCode(RiffWaveFormat sampleFormat)
        {
            switch (sampleFormat)
            {
                case RiffWaveFormat.PCM_S16LE:
                case RiffWaveFormat.PCM_S24LE:
                case RiffWaveFormat.PCM_S32LE:
                    return RiffWaveConstants.WAVEFORMATTAG_PCM;
                case RiffWaveFormat.PCM_F32LE:
                    return RiffWaveConstants.WAVEFORMATTAG_IEEE_FLOAT;
                case RiffWaveFormat.ULAW:
                    return RiffWaveConstants.WAVEFORMATTAG_ULAW;
                case RiffWaveFormat.ALAW:
                    return RiffWaveConstants.WAVEFORMATTAG_ALAW;
                case RiffWaveFormat.ADPCM_IMA:
                    return RiffWaveConstants.WAVEFORMATTAG_IMA_ADPCM;
                default:
                    throw new ArgumentException($"Unknown RIFF sample format {sampleFormat}");
            }
        }

        /// <summary>
        /// Calculates the WAVEFORMATEXTENSIBLE channel mask for the given speaker layout.
        /// </summary>
        /// <param name="channelMapping">The channel mapping</param>
        /// <returns>A 32-bit channel mask.</returns>
        private static uint GetChannelMask(MultiChannelMapping channelMapping)
        {
            switch (channelMapping)
            {
                case MultiChannelMapping.Monaural:
                    return RiffWaveConstants.SPEAKER_LAYOUT_MONO;
                case MultiChannelMapping.Stereo_L_R:
                    return RiffWaveConstants.SPEAKER_LAYOUT_STEREO;
                case MultiChannelMapping.Quadraphonic:
                    return RiffWaveConstants.SPEAKER_LAYOUT_QUAD;
                case MultiChannelMapping.Quadraphonic_rear:
                    return RiffWaveConstants.SPEAKER_LAYOUT_SURROUND;
                case MultiChannelMapping.Surround_5_1ch:
                    return RiffWaveConstants.SPEAKER_LAYOUT_5_1;
                case MultiChannelMapping.Surround_5_1ch_side:
                    return RiffWaveConstants.SPEAKER_LAYOUT_5_1_SIDE;
                case MultiChannelMapping.Surround_7_1ch:
                    return RiffWaveConstants.SPEAKER_LAYOUT_7_1;
                case MultiChannelMapping.Surround_7_1ch_side:
                    return RiffWaveConstants.SPEAKER_LAYOUT_7_1_SIDE;
                default:
                    return RiffWaveConstants.SPEAKER_LAYOUT_PACKED;
            }
        }
    }
}
