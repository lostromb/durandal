using Durandal.Common.Audio.Codecs.Opus.Common;
using Durandal.Common.Audio.Codecs.Opus.Structs;
using Durandal.Common.Audio;
using Durandal.Common.IO;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Durandal.Common.Utils;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Collections;

namespace Durandal.Common.Audio.Codecs.Opus.Ogg
{
    /// <summary>
    /// A class for writing audio data as an .opus Ogg stream, using an Opus encoder provided in the constructor.
    /// This will handle all of the buffering, packetization and Ogg container work in order to output standard-compliant
    /// .opus files that can be played universally. Note that this makes very basic assumptions about output files:
    /// - Only 1 elementary stream
    /// - Segments may not span pages
    /// </summary>
    public class OpusOggWriteStream : IDisposable
    {
        private const int FRAME_SIZE_MS = 20;

        private static readonly TimeSpan DEFAULT_PRESKIP = TimeSpan.FromMilliseconds(80);

        private readonly IOpusEncoder _encoder;
        private readonly bool _ownsStream;
        private readonly NonRealTimeStream _outputStream;
        private readonly Crc _crc;
        private readonly AudioSampleFormat _inputFormat;

        // Ogg page parameters
        private float[] _opusFrame;
        private int _opusFrameSamples;
        private int _opusFrameIndex;
        private byte[] _currentHeader = new byte[400];
        private byte[] _currentPayload = new byte[65536];
        private int _headerIndex = 0;
        private int _payloadIndex = 0;
        private int _pageCounter = 0;
        private int _logicalStreamId = 0;
        private long _granulePosition = 0;
        private byte _lacingTableCount = 0;
        private byte _maxSegmentsPerPage = 0;
        private const int PAGE_FLAGS_POS = 5;
        private const int GRANULE_COUNT_POS = 6;
        private const int CHECKSUM_HEADER_POS = 22;
        private const int SEGMENT_COUNT_POS = 26;
        private const int MAX_MAX_SEGMENTS_PER_PAGE = 248;
        private bool _finalized = false;
        private int _disposed = 0;

        /// <summary>
        /// Async constructor pattern
        /// </summary>
        /// <param name="encoder">The opus encoder handle</param>
        /// <param name="inputFormat">The format of input audio</param>
        /// <param name="outputStream">The stream to write encoded data to</param>
        /// <param name="ownsStream">Whether this object will take ownership of the output stream and dispose of it</param>
        private OpusOggWriteStream(
            IOpusEncoder encoder,
            AudioSampleFormat inputFormat,
            NonRealTimeStream outputStream,
            bool ownsStream = false)
        {
            _encoder = encoder.AssertNonNull(nameof(encoder));
            _inputFormat = inputFormat.AssertNonNull(nameof(inputFormat));

            if (_encoder.UseDTX)
            {
                throw new ArgumentException("DTX is not supported in Ogg streams");
            }

            _logicalStreamId = new Random().Next();
            _outputStream = outputStream;
            _opusFrameIndex = 0;
            _granulePosition = 0;
            _opusFrameSamples = (int)((long)inputFormat.SampleRateHz * FRAME_SIZE_MS / 1000);
            _opusFrame = new float[_opusFrameSamples * _inputFormat.NumChannels];
            _crc = new Crc();
            _ownsStream = ownsStream;

            DebugMemoryLeakTracer.TraceDisposableItemCreated(this);
        }

#if TRACK_IDISPOSABLE_LEAKS
        ~OpusOggWriteStream()
        {
            Dispose(false);
        }
#endif

        /// <summary>
        /// Constructs a stream that will accept PCM audio input, and automatically encode it to Opus and packetize it using Ogg,
        /// writing the output pages to an underlying stream (usually a file stream).
        /// You are allowed to change the encoding parameters mid-stream using the properties of the OpusEncoder; the only thing you
        /// cannot change is the sample rate and num# of channels.
        /// </summary>
        /// <param name="encoder">An opus encoder to use for output. If channel count > 2, this is a MS encoder.</param>
        /// <param name="inputFormat">The format of input audio going to the encoder</param>
        /// <param name="cancelToken">A cancellation token (can be used to cancel the initialization of the codec)</param>
        /// <param name="realTime">A definition of real time</param>
        /// <param name="outputStream">A base stream to accept the encoded ogg file output</param>
        /// <param name="ownsStream">If true, this object will take responsibility of disposing the output stream.</param>
        /// <param name="fileTags">(optional) A set of tags to include in the encoded file</param>
        public static async Task<OpusOggWriteStream> Create(
            IOpusEncoder encoder,
            AudioSampleFormat inputFormat,
            CancellationToken cancelToken,
            IRealTimeProvider realTime,
            NonRealTimeStream outputStream,
            bool ownsStream = false,
            OpusTags fileTags = null)
        {
            OpusOggWriteStream returnVal = new OpusOggWriteStream(encoder, inputFormat, outputStream, ownsStream);
            await returnVal.Initialize(fileTags, cancelToken, realTime).ConfigureAwait(false);
            return returnVal;
        }

        private async Task Initialize(OpusTags fileTags, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            BeginNewPage();
            await WriteOpusHeadPage(cancelToken, realTime).ConfigureAwait(false);
            await WriteOpusTagsPage(cancelToken, realTime, fileTags).ConfigureAwait(false);

            // Write preskip samples of silence at the start of the file.
            int samplesPerChannelOfPreskip = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(_inputFormat.SampleRateHz, DEFAULT_PRESKIP);
            using (PooledBuffer<float> preskipSilence = BufferPool<float>.Rent(samplesPerChannelOfPreskip * _inputFormat.NumChannels))
            {
                ArrayExtensions.WriteZeroes(preskipSilence.Buffer, 0, preskipSilence.Length);
                await WriteSamplesInternal(preskipSilence.Buffer, 0, samplesPerChannelOfPreskip, cancelToken, realTime).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets or sets the maximum length of encoded audio to write to a single OGG page.
        /// Each page can hold 254 frames, and Opus frames are 20ms, which means that typical
        /// encodings can store about 5 seconds of audio on a single page. However, this translates
        /// directly into streaming delay (since each page must be checksummed before it can begin
        /// transmission) and possible seeking quirks with some players. So this option is provided
        /// to allow more frequent page breaks, which comes at the expense of larger OGG overhead.
        /// </summary>
        public TimeSpan MaxAudioLengthPerPage
        {
            get
            {
                return TimeSpan.FromMilliseconds(FRAME_SIZE_MS * _maxSegmentsPerPage);
            }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxAudioLengthPerPage));
                }

                _maxSegmentsPerPage = (byte)Math.Min(MAX_MAX_SEGMENTS_PER_PAGE, Math.Max(1, (value.TotalMilliseconds / FRAME_SIZE_MS)));
            }
        }

        /// <summary>
        /// Writes a buffer of PCM audio samples to the encoder and packetizer. Runs Opus encoding and potentially outputs one or more pages to the underlying Ogg stream.
        /// You can write any non-zero number of samples that you want here; there are no restrictions on length or packet boundaries
        /// </summary>
        /// <param name="input">The audio samples to write. If stereo, this will be interleaved</param>
        /// <param name="inputOffset">The offset to use when reading data</param>
        /// <param name="samplesPerChannel">The amount of PCM data to write, in samples per channel</param>
        /// <param name="cancelToken">A cancel token</param>
        /// <param name="realTime">Real time definition</param>
        public async Task WriteSamples(float[] input, int inputOffset, int samplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            await WriteSamplesInternal(input, inputOffset, samplesPerChannel, cancelToken, realTime).ConfigureAwait(false);
        }

        private async Task WriteSamplesInternal(float[] input, int inputOffset, int samplesPerChannel, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_finalized)
            {
                throw new InvalidOperationException("Cannot write new samples to Ogg file, the output stream is already closed!");
            }

            if ((input.Length - inputOffset) / _inputFormat.NumChannels < samplesPerChannel)
            {
                // Check that caller isn't lying about its buffer sizes
                throw new ArgumentOutOfRangeException($"The given audio buffer claims to have {samplesPerChannel} frames, but it actually only has {input.Length - inputOffset}");
            }

            int totalInputSamples = samplesPerChannel * _inputFormat.NumChannels;

            // Try and fill the opus frame
            // input cursor in RAW SAMPLES
            int inputCursor = 0;
            // output cursor in RAW SAMPLES
            int totalSamplesToWrite = Math.Min(_opusFrame.Length - _opusFrameIndex, totalInputSamples);

            while (totalSamplesToWrite > 0)
            {
                ArrayExtensions.MemCopy(input, inputOffset + inputCursor, _opusFrame, _opusFrameIndex, totalSamplesToWrite);
                _opusFrameIndex += totalSamplesToWrite;
                inputCursor += totalSamplesToWrite;

                if (_opusFrameIndex == _opusFrame.Length)
                {
                    // Frame is finished. Encode it
                    int packetSize = _encoder.Encode(_opusFrame, 0, _opusFrameSamples, _currentPayload, _payloadIndex, _currentPayload.Length - _payloadIndex);
                    _payloadIndex += packetSize;

                    // Opus granules are measured in 48Khz samples. 
                    // Since our framesize is fixed (20ms) and the sample rate doesn't change, this is basically a constant value
                    _granulePosition += FRAME_SIZE_MS * 48;

                    // And update the lacing values in the header
                    int segmentLength = packetSize;
                    while (segmentLength >= 255)
                    {
                        segmentLength -= 255;
                        _currentHeader[_headerIndex++] = 0xFF;
                        _lacingTableCount++;
                    }

                    _currentHeader[_headerIndex++] = (byte)segmentLength;
                    _lacingTableCount++;

                    // And finalize the page if we need.
                    // By default this just tries to fill the entire page, which could contain
                    // several seconds of audio.
                    // The caller may specify a smaller value which can improve streaming latency and seekability at the expense of page overhead.
                    if (_lacingTableCount > _maxSegmentsPerPage)
                    {
                        await FinalizePage(cancelToken, realTime).ConfigureAwait(false);
                    }

                    _opusFrameIndex = 0;
                }

                totalSamplesToWrite = Math.Min(_opusFrame.Length - _opusFrameIndex, totalInputSamples - inputCursor);
            }
        }

        /// <summary>
        /// Call when you are finished encoding your file.
        /// </summary>
        public async Task Finish(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_opusFrameIndex > 0)
            {
                // Pad the final opus frame with silence and encode as the final packet
                int samplesPerChannelToWrite = (_opusFrame.Length - _opusFrameIndex) / _inputFormat.NumChannels;
                using (PooledBuffer<float> paddingSamples = BufferPool<float>.Rent(samplesPerChannelToWrite * _inputFormat.NumChannels))
                {
                    // Tweak the granule position of the EndOfStream page to reflect the amount of padding we added to the end
                    int granulePositionUpdateFromPartialPacket = (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(
                        48000, AudioMath.ConvertSamplesPerChannelToTimeSpan(_inputFormat.SampleRateHz, _opusFrameIndex / _inputFormat.NumChannels));

                    ArrayExtensions.WriteZeroes(paddingSamples.Buffer, 0, paddingSamples.Length);
                    await WriteSamplesInternal(paddingSamples.Buffer, 0, samplesPerChannelToWrite, cancelToken, realTime).ConfigureAwait(false);

                    // Finalize the page if it was not just finalized right then
                    await FinalizePage(cancelToken, realTime).ConfigureAwait(false);

                    _granulePosition = _granulePosition - (FRAME_SIZE_MS * 48) + granulePositionUpdateFromPartialPacket;
                }
            }
            else
            {
                // Our output buffer is empty. So just finalize the page since there's nothing else to write.
                await FinalizePage(cancelToken, realTime).ConfigureAwait(false);
            }

            // Write a new page that just contains the EndOfStream flag and a potentially trimmed granule position
            await WriteStreamFinishedPage(cancelToken, realTime).ConfigureAwait(false);

            // Now close our output
            await _outputStream.FlushAsync().ConfigureAwait(false);

            _finalized = true;
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
                _encoder?.Dispose();

                if (_ownsStream)
                {
                    _outputStream?.Dispose();
                }
            }
        }

        /// <summary>
        /// Writes an empty page containing only the EndOfStream flag
        /// </summary>
        private Task WriteStreamFinishedPage(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Write one lacing value of 0 length
            _currentHeader[_headerIndex++] = 0x00;
            // Increase the segment count
            _lacingTableCount++;
            // Set page flag to start of logical stream
            _currentHeader[PAGE_FLAGS_POS] = (byte)PageFlags.EndOfStream;
            return FinalizePage(cancelToken, realTime);
        }

        /// <summary>
        /// Writes the Ogg page for OpusHead, containing encoder information
        /// </summary>
        private async Task WriteOpusHeadPage(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_payloadIndex != 0)
            {
                throw new InvalidOperationException("Must begin writing OpusHead on a new page!");
            }

            _payloadIndex += WriteValueToByteBuffer("OpusHead", _currentPayload, _payloadIndex);
            _currentPayload[_payloadIndex++] = 0x01; // Version number
            _currentPayload[_payloadIndex++] = (byte)_inputFormat.NumChannels; // Channel count
            short preskip = (short)AudioMath.ConvertTimeSpanToSamplesPerChannel(48000, DEFAULT_PRESKIP); // 80ms recommended value to use by opus devs
            _payloadIndex += WriteValueToByteBuffer(preskip, _currentPayload, _payloadIndex); // Pre-skip (number of samples).
            _payloadIndex += WriteValueToByteBuffer(_inputFormat.SampleRateHz, _currentPayload, _payloadIndex); // Input sample rate
            short outputGainQ8 = 0;
            _payloadIndex += WriteValueToByteBuffer(outputGainQ8, _currentPayload, _payloadIndex); // Output gain in Q8

            if (_inputFormat.NumChannels <= 2)
            {
                _currentPayload[_payloadIndex++] = 0x00; // Channel map (0 indicates mono/stereo config)
            }
            else
            {
                // Write multichannel opushead - this gets complicated...
                VorbisLayout layout;
                if (_inputFormat.ChannelMapping == MultiChannelMapping.Quadraphonic)
                {
                    layout = VorbisLayout.vorbis_mappings[3];
                }
                else if (_inputFormat.ChannelMapping == MultiChannelMapping.Surround_5ch_Vorbis_Layout)
                {
                    layout = VorbisLayout.vorbis_mappings[4];
                }
                else if (_inputFormat.ChannelMapping == MultiChannelMapping.Surround_5_1ch_Vorbis_Layout)
                {
                    layout = VorbisLayout.vorbis_mappings[5];
                }
                else if (_inputFormat.ChannelMapping == MultiChannelMapping.Surround_7_1ch_Vorbis_Layout)
                {
                    layout = VorbisLayout.vorbis_mappings[7];
                }
                else
                {
                    throw new NotImplementedException($"Writing multichannel OggOpus audio is not supported for channel layout {_inputFormat.ChannelMapping} (and those which do must use Vorbis layouts)");
                }

                _currentPayload[_payloadIndex++] = 0x01; // Mapping family 1
                _currentPayload[_payloadIndex++] = (byte)layout.nb_streams; // Stream count
                _currentPayload[_payloadIndex++] = (byte)layout.nb_coupled_streams; // Coupled stream count
                layout.mapping.AsSpan().CopyTo(_currentPayload.AsSpan(_payloadIndex)); // Channel mapping
                _payloadIndex += layout.mapping.Length;
            }

            // Write the payload as segment data
            _currentHeader[_headerIndex++] = (byte)_payloadIndex; // implicit assumption that this value will always be less than 255
            _lacingTableCount++;

            // Set page flag to start of logical stream
            _currentHeader[PAGE_FLAGS_POS] = (byte)PageFlags.BeginningOfStream;
            await FinalizePage(cancelToken, realTime).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes an Ogg page for the OpusTags, given an input tag set
        /// </summary>
        /// <param name="cancelToken">cancel token</param>
        /// <param name="realTime">Real time definition</param>
        /// <param name="tags"></param>
        private async Task WriteOpusTagsPage(CancellationToken cancelToken, IRealTimeProvider realTime, OpusTags tags = null)
        {
            if (tags == null)
            {
                tags = new OpusTags();
            }

            if (string.IsNullOrEmpty(tags.Comment))
            {
                tags.Comment = CodecHelpers.GetVersionString();
            }

            if (_payloadIndex != 0)
            {
                throw new InvalidOperationException("Must begin writing OpusTags on a new page!");
            }

            // BUGBUG: Very long tags can overflow the page and corrupt the stream
            _payloadIndex += WriteValueToByteBuffer("OpusTags", _currentPayload, _payloadIndex);
            
            // write comment
            int stringLength = WriteValueToByteBuffer(tags.Comment, _currentPayload, _payloadIndex + 4);
            _payloadIndex += WriteValueToByteBuffer(stringLength, _currentPayload, _payloadIndex);
            _payloadIndex += stringLength;

            // capture the location of the tag count field to fill in later
            int numTagsIndex = _payloadIndex;
            _payloadIndex += 4;

            // write each tag. skipping empty or invalid ones
            int tagsWritten = 0;
            foreach (var kvp in tags.Fields)
            {
                if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value))
                    continue;

                string tag = kvp.Key + "=" + kvp.Value;
                stringLength = WriteValueToByteBuffer(tag, _currentPayload, _payloadIndex + 4);
                _payloadIndex += WriteValueToByteBuffer(stringLength, _currentPayload, _payloadIndex);
                _payloadIndex += stringLength;
                tagsWritten++;
            }

            // Write actual tag count
            WriteValueToByteBuffer(tagsWritten, _currentPayload, numTagsIndex);

            // Write segment data, ensuring we can handle tags longer than 255 bytes
            int tagsSegmentSize = _payloadIndex;
            while (tagsSegmentSize >= 255)
            {
                _currentHeader[_headerIndex++] = 255;
                _lacingTableCount++;
                tagsSegmentSize -= 255;
            }
            _currentHeader[_headerIndex++] = (byte)tagsSegmentSize;
            _lacingTableCount++;

            await FinalizePage(cancelToken, realTime).ConfigureAwait(false);
        }

        /// <summary>
        /// Clears all buffers and prepares a new page with an empty header
        /// </summary>
        private void BeginNewPage()
        {
            _headerIndex = 0;
            _payloadIndex = 0;
            _lacingTableCount = 0;

            // Page begin keyword
            _headerIndex += WriteValueToByteBuffer("OggS", _currentHeader, _headerIndex);
            // Stream version 0
            _currentHeader[_headerIndex++] = 0x0;
            // Header flags
            _currentHeader[_headerIndex++] = (byte)PageFlags.None;
            // Granule position (for opus, it is the number of 48Khz pcm samples encoded)
            _headerIndex += WriteValueToByteBuffer(_granulePosition, _currentHeader, _headerIndex);
            // Logical stream serial number
            _headerIndex += WriteValueToByteBuffer(_logicalStreamId, _currentHeader, _headerIndex);
            // Page sequence number
            _headerIndex += WriteValueToByteBuffer(_pageCounter, _currentHeader, _headerIndex);
            // Checksum is initially zero
            _currentHeader[_headerIndex++] = 0x0;
            _currentHeader[_headerIndex++] = 0x0;
            _currentHeader[_headerIndex++] = 0x0;
            _currentHeader[_headerIndex++] = 0x0;
            // Number of segments, initially zero
            _currentHeader[_headerIndex++] = _lacingTableCount;
            // Segment table goes after this point, once we have packets in this page

            _pageCounter++;
        }

        /// <summary>
        /// If the number of segments is nonzero, finalizes the page into a contiguous buffer, calculates CRC, and writes the page to the output stream
        /// </summary>
        private async Task FinalizePage(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_finalized)
            {
                throw new InvalidOperationException("Cannot finalize page, the output stream is already closed!");
            }

            if (_lacingTableCount != 0)
            {
                // Write the final segment count to the header
                _currentHeader[SEGMENT_COUNT_POS] = _lacingTableCount;
                // And the granule count for frames that finished on this page
                WriteValueToByteBuffer(_granulePosition, _currentHeader, GRANULE_COUNT_POS);
                // Calculate CRC and update the header
                _crc.Reset();
                for (int c = 0; c < _headerIndex; c++)
                {
                    _crc.Update(_currentHeader[c]);
                }
                for (int c = 0; c < _payloadIndex; c++)
                {
                    _crc.Update(_currentPayload[c]);
                }
                //Debug.WriteLine("Writing CRC " + _crc.Value);
                WriteValueToByteBuffer(_crc.Value, _currentHeader, CHECKSUM_HEADER_POS);
                // Write the page to the stream (TODO: Make sure this operation does not overflow any target stream buffers?)
                await _outputStream.WriteAsync(_currentHeader, 0, _headerIndex, cancelToken, realTime).ConfigureAwait(false);
                await _outputStream.WriteAsync(_currentPayload, 0, _payloadIndex, cancelToken, realTime).ConfigureAwait(false);
                // And reset the page
                BeginNewPage();
            }
        }

        private static int WriteValueToByteBuffer(int val, byte[] target, int targetOffset)
        {
            BinaryHelpers.Int32ToByteArrayLittleEndian(val, target, targetOffset);
            return 4;
        }

        private static int WriteValueToByteBuffer(long val, byte[] target, int targetOffset)
        {
            BinaryHelpers.Int64ToByteArrayLittleEndian(val, target, targetOffset);
            return 8;
        }

        private static int WriteValueToByteBuffer(uint val, byte[] target, int targetOffset)
        {
            BinaryHelpers.UInt32ToByteArrayLittleEndian(val, target, targetOffset);
            return 4;
        }

        private static int WriteValueToByteBuffer(short val, byte[] target, int targetOffset)
        {
            BinaryHelpers.Int16ToByteArrayLittleEndian(val, target, targetOffset);
            return 2;
        }

        private static int WriteValueToByteBuffer(string val, byte[] target, int targetOffset)
        {
            if (string.IsNullOrEmpty(val))
                return 0;

            // using a scratch pooled buffer here is inevitable as we can't write the string directly to the stream,
            // and we don't know its exact size before encoding.
            using (PooledBuffer<byte> stringScratch = BufferPool<byte>.Rent(Encoding.UTF8.GetMaxByteCount(val.Length)))
            {
                int encodedStringSize = Encoding.UTF8.GetBytes(val, 0, val.Length, stringScratch.Buffer, 0);
                ArrayExtensions.MemCopy(stringScratch.Buffer, 0, target, targetOffset, encodedStringSize);
                return encodedStringSize;
            }
        }
    }
}
