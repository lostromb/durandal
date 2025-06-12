using Durandal.Common.Audio.Codecs.Opus.Structs;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs.Opus.Ogg
{
    /// <summary>
    /// Provides functionality to decode a basic .opus Ogg file, decoding the audio packets individually and returning them. Tags are also parsed if present.
    /// Note that this currently assumes the input file only has 1 elementary stream; anything more advanced than that will probably not work.
    /// </summary>
    public class OpusOggReadStream : IDisposable
    {
        private static readonly byte[] OPUS_HEAD = new byte[] { (byte)'O', (byte)'p', (byte)'u', (byte)'s', (byte)'H', (byte)'e', (byte)'a', (byte)'d'};
        private static readonly byte[] OPUS_TAGS = new byte[] { (byte)'O', (byte)'p', (byte)'u', (byte)'s', (byte)'T', (byte)'a', (byte)'g', (byte)'s' };

        private readonly Stream _inputStream;
        private readonly bool _ownsStream;
        private IOpusDecoder _decoder;
        private AudioSampleFormat _decodedFormat;
        private OggContainerReader _oggReader;
        private OpusTags _tags;
        private WeakPointer<IPacketProvider> _packetProvider;
        private byte[] _nextDataPacket;
        private bool _endOfStream;
        private string _lastError;
        private int _preskipSamplesPerChannelRemaining = 0;
        private float _outputGainLinear = 1.0f;
        private int _disposed = 0;

        private OpusOggReadStream(Stream oggFileInput, bool ownsStream)
        {
            _ownsStream = ownsStream;
            _inputStream = oggFileInput.AssertNonNull(nameof(oggFileInput));
            _endOfStream = false;
            _oggReader = null;
            
            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

        /// <summary>
        /// Builds an Ogg file reader that decodes Opus packets from the given input stream, using a 
        /// specified output sample rate and channel count. The given decoder will be used as-is
        /// and return the decoded PCM buffers directly.
        /// </summary>
        /// <param name="oggFileInput">The input stream for an Ogg formatted .opus file. The stream will be read from immediately</param>
        /// <param name="ownsStream">If true, dispose of the inner stream when this read stream gets disposed.</param>
        public static async Task<OpusOggReadStream> Create(Stream oggFileInput, bool ownsStream = false)
        {
            OpusOggReadStream returnVal = new OpusOggReadStream(oggFileInput, ownsStream);
            if (!(await returnVal.Initialize().ConfigureAwait(false)))
            {
                returnVal._endOfStream = true;
            }

            return returnVal;
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~OpusOggReadStream()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Gets the tags that were parsed from the OpusTags Ogg packet, or NULL if no such packet was found.
        /// </summary>
        public OpusTags Tags
        {
            get
            {
                return _tags;
            }
        }

        /// <summary>
        /// Returns true if there is still another data packet to be decoded from the current Ogg stream.
        /// Note that this decoder currently only assumes that the input has 1 elementary stream with no splices
        /// or other fancy things.
        /// </summary>
        public bool HasNextPacket
        {
            get
            {
                return !_endOfStream;
            }
        }

        /// <summary>
        /// If an error happened either in stream initialization, reading, or decoding, the message will appear here.
        /// </summary>
        public string LastError
        {
            get
            {
                return _lastError;
            }
        }

        public AudioSampleFormat DecodedFormat
        {
            get
            {
                return _decodedFormat;
            }
        }

        /// <summary>
        /// Reads the next packet from the Ogg stream and decodes it, returning the decoded PCM buffer.
        /// If there are no more packets to decode, this returns NULL. If an error occurs, this also returns
        /// NULL and puts the error message into the LastError field
        /// </summary>
        /// <returns>The decoded audio for the next packet in the stream, or NULL</returns>
        public async Task<PooledBuffer<float>> DecodeNextPacket()
        {
            if (_decoder == null)
            {
                throw new InvalidOperationException("Cannot decode opus packets as a decoder was never provided");
            }

            if (_nextDataPacket == null || _nextDataPacket.Length == 0)
            {
                _endOfStream = true;
            }

            if (_endOfStream)
            {
                return null;
            }

            try
            {
                PooledBuffer<float> output = null;
                do
                {
                    int numSamples = OpusPacketInfo.GetNumSamples(_nextDataPacket, 0, _nextDataPacket.Length, _decodedFormat.SampleRateHz);
                    output?.Dispose(); // could be a discarded preskip packet from a previous loop, make sure we dispose
                    output = BufferPool<float>.Rent(numSamples * _decodedFormat.NumChannels);

                    _decoder.Decode(_nextDataPacket, 0, _nextDataPacket.Length, output.Buffer, 0, numSamples, false);

                    await QueueNextPacket().ConfigureAwait(false);

                    // Handle preskip by skipping decoded samples at the start of the file
                    if (_preskipSamplesPerChannelRemaining > 0)
                    {
                        int preskipSamplesPerChannelToTrimFromThisPacket = Math.Min(numSamples, _preskipSamplesPerChannelRemaining);
                        int newBufferLengthSamplesPerChannel = numSamples - preskipSamplesPerChannelToTrimFromThisPacket;
                        if (newBufferLengthSamplesPerChannel > 0)
                        {
                            // We preskipped a partial frame. Shift left and trim the buffer
                            ArrayExtensions.MemMove(output.Buffer, preskipSamplesPerChannelToTrimFromThisPacket * _decodedFormat.NumChannels, 0, newBufferLengthSamplesPerChannel * _decodedFormat.NumChannels);
                        }

                        // new buffer size could be 0, in which case we'll iterate
                        output.Shrink(newBufferLengthSamplesPerChannel);
                        _preskipSamplesPerChannelRemaining -= preskipSamplesPerChannelToTrimFromThisPacket;
                    }
                } while (output.Length == 0 && !_endOfStream);

                if (output.Length == 0)
                {
                    output?.Dispose();
                    return null;
                }

                if (_endOfStream && output != null && output.Length > 0)
                {
                    // TODO: if this is the final packet in the stream, see if we need to trim its length based on the end of stream page granule position
                    // (that is, the granule position of the _next_ page, after the one we just decoded
                    // Unimplemented for now because I can't get it right with how IPacketProvider works
                    //long granuleDiff = _prevDataPacketGranulePos - _nextDataPacketGranulePos;
                    //TimeSpan validAudioInThisPacket = AudioMath.ConvertSamplesPerChannelToTimeSpan(48000, granuleDiff);
                    //int validSamplesPerChannel = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(_decodedFormat.SampleRateHz, validAudioInThisPacket);
                    //output.Shrink(validSamplesPerChannel * _decodedFormat.NumChannels);
                }

                // Apply output gain if applicable
                if (output != null && output.Length > 0 && _outputGainLinear != 1.0f)
                {
                    AudioMath.ScaleSamples(output.Buffer, 0, output.Length, _outputGainLinear);
                }

                return output;
            }
            catch (OpusException e)
            {
                _lastError = $"Opus decoder threw exception: {e.Message}";
                return null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicOperations.ExecuteOnce(ref _disposed))
            {
                return;
            }

            DebugMemoryLeakTracer.TraceDisposableItemDisposed(this, disposing);

            if (disposing)
            {
                _decoder?.Dispose();
                _oggReader?.Dispose();
                if (_ownsStream)
                {
                    _inputStream?.Dispose();
                }
            }
        }

        /// <summary>
        /// Creates an opus decoder and reads from the ogg stream until a data packet is encountered,
        /// queuing it up for future decoding. Tags are also parsed if they are encountered.
        /// </summary>
        /// <returns>True if the stream is valid and ready to be decoded</returns>
        private async Task<bool> Initialize()
        {
            try
            {
                _oggReader = new OggContainerReader(_inputStream, false);
                if (!(await _oggReader.Init().ConfigureAwait(false)))
                {
                    _lastError = "Could not initialize stream";
                    return false;
                }

                //if (!reader.FindNextStream())
                //{
                //    _lastError = "Could not find elementary stream";
                //    return false;
                //}
                if (_oggReader.StreamSerials.Length == 0)
                {
                    _lastError = "Initialization failed: No elementary streams found in input file";
                    return false;
                }

                int streamSerial = _oggReader.StreamSerials[0];
                _packetProvider = _oggReader.GetStream(streamSerial);
                await QueueNextPacket().ConfigureAwait(false);

                return true;
            }
            catch (Exception e)
            {
                _lastError = "Unknown initialization error: " + e.Message;
                return false;
            }
        }

        /// <summary>
        /// Looks for the next opus data packet in the Ogg stream and queues it up.
        /// If the end of stream has been reached, this does nothing.
        /// </summary>
        private async Task QueueNextPacket()
        {
            if (_endOfStream)
            {
                return;
            }

            DataPacket packet = await _packetProvider.Value.GetNextPacket().ConfigureAwait(false);
            if (packet == null || packet.IsEndOfStream || packet.Length == 0)
            {
                _endOfStream = true;
                _nextDataPacket = null;
                return;
            }

            byte[] buf = new byte[packet.Length];
            packet.Read(buf, 0, packet.Length);
            packet.Done();

            if (buf.Length >= 19 && OPUS_HEAD.AsSpan().SequenceEqual(buf.AsSpan(0, 8)))
            {
                ParseOpusHeadAndCreateDecoder(buf, buf.Length);
                 await QueueNextPacket().ConfigureAwait(false);
            }
            else if (buf.Length > 8 && OPUS_TAGS.AsSpan().SequenceEqual(buf.AsSpan(0, 8)))
            {
                _tags = OpusTags.ParsePacket(buf, buf.Length);
                await QueueNextPacket().ConfigureAwait(false);
            }
            else
            {
                _nextDataPacket = buf;
            }
        }

        private void ParseOpusHeadAndCreateDecoder(byte[] buf, int bufLength)
        {
            // Read OpusHead and create decoder
            // https://wiki.xiph.org/OggOpus
            byte version = buf[8];
            byte channelCount = buf[9];
            ushort preskip = BinaryHelpers.ByteArrayToUInt16LittleEndian(buf, 10);
            uint sampleRate = BinaryHelpers.ByteArrayToUInt32LittleEndian(buf, 12);
            short rawOutputGain = BinaryHelpers.ByteArrayToInt16LittleEndian(buf, 16);
            byte channelMappingFamily = buf[18];

            if (version != 1)
            {
                throw new FormatException($"Invalid opus stream: version number must be 1 (got {version})");
            }

            AssertSampleRateIsValidForOpus((int)sampleRate);

            if (rawOutputGain == 0)
            {
                _outputGainLinear = 1.0f;
            }
            else
            {
                _outputGainLinear = (float)Math.Pow(10, (double)rawOutputGain / (20.0 * 256));
            }

            // https://www.iana.org/assignments/opus-channel-mapping-families/opus-channel-mapping-families.xhtml
            if (channelMappingFamily == 0)
            {
                if (channelCount > 2)
                {
                    throw new FormatException($"Invalid opus stream: wrong channel count {channelCount} for channel mapping family {channelMappingFamily}");
                }

                _decoder = OpusCodecFactory.Provider.CreateDecoder((int)sampleRate, (int)channelCount);
                _decodedFormat = new AudioSampleFormat((int)sampleRate, channelCount == 1 ? MultiChannelMapping.Monaural : MultiChannelMapping.Stereo_L_R);
            }
            else if (channelMappingFamily == 1)
            {
                byte streamCount = buf[19];
                byte coupledStreamCount = buf[20];
                byte[] channelMappingTable = new byte[bufLength - 21];
                buf.AsSpan(21, bufLength - 21).CopyTo(channelMappingTable.AsSpan());

                _decoder = OpusCodecFactory.Provider.CreateMultistreamDecoder((int)sampleRate, (int)channelCount, streamCount, coupledStreamCount, channelMappingTable);
                MultiChannelMapping outputChannelMapping;
                if (channelCount == 4)
                {
                    outputChannelMapping = MultiChannelMapping.Quadraphonic;
                }
                else if (channelCount == 5)
                {
                    outputChannelMapping = MultiChannelMapping.Surround_5ch_Vorbis_Layout;
                }
                else if (channelCount == 6)
                {
                    outputChannelMapping = MultiChannelMapping.Surround_5_1ch_Vorbis_Layout;
                }
                //else if (channelCount == 7)
                //{
                //    outputChannelMapping = MultiChannelMapping.Surround_6_1ch_Vorbis_Layout;
                //}
                else if (channelCount == 8)
                {
                    outputChannelMapping = MultiChannelMapping.Surround_7_1ch_Vorbis_Layout;
                }
                else
                {
                    throw new NotImplementedException($"Cannot decode multistream Opus audio with channel count {channelCount}: No layout is defined.");
                }

                _decodedFormat = new AudioSampleFormat((int)sampleRate, outputChannelMapping);
            }
            else if (channelMappingFamily == 255)
            {
                throw new NotImplementedException($"Opus stream uses discrete channel mapping which is not yet supported");
            }
            else if (channelMappingFamily == 2 || channelMappingFamily == 3)
            {
                throw new NotImplementedException($"Opus stream uses Ambisonics channel mapping {channelMappingFamily} which is not yet supported");
            }
            else
            {
                throw new FormatException($"Invalid opus stream: channel mapping family {channelMappingFamily} is not defined");
            }

            _preskipSamplesPerChannelRemaining = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel((int)sampleRate, AudioMath.ConvertSamplesPerChannelToTimeSpan(48000, preskip));
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
