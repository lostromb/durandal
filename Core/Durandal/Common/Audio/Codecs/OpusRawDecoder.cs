using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs
{
    /// <summary>
    /// Audio codec which decodes a length-delimited stream of Opus packets.
    /// </summary>
    public sealed class OpusRawDecoder : AudioDecoder
    {
        /// <summary>
        /// Max number of bytes in a valid Opus packet
        /// Dictated by Opus' internal length delimiter scheme
        /// </summary>
        private const int MAX_OPUS_PACKET_SIZE = 1275;

        /// <summary>
        /// Max number of samples per channel in a single Opus packet
        /// </summary>
        private const int MAX_OPUS_FRAME_SIZE_SAMPLES_PER_CHANNEL = 5760; // 120ms * 48khz

        /// <summary>
        /// Max number of stereo samples in a single Opus packet
        /// </summary>
        private const int MAX_OPUS_FRAME_SIZE_SAMPLES_IN_STEREO = MAX_OPUS_FRAME_SIZE_SAMPLES_PER_CHANNEL * 2;

        private static readonly Regex SAMPLE_RATE_PARSER = new Regex("samplerate=([0-9]+)");
        private static readonly Regex CHANNEL_COUNT_PARSER = new Regex("channels=([0-9]+)");
        private static readonly Regex CHANNEL_LAYOUT_PARSER = new Regex("layout=([0-9]+)");

        private bool _endOfStream;

        /// <summary>
        /// A fixed-length buffer of decoded audio data.
        /// When a packet is decoded, it writes audio data here. Then we copy that
        /// data to the caller until we have read all of it, after which we try
        /// to decode another packet.
        /// </summary>
        private readonly PooledBuffer<float> _decodedFrameBuffer;
        private int _decodedFrameBufferTotalSamplesPerChannel = 0;
        private int _decodedFrameBufferSamplesPerChannelRead = 0;

        /// <summary>
        /// A fixed-length buffer for storing encoded data
        /// We fill this buffer as much as possible, then read packets from it incrementally
        /// When we start reading a packet that is too close to the end of the buffer,
        /// shift everything to the left and fill it up again
        /// </summary>
        private readonly PooledBuffer<byte> _streamBuffer;
        private int _nextPacketLength = -1; // The reported length of the next incoming packet (decoded from each packet's 4-byte header)
        private int _firstValidByteInStreamBuffer = 0; // The index of the next byte we are going to read from the buffer
        private int _bytesInStreamBuffer = 0; // The total number of valid bytes in the stream buffer

        private readonly string _codecDescription;
        private readonly IOpusDecoder _decoder = null;
        private readonly ILogger _traceLogger;

        // used in real-time scenarios to prevent the codec from trying to decode a huge amount of data in a single read
        private readonly TimeSpan? _realTimeDecodingBudget;

        private int _disposed = 0;

        public override bool PlaybackFinished => _endOfStream && _decodedFrameBufferTotalSamplesPerChannel == 0;

        public override string CodecDescription => _codecDescription;

        /// <summary>
        /// Constructs a new <see cref="OpusRawDecoder"/> 
        /// </summary>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="traceLogger">A logger</param>
        /// <param name="codecParams">Parameters used in encoding, specifying sample rate, channel count, and layout</param>
        /// <param name="maxSupportedOutputFormat">The highest fidelityy audio sample format you want to decode to (must be a format that Opus actually supports).
        /// If null, the decoder will default to the same format that was used by the encoder end.</param>
        /// <param name="realTimeDecodingBudget">An optional limit to the amount of real time a single read loop can take.
        /// Useful to prevent stutter caused by the decoder trying to do too much work at once.</param>
        /// <returns>A newly created <see cref="OpusRawDecoder"/></returns>
        public OpusRawDecoder(
            WeakPointer<IAudioGraph> graph,
            string nodeCustomName,
            ILogger traceLogger,
            string codecParams,
            AudioSampleFormat maxSupportedOutputFormat = null,
            TimeSpan? realTimeDecodingBudget = null)
            : base(OpusRawCodecFactory.CODEC_NAME, graph, nameof(OpusRawDecoder), nodeCustomName)
        {
            OutputFormat = DetermineBestOutputFormat(codecParams, maxSupportedOutputFormat);
            AssertSampleRateIsValidForOpus(OutputFormat.SampleRateHz);
            _codecDescription = "Opus audio codec (via " + OpusCodecFactory.Provider.GetVersionString() + ")";
            if (OutputFormat.NumChannels > 2)
            {
                throw new ArgumentOutOfRangeException("Number of raw Opus channels must be 1 or 2");
            }
            _decoder = OpusCodecFactory.Provider.CreateDecoder(OutputFormat.SampleRateHz, OutputFormat.NumChannels);
            _traceLogger = traceLogger;
            _realTimeDecodingBudget = realTimeDecodingBudget;
            _decodedFrameBuffer = BufferPool<float>.Rent(MAX_OPUS_FRAME_SIZE_SAMPLES_IN_STEREO);
            _streamBuffer = BufferPool<byte>.Rent(FastMath.Max(MAX_OPUS_PACKET_SIZE + 2, BufferPool<byte>.DEFAULT_BUFFER_SIZE));
        }

        private static AudioSampleFormat DetermineBestOutputFormat(string codecParams, AudioSampleFormat maxSupportedFormat)
        {
            maxSupportedFormat = maxSupportedFormat ?? AudioSampleFormat.Stereo(48000);

            if (codecParams == null)
            {
                return maxSupportedFormat;
            }

            int sampleRate;
            int numChannels;
            int channelMappingInt;

            Match m = SAMPLE_RATE_PARSER.Match(codecParams);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out sampleRate))
            {
                throw new ArgumentException("Could not find samplerate parameter in Opus codec params. Will not try to decode stream");
            }

            m = CHANNEL_COUNT_PARSER.Match(codecParams);
            if (m.Success)
            {
                if (!int.TryParse(m.Groups[1].Value, out numChannels))
                {
                    throw new FormatException("Channel count field was not an integer. Codec params are: " + codecParams);
                }
            }
            else
            {
                throw new ArgumentException("Could not find channels parameter in Opus codec params. Will not try to decode stream");
            }

            m = CHANNEL_LAYOUT_PARSER.Match(codecParams);
            if (m.Success)
            {
                if (!int.TryParse(m.Groups[1].Value, out channelMappingInt))
                {
                    throw new FormatException("Channel layout field was not an integer. Codec params are: " + codecParams);
                }
            }
            else
            {
                if (numChannels == 1)
                {
                    channelMappingInt = (int)MultiChannelMapping.Monaural;
                }
                else if (numChannels == 2)
                {
                    channelMappingInt = (int)MultiChannelMapping.Stereo_L_R;
                }
                else
                {
                    throw new ArgumentException("Got raw opus stream containing multichannel audio but no layout specifier. Cannot decode stream.");
                }
            }


            if (sampleRate > maxSupportedFormat.SampleRateHz)
            {
                sampleRate = maxSupportedFormat.SampleRateHz;

                // Round up the requested sample rate to the next highest value supported by Opus
                if (sampleRate > 24000)
                {
                    sampleRate = 48000;
                }
                else if (sampleRate > 16000)
                {
                    sampleRate = 24000;
                }
                else if (sampleRate > 12000)
                {
                    sampleRate = 16000;
                }
                else if (sampleRate > 8000)
                {
                    sampleRate = 12000;
                }
                else
                {
                    sampleRate = 8000;
                }
            }

            if (numChannels > maxSupportedFormat.NumChannels && numChannels <= 2)
            {
                numChannels = maxSupportedFormat.NumChannels;

                if (numChannels == 1)
                {
                    channelMappingInt = (int)MultiChannelMapping.Monaural;
                }
                else if (numChannels == 2)
                {
                    channelMappingInt = (int)MultiChannelMapping.Stereo_L_R;
                }

                // If the input is surround sound for whatever reason, don't try to downscale the number of output channels
                // But sample rate lowering will still apply
            }

            return new AudioSampleFormat(sampleRate, numChannels, (MultiChannelMapping)channelMappingInt);
        }

        public override Task<AudioInitializationResult> Initialize(NonRealTimeStream inputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(OpusRawDecoder));
            }

            if (IsInitialized)
            {
                return Task.FromResult(AudioInitializationResult.Already_Initialized);
            }

            InputStream = inputStream.AssertNonNull(nameof(inputStream));
            OwnsStream = ownsStream;
            IsInitialized = true;
            return Task.FromResult(AudioInitializationResult.Success);
        }

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int numSamplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_disposed != 0)
            {
                throw new ObjectDisposedException(nameof(OpusRawDecoder));
            }

            if (_endOfStream && _decodedFrameBufferSamplesPerChannelRead >= _decodedFrameBufferTotalSamplesPerChannel)
            {
                return -1;
            }

            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (bufferOffset < 0) throw new ArgumentOutOfRangeException(nameof(bufferOffset));
            if (numSamplesPerChannel <= 0) throw new ArgumentOutOfRangeException(nameof(numSamplesPerChannel));
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            int samplesPerChannelReturnedToCaller = 0;
            bool continueDecoding = true;
            long readStartTimeTicks = realTime.TimestampTicks;
            while (continueDecoding && samplesPerChannelReturnedToCaller < numSamplesPerChannel)
            {
                // Is there any data left in the sample buffer? Then use that
                int samplesPerChannelCanReadFromDecodedBuffer = FastMath.Min(
                    _decodedFrameBufferTotalSamplesPerChannel - _decodedFrameBufferSamplesPerChannelRead,
                    numSamplesPerChannel - samplesPerChannelReturnedToCaller);

                if (samplesPerChannelCanReadFromDecodedBuffer > 0)
                {
                    ArrayExtensions.MemCopy(
                        _decodedFrameBuffer.Buffer,
                        _decodedFrameBufferSamplesPerChannelRead * OutputFormat.NumChannels,
                        buffer,
                        (bufferOffset + (samplesPerChannelReturnedToCaller * OutputFormat.NumChannels)),
                        samplesPerChannelCanReadFromDecodedBuffer * OutputFormat.NumChannels);

                    samplesPerChannelReturnedToCaller += samplesPerChannelCanReadFromDecodedBuffer;
                    _decodedFrameBufferSamplesPerChannelRead += samplesPerChannelCanReadFromDecodedBuffer;
                    if (_decodedFrameBufferSamplesPerChannelRead == _decodedFrameBufferTotalSamplesPerChannel)
                    {
                        _decodedFrameBufferSamplesPerChannelRead = 0;
                        _decodedFrameBufferTotalSamplesPerChannel = 0;
                        if (_endOfStream)
                        {
                            //_traceLogger.Log("Opus decoder returned final audio sample", LogLevel.Vrb);
                            return samplesPerChannelReturnedToCaller;
                        }
                    }
                }

                // Now try and decode a packet
                // First read the length prefix if needed
                if (!_endOfStream && _bytesInStreamBuffer < 2)
                {
                    // This read are intended to fill the buffer as much as possible, so we can potentially process a
                    // bunch of frames at once without the overhead of coming back to the stream for tiny <30 byte reads
                    int readResult = await InputStream.ReadAsync(
                        _streamBuffer.Buffer,
                        _firstValidByteInStreamBuffer + _bytesInStreamBuffer,
                        _streamBuffer.Buffer.Length - _bytesInStreamBuffer - _firstValidByteInStreamBuffer,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    if (readResult <= 0)
                    {
                        continueDecoding = false;
                        _endOfStream = true;
                        //_traceLogger.Log("Opus decoder marked end of stream", LogLevel.Vrb);
                    }
                    else
                    {
                        //_traceLogger.Log(BinaryHelpers.ToHexString(_packetBuffer, _bytesInPacketBuffer, readResult));
                        _bytesInStreamBuffer += readResult;
                    }
                }

                if (_bytesInStreamBuffer >= 2)
                {
                    _nextPacketLength = BinaryHelpers.ByteArrayToInt16LittleEndian(_streamBuffer.Buffer, _firstValidByteInStreamBuffer);
                    if (_nextPacketLength <= 0 || _nextPacketLength > MAX_OPUS_PACKET_SIZE)
                    {
                        throw new InvalidDataException("Opus packet is an invalid size: audio stream is corrupted");
                    }
                }

                // Then read the packet body
                if (!_endOfStream &&
                    _bytesInStreamBuffer >= 2 &&
                    _bytesInStreamBuffer < _nextPacketLength + 2)
                {
                    int readResult = await InputStream.ReadAsync(
                        _streamBuffer.Buffer,
                        _firstValidByteInStreamBuffer + _bytesInStreamBuffer,
                        _streamBuffer.Buffer.Length - _bytesInStreamBuffer - _firstValidByteInStreamBuffer,
                        cancelToken,
                        realTime).ConfigureAwait(false);

                    if (readResult <= 0)
                    {
                        continueDecoding = false;
                        _endOfStream = true;
                        //_traceLogger.Log("Opus decoder marked end of stream", LogLevel.Vrb);
                    }
                    else
                    {
                        _bytesInStreamBuffer += readResult;
                    }
                }

                // And decode the packet if we have a complete one
                if (_decodedFrameBufferTotalSamplesPerChannel == 0 &&
                    _bytesInStreamBuffer > 2 &&
                    _bytesInStreamBuffer >= _nextPacketLength + 2)
                {
                    //_traceLogger.Log(BinaryHelpers.ToHexString(_packetBuffer, 2, _nextPacketLength));
                    _decodedFrameBufferTotalSamplesPerChannel = _decoder.Decode(_streamBuffer.Buffer, _firstValidByteInStreamBuffer + 2, _nextPacketLength, _decodedFrameBuffer.Buffer, 0, 5760, false);
                    _decodedFrameBufferSamplesPerChannelRead = 0;

                    // Have we almost read to the end of the current buffer?
                    _bytesInStreamBuffer = _bytesInStreamBuffer - _nextPacketLength - 2;
                    _firstValidByteInStreamBuffer += _nextPacketLength + 2;
                    if (_firstValidByteInStreamBuffer > _streamBuffer.Buffer.Length - MAX_OPUS_PACKET_SIZE - 2)
                    {
                        // Shift the entire thing left
                        if (_bytesInStreamBuffer > 0)
                        {
                            ArrayExtensions.MemMove(_streamBuffer.Buffer, _firstValidByteInStreamBuffer, 0, _bytesInStreamBuffer);
                        }

                        _firstValidByteInStreamBuffer = 0;
                    }
                }

                // If this decoder has a real-time budget, see if we've hit it
                if (samplesPerChannelReturnedToCaller > 0 &&
                    _realTimeDecodingBudget.HasValue &&
                    (_realTimeDecodingBudget.Value == TimeSpan.Zero ||
                    (realTime.TimestampTicks - readStartTimeTicks) >= _realTimeDecodingBudget.Value.Ticks))
                {
                    continueDecoding = false;
                }
            }

            return samplesPerChannelReturnedToCaller;
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
                    _decodedFrameBuffer?.Dispose();
                    _streamBuffer?.Dispose();
                    _decoder?.Dispose();
                }
            }
            catch (Exception e)
            {
                // don't let exceptions in the native layer kill the finalizer thread
                _traceLogger.Log(e);
            }
            finally
            {
                base.Dispose(disposing);
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
