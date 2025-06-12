using Durandal.Common.Audio.Codecs.ADPCM;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs
{
    /// <summary>
    /// Audio codec which reads from a .wav formatted (RIFF) stream.
    /// </summary>
    public sealed class RiffWaveDecoder : AudioDecoder
    {
        private RiffDecoder _riffDecoder;
        private AudioDecoder _innerDataDecoder;
        private int _disposed = 0;

        /// <summary>
        /// Constructs a new <see cref="RiffWaveDecoder"/> 
        /// </summary>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <returns>A newly created <see cref="RiffWaveDecoder"/></returns>
        public RiffWaveDecoder(WeakPointer<IAudioGraph> graph, string nodeCustomName)
            : base("riff", graph, nameof(RiffWaveDecoder), nodeCustomName)
        {
            OutputFormat = null;
        }

        public override bool PlaybackFinished => _innerDataDecoder == null ? false : _innerDataDecoder.PlaybackFinished;

        public override string CodecDescription => "Uncompressed PCM .wav";

        public override async Task<AudioInitializationResult> Initialize(NonRealTimeStream inputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (IsInitialized)
            {
                return AudioInitializationResult.Already_Initialized;
            }

            InputStream = inputStream.AssertNonNull(nameof(inputStream));
            OwnsStream = ownsStream;
            _riffDecoder = new RiffDecoder(InputStream, ownsStream);
            bool initializeOk = await _riffDecoder.InitializeAsync(cancelToken, realTime).ConfigureAwait(false);
            if (!initializeOk)
            {
                return AudioInitializationResult.Failure_StreamEnded;
            }

            // Assume the first block is the "fmt " block
            if (!string.Equals("fmt ", _riffDecoder.CurrentBlockName) ||
                _riffDecoder.CurrentBlockLength < 16)
            {
                return AudioInitializationResult.Failure_BadFormat;
            }

            RiffWaveFormat dataFormat = RiffWaveFormat.Unknown;
            byte[] headerBuffer = new byte[_riffDecoder.CurrentBlockLength];
            int headerBytesRead = 0;
            while (headerBytesRead < headerBuffer.Length)
            {
                int amountRead = await _riffDecoder.ReadFromCurrentBlockAsync(headerBuffer, headerBytesRead, _riffDecoder.CurrentBlockLength - headerBytesRead, cancelToken, realTime).ConfigureAwait(false);
                if (amountRead <= 0)
                {
                    return AudioInitializationResult.Failure_StreamEnded;
                }
                else
                {
                    headerBytesRead += amountRead;
                }
            }

            bool isWaveFormatEx = headerBytesRead >= 18;
            bool isWaveFormatExtensible = headerBytesRead >= 24;

            // Parse header if we are finished
            ushort formatTag = BinaryHelpers.ByteArrayToUInt16LittleEndian(headerBuffer, 0);
            short numChannels = BinaryHelpers.ByteArrayToInt16LittleEndian(headerBuffer, 2);
            int sampleRate = BinaryHelpers.ByteArrayToInt32LittleEndian(headerBuffer, 4);
            short bytesPerBlock = BinaryHelpers.ByteArrayToInt16LittleEndian(headerBuffer, 12);
            short bitsPerSample = BinaryHelpers.ByteArrayToInt16LittleEndian(headerBuffer, 14);

            switch (formatTag)
            {
                case RiffWaveConstants.WAVEFORMATTAG_PCM:
                    if (bitsPerSample == 16)
                        dataFormat = RiffWaveFormat.PCM_S16LE;
                    else if (bitsPerSample == 24)
                        dataFormat = RiffWaveFormat.PCM_S24LE;
                    else if (bitsPerSample == 32)
                        dataFormat = RiffWaveFormat.PCM_S32LE;
                    else
                        throw new NotSupportedException($"Integer PCM does not support {bitsPerSample} bits per sample");
                    break;
                case RiffWaveConstants.WAVEFORMATTAG_IEEE_FLOAT:
                    if (bitsPerSample == 32)
                        dataFormat = RiffWaveFormat.PCM_F32LE;
                    else
                        throw new NotSupportedException($"IEEE Float PCM does not support {bitsPerSample} bits per sample");
                    break;
                case RiffWaveConstants.WAVEFORMATTAG_ALAW:
                    if (bitsPerSample == 8)
                        dataFormat = RiffWaveFormat.ALAW;
                    else
                        throw new NotSupportedException("aLaw only supports 8 bits per sample");
                    break;
                case RiffWaveConstants.WAVEFORMATTAG_ULAW:
                    if (bitsPerSample == 8)
                        dataFormat = RiffWaveFormat.ULAW;
                    else
                        throw new NotSupportedException("uLaw only supports 8 bits per sample");
                    break;
                case RiffWaveConstants.WAVEFORMATTAG_MS_ADPCM:
                    dataFormat = RiffWaveFormat.ADPCM_MS;
                    break;
                case RiffWaveConstants.WAVEFORMATTAG_IMA_ADPCM:
                case RiffWaveConstants.WAVEFORMATTAG_UNKNOWN_ADPCM:
                    if (bitsPerSample == 4)
                        dataFormat = RiffWaveFormat.ADPCM_IMA;
                    else
                        throw new NotSupportedException("IMA ADPCM only supports 4 bits per sample");
                    break;
            }

            MultiChannelMapping channelMapping;
            if (numChannels == 1)
            {
                channelMapping = MultiChannelMapping.Monaural;
            }
            else if (numChannels == 2)
            {
                channelMapping = MultiChannelMapping.Stereo_L_R;
            }
            else if (numChannels == 3)
            {
                channelMapping = MultiChannelMapping.Packed_3Ch;
            }
            else if (numChannels == 4)
            {
                channelMapping = MultiChannelMapping.Packed_4Ch;
            }
            else if (numChannels == 5)
            {
                channelMapping = MultiChannelMapping.Packed_5Ch;
            }
            else if (numChannels == 6)
            {
                channelMapping = MultiChannelMapping.Packed_6Ch;
            }
            else if (numChannels == 7)
            {
                channelMapping = MultiChannelMapping.Packed_7Ch;
            }
            else if (numChannels == 8)
            {
                channelMapping = MultiChannelMapping.Packed_8Ch;
            }
            else
            {
                throw new FormatException("Unknown .wav channel layout (channel count " + numChannels + ")");
            }

            if (isWaveFormatExtensible)
            {
                if (numChannels > 1)
                {
                    int channelMask = BinaryHelpers.ByteArrayToInt32LittleEndian(headerBuffer, 20);
                    channelMapping = InterpretChannelMask(channelMapping, numChannels, channelMask);
                }

                // Validate the subformat GUID to ensure the actual data is PCM
                if (RiffWaveConstants.KSDATAFORMAT_SUBTYPE_PCM.AsSpan().SequenceEqual(new Span<byte>(headerBuffer, 24, 16)))
                {
                    if (bitsPerSample == 16)
                        dataFormat = RiffWaveFormat.PCM_S16LE;
                    else if (bitsPerSample == 24)
                        dataFormat = RiffWaveFormat.PCM_S24LE;
                    else if (bitsPerSample == 32)
                        dataFormat = RiffWaveFormat.PCM_S32LE;
                    else
                        throw new NotSupportedException($"Integer PCM does not support {bitsPerSample} bits per sample");
                }
                else if (RiffWaveConstants.KSDATAFORMAT_SUBTYPE_IEEE_FLOAT.AsSpan().SequenceEqual(new Span<byte>(headerBuffer, 24, 16)))
                {
                    if (bitsPerSample == 32)
                        dataFormat = RiffWaveFormat.PCM_F32LE;
                    else
                        throw new NotSupportedException($"IEEE Float PCM does not support {bitsPerSample} bits per sample");
                }
                else if (RiffWaveConstants.KSDATAFORMAT_SUBTYPE_IMA_ADPCM.AsSpan().SequenceEqual(new Span<byte>(headerBuffer, 24, 16)))
                {
                    if (bitsPerSample == 4)
                        dataFormat = RiffWaveFormat.ADPCM_IMA;
                    else
                        throw new NotSupportedException("IMA ADPCM only supports 4 bits per sample");
                }
                else if (RiffWaveConstants.KSDATAFORMAT_SUBTYPE_ALAW.AsSpan().SequenceEqual(new Span<byte>(headerBuffer, 24, 16)))
                {
                    if (bitsPerSample == 8)
                        dataFormat = RiffWaveFormat.ALAW;
                    else
                        throw new NotSupportedException("aLaw only supports 8 bits per sample");
                }
                else if (RiffWaveConstants.KSDATAFORMAT_SUBTYPE_ULAW.AsSpan().SequenceEqual(new Span<byte>(headerBuffer, 24, 16)))
                {
                    if (bitsPerSample == 8)
                        dataFormat = RiffWaveFormat.ULAW;
                    else
                        throw new NotSupportedException("uLaw only supports 8 bits per sample");
                }
                else if (RiffWaveConstants.KSDATAFORMAT_SUBTYPE_MS_ADPCM.AsSpan().SequenceEqual(new Span<byte>(headerBuffer, 24, 16)))
                {
                    throw new InvalidDataException($"MS ADPCM subformat GUID is not supported (got {BinaryHelpers.ToHexString(headerBuffer, 24, 16)})");
                }
                else
                {
                    throw new InvalidDataException($"WAVEFORMATEXTENSIBLE subformat GUID is not known (got {BinaryHelpers.ToHexString(headerBuffer, 24, 16)})");
                }
            }

            if (dataFormat == RiffWaveFormat.Unknown)
            {
                throw new InvalidDataException($"WaveFileSampleSource does not support format:{formatTag} / bps:{bitsPerSample}");
            }

            OutputFormat = new AudioSampleFormat(sampleRate, numChannels, channelMapping);

            // Now initialize our actual decoder
            switch (dataFormat)
            {
                case RiffWaveFormat.PCM_S16LE:
                case RiffWaveFormat.PCM_S24LE:
                case RiffWaveFormat.PCM_S32LE:
                case RiffWaveFormat.PCM_F32LE:
                    _innerDataDecoder = new RawPcmDecoder(_graph, OutputFormat, null, dataFormat);
                    break;
                case RiffWaveFormat.ALAW:
                    _innerDataDecoder = new ALawDecoder(_graph, OutputFormat, null);
                    break;
                case RiffWaveFormat.ULAW:
                    _innerDataDecoder = new ULawDecoder(_graph, OutputFormat, null);
                    break;
                case RiffWaveFormat.ADPCM_IMA:
                    _innerDataDecoder = new AdpcmDecoder(_graph, OutputFormat, null, bytesPerBlock, bitsPerSample);
                    break;
                default:
                    throw new InvalidDataException($"Riff wave decoder does not understand wave format {dataFormat}");
            }

            AudioInitializationResult innerDecoderInitResult = await _innerDataDecoder.Initialize(
                new RiffDataStream(new WeakPointer<RiffDecoder>(_riffDecoder)),
                true,
                cancelToken,
                realTime).ConfigureAwait(false);

            if (innerDecoderInitResult == AudioInitializationResult.Success)
            {
                IsInitialized = true;
            }

            // Update the codec name of this decoder with the actual format parsed from the file
            Codec = $"riff-{_innerDataDecoder.Codec}";
            return innerDecoderInitResult;
        }

        protected override ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            return _innerDataDecoder.ReadAsync(buffer, bufferOffset, numSamplesPerChannel, cancelToken, realTime);
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
                    _innerDataDecoder?.Dispose();
                    _riffDecoder?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        private static MultiChannelMapping InterpretChannelMask(MultiChannelMapping defaultFormat, int numChannels, int channelMask)
        {
            // documentation for channel mask: https://learn.microsoft.com/en-us/windows/win32/api/mmreg/ns-mmreg-waveformatextensible
            if (numChannels == 2 && (channelMask & RiffWaveConstants.SPEAKER_LAYOUT_PACKED) == RiffWaveConstants.SPEAKER_LAYOUT_PACKED)
            {
                // if it's more than 2 channels the default will already by a packed format, this case is
                // just to catch 2-channel audio that we don't want to interpret as stereo for whatever reason
                return MultiChannelMapping.Packed_2Ch;
            }
            else if (numChannels == 4 && (channelMask & RiffWaveConstants.SPEAKER_LAYOUT_QUAD) == RiffWaveConstants.SPEAKER_LAYOUT_QUAD)
            {
                return MultiChannelMapping.Quadraphonic;
            }
            else if (numChannels == 4 && (channelMask & RiffWaveConstants.SPEAKER_LAYOUT_SURROUND) == RiffWaveConstants.SPEAKER_LAYOUT_SURROUND)
            {
                return MultiChannelMapping.Quadraphonic_rear;
            }
            else if (numChannels == 6 && (channelMask & RiffWaveConstants.SPEAKER_LAYOUT_5_1) == RiffWaveConstants.SPEAKER_LAYOUT_5_1)
            {
                return MultiChannelMapping.Surround_5_1ch;
            }
            else if (numChannels == 6 && (channelMask & RiffWaveConstants.SPEAKER_LAYOUT_5_1_SIDE) == RiffWaveConstants.SPEAKER_LAYOUT_5_1_SIDE)
            {
                return MultiChannelMapping.Surround_5_1ch_side;
            }
            else if (numChannels == 8 && (channelMask & RiffWaveConstants.SPEAKER_LAYOUT_7_1) == RiffWaveConstants.SPEAKER_LAYOUT_7_1)
            {
                return MultiChannelMapping.Surround_7_1ch;
            }
            else if (numChannels == 8 && (channelMask & RiffWaveConstants.SPEAKER_LAYOUT_7_1_SIDE) == RiffWaveConstants.SPEAKER_LAYOUT_7_1_SIDE)
            {
                return MultiChannelMapping.Surround_7_1ch_side;
            }

            return defaultFormat;
        }

        /// <summary>
        /// Stream implementation which reads the contents of all concatenated "data" blocks in a RIFF file,
        /// ignoring all other block types
        /// </summary>
        private class RiffDataStream : NonRealTimeStream
        {
            private readonly WeakPointer<RiffDecoder> _decoder;
            private long _bytesRead;
            
            public RiffDataStream(WeakPointer<RiffDecoder> decoder)
            {
                _decoder = decoder.AssertNonNull(nameof(decoder));
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => _bytesRead;
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int bufferOffset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                buffer.AssertNonNull(nameof(buffer));
                bufferOffset.AssertNonNegative(nameof(bufferOffset));
                count.AssertPositive(nameof(count));
                realTime = realTime ?? DefaultRealTimeProvider.Singleton;

                using (PooledBuffer<byte> scratchBuf = BufferPool<byte>.Rent())
                {
                    // First, make sure we are reading from a data block
                    while (!_decoder.Value.EndOfStream && !string.Equals("data", _decoder.Value.CurrentBlockName))
                    {
                        _decoder.Value.ReadFromCurrentBlock(scratchBuf.Buffer, 0, scratchBuf.Length, cancelToken, realTime);
                    }

                    int returnVal = _decoder.Value.ReadFromCurrentBlock(buffer, bufferOffset, count, cancelToken, realTime);
                    if (returnVal > 0)
                    {
                        _bytesRead += returnVal;
                    }

                    return returnVal;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return Read(buffer, offset, count, CancellationToken.None, DefaultRealTimeProvider.Singleton);
            }

            public override async Task<int> ReadAsync(byte[] buffer, int bufferOffset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                buffer.AssertNonNull(nameof(buffer));
                bufferOffset.AssertNonNegative(nameof(bufferOffset));
                count.AssertPositive(nameof(count));
                realTime = realTime ?? DefaultRealTimeProvider.Singleton;

                using (PooledBuffer<byte> scratchBuf = BufferPool<byte>.Rent())
                {
                    // First, make sure we are reading from a data block
                    while (!_decoder.Value.EndOfStream && !string.Equals("data", _decoder.Value.CurrentBlockName))
                    {
                        await _decoder.Value.ReadFromCurrentBlockAsync(scratchBuf.Buffer, 0, scratchBuf.Length, cancelToken, realTime).ConfigureAwait(false);
                    }

                    int returnVal = await _decoder.Value.ReadFromCurrentBlockAsync(buffer, bufferOffset, count, cancelToken, realTime).ConfigureAwait(false);
                    if (returnVal > 0)
                    {
                        _bytesRead += returnVal;
                    }

                    return returnVal;
                }
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override Task WriteAsync(byte[] sourceBuffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
            {
                throw new NotSupportedException();
            }
        }
    }
}
