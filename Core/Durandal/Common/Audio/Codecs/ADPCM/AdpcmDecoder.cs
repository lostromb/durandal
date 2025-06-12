using Durandal.Common.Audio.Components;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.MathExt;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Tasks;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs.ADPCM
{
    public class AdpcmDecoder : AudioDecoder
    {
        private static readonly Regex BITS_PER_SAMPLE_PARSER = new Regex("bits=([0-9]+)");
        private static readonly Regex BLOCKSIZE_PARSER = new Regex("bs=([0-9]+)");

        private readonly int _blockSize;
        private readonly byte[] _encodedBlock;
        private readonly float[] _decodedBlock;
        private int _encodedBlockPtr;
        private int _decodedBlockStartSamplesPerChannel;
        private int _decodedBlockEndSamplesPerChannel;
        private bool _endOfStream;

        public override bool PlaybackFinished => _endOfStream;

        /// <summary>
        /// Creates a new instance of <see cref="AdpcmDecoder"/> 
        /// </summary>
        /// <param name="graph">The audio graph that this component is part of</param>
        /// <param name="outFormat">The output format to decode to</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="blockSize">The ADPCM block size (must be defined by the encoder)</param>
        /// <param name="bitsPerSample">The number of bits per sample used by the encoder (must be 4)</param>
        /// <returns>A newly created <see cref="AdpcmDecoder"/></returns>
        public AdpcmDecoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat outFormat,
            string nodeCustomName,
            int blockSize,
            int bitsPerSample = 4)
            : base(AdpcmCodecFactory.CODEC_NAME, graph, nameof(AdpcmDecoder), nodeCustomName)
        {
            if (bitsPerSample != 4)
            {
                throw new FormatException("ADPCM decoder can only decode 4-bit samples");
            }

            OutputFormat = outFormat.AssertNonNull(nameof(outFormat));
            _blockSize = blockSize.AssertPositive(nameof(blockSize));

            // bytesPerBlock = (((SamplesPerChannel - 1) / 2) + 4) * NumChannels;
            int samplesPerChannelToDecode = (((_blockSize / outFormat.NumChannels) - 4) * 2) + 1;
            _decodedBlock = new float[samplesPerChannelToDecode * outFormat.NumChannels];
            _encodedBlock = new byte[_blockSize];
        }

        public AdpcmDecoder(
            WeakPointer<IAudioGraph> graph,
            string codecParams,
            string nodeCustomName)
            : base(AdpcmCodecFactory.CODEC_NAME, graph, nameof(AdpcmDecoder), nodeCustomName)
        {
            // Parse codec params
            // samplerate=16000 channels=1 layout=1 bits=4 bs=512
            AudioSampleFormat outFormat;
            if (!CommonCodecParamHelper.TryParseCodecParams(codecParams, out outFormat))
            {
                throw new ArgumentException($"Could not parse ADPCM codec params: \"{codecParams}\".");
            }

            int bitsPerSample;
            int blockSize;
            Match m = BITS_PER_SAMPLE_PARSER.Match(codecParams);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out bitsPerSample))
            {
                throw new ArgumentException($"Codec params did not specify \"bits=(bits per sample)\". Params were \"{codecParams}\".");
            }

            m = BLOCKSIZE_PARSER.Match(codecParams);
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out blockSize))
            {
                throw new ArgumentException($"Codec params did not specify \"bs=(blocksize)\". Params were \"{codecParams}\".");
            }

            if (bitsPerSample != 4)
            {
                throw new FormatException("ADPCM decoder can only decode 4-bit samples");
            }

            OutputFormat = outFormat.AssertNonNull(nameof(outFormat));
            _blockSize = blockSize.AssertPositive(nameof(blockSize));
            // bytesPerBlock = (((SamplesPerChannel - 1) / 2) + 4) * NumChannels;
            int samplesPerChannelToDecode = (((_blockSize / outFormat.NumChannels) - 4) * 2) + 1;
            _decodedBlock = new float[samplesPerChannelToDecode * outFormat.NumChannels];
            _encodedBlock = new byte[_blockSize];
        }

        public override async Task<AudioInitializationResult> Initialize(NonRealTimeStream inputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (IsInitialized)
            {
                await DurandalTaskExtensions.NoOpTask;
                return AudioInitializationResult.Already_Initialized;
            }

            InputStream = inputStream.AssertNonNull(nameof(inputStream));
            OwnsStream = ownsStream;
            IsInitialized = true;

            return AudioInitializationResult.Success;
        }

        public override string CodecDescription => "ADPCM (4-bit IMA) codec";

        protected override async ValueTask<int> ReadAsyncInternal(float[] buffer, int bufferOffset, int samplesPerChannelRequestedByCaller, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_endOfStream)
            {
                return -1;
            }

            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (bufferOffset < 0) throw new ArgumentOutOfRangeException(nameof(bufferOffset));
            if (samplesPerChannelRequestedByCaller <= 0) throw new ArgumentOutOfRangeException(nameof(samplesPerChannelRequestedByCaller));
            realTime = realTime ?? DefaultRealTimeProvider.Singleton;

            int samplesPerChannelActuallyRead = 0;
            int attempts = 3;
            while (samplesPerChannelActuallyRead < samplesPerChannelRequestedByCaller &&
                --attempts > 0)
            {
                // Do we have anything to read from our previously decoded block?
                int samplesPerChannelCanReturnFromBlock =
                    FastMath.Min(samplesPerChannelRequestedByCaller - samplesPerChannelActuallyRead,
                    _decodedBlockEndSamplesPerChannel - _decodedBlockStartSamplesPerChannel);
                if (samplesPerChannelCanReturnFromBlock > 0)
                {
                    ArrayExtensions.MemCopy(
                        _decodedBlock,
                        _decodedBlockStartSamplesPerChannel * OutputFormat.NumChannels,
                        buffer,
                        bufferOffset + (samplesPerChannelActuallyRead * OutputFormat.NumChannels),
                        samplesPerChannelCanReturnFromBlock * OutputFormat.NumChannels);
                    _decodedBlockStartSamplesPerChannel += samplesPerChannelCanReturnFromBlock;
                    samplesPerChannelActuallyRead += samplesPerChannelCanReturnFromBlock;

                    // Return early if we can
                    if (samplesPerChannelActuallyRead == samplesPerChannelRequestedByCaller)
                    {
                        break;
                    }
                }

                // If our block is exhausted, try and read a new block
                int bytesActuallyRead = await InputStream.ReadAsync(_encodedBlock, _encodedBlockPtr, _blockSize - _encodedBlockPtr, cancelToken, realTime).ConfigureAwait(false);

                if (bytesActuallyRead == 0)
                {
                    // Reached end of stream. Discard partial block, if any
                    _endOfStream = true;
                    _encodedBlockPtr = 0;
                    return samplesPerChannelActuallyRead == 0 ? -1 : samplesPerChannelActuallyRead;
                }
                else
                {
                    _encodedBlockPtr += bytesActuallyRead;
                }

                // Decode the block if we have one
                if (_encodedBlockPtr == _blockSize)
                {
                    int samplesPerChannelDecoded = Adpcm_xq.adpcm_decode_block(
                        _decodedBlock.AsSpan(),
                        _encodedBlock.AsSpan(0, _blockSize),
                        _blockSize,
                        OutputFormat.NumChannels);

                    _decodedBlockStartSamplesPerChannel = 0;
                    _decodedBlockEndSamplesPerChannel = samplesPerChannelDecoded;
                    _encodedBlockPtr = 0;
                }
            }

            return samplesPerChannelActuallyRead;
        }
    }
}
