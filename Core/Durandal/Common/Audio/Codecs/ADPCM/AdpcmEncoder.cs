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
using Durandal.Common.MathExt;
using Durandal.Common.Collections;
using Durandal.Common.ServiceMgmt;

namespace Durandal.Common.Audio.Codecs.ADPCM
{
    /// <summary>
    /// Encoder which encodes 16-bit samples to 4-bit IMA Adpcm-xq blocks
    /// </summary>
    public class AdpcmEncoder : AudioEncoder
    {
        private readonly int _bytesPerBlock;
        private readonly int _samplesPerChannelPerBlock;
        private readonly float[] _inputBlock;
        private readonly byte[] _outputBlock;
        private readonly int _lookahead;
        private readonly ADPCM.NoiseShaping _noiseShaping;
        private Adpcm_xq.adpcm_context _encoder;
        private int _samplesPerChannelBuffered = 0;

        /// <summary>
        /// Constructs a new <see cref="ALawEncoder"/>.
        /// </summary>
        /// <param name="graph">The audio graph that this component is a part of.</param>
        /// <param name="format">The audio sample format of the input</param>
        /// <param name="nodeCustomName">The friendly name of this node in the audio graph, for debugging and instrumentation (may be null).</param>
        /// <param name="lookahead">The number of samples to lookahead when encoding. 3 is "high". 0 is default.</param>
        /// <param name="noiseShaping">The noise shaping parameter to use when encoding.</param>
        /// <param name="desiredBlockSize">The desired amount of audio to encode in each block. Default 10ms</param>
        /// <returns>A newly constructed <see cref="AdpcmEncoder"/>.</returns>
        public AdpcmEncoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat format,
            string nodeCustomName,
            int lookahead = 0,
            ADPCM.NoiseShaping noiseShaping = ADPCM.NoiseShaping.NOISE_SHAPING_DYNAMIC,
            TimeSpan? desiredBlockSize = null)
            : base(graph, format, nameof(AdpcmEncoder), nodeCustomName)
        {
            if (format.NumChannels > Adpcm_xq.ADPCM_MAX_CHANNELS)
            {
                throw new NotSupportedException($"ADPCM does not support {format.NumChannels}-channel encoding");
            }

            TimeSpan blockSize = desiredBlockSize ?? TimeSpan.FromMilliseconds(10);

            // A block is composed of a header followed by chunks
            // The header is (4 bytes * num channels) and contains 1 sample per channel
            // Each chunk is (4 bytes * num channels) and contains 8 samples per channel
            int desiredSamplesPerChannel = FastMath.Max(32, (int)AudioMath.ConvertTimeSpanToSamplesPerChannel(format.SampleRateHz, blockSize));
            desiredSamplesPerChannel = (desiredSamplesPerChannel - desiredSamplesPerChannel % 8);
            _bytesPerBlock = ((desiredSamplesPerChannel / 2) + 4) * format.NumChannels;
            _samplesPerChannelPerBlock = desiredSamplesPerChannel + 1;
            _inputBlock = new float[_samplesPerChannelPerBlock * format.NumChannels];
            _outputBlock = new byte[_bytesPerBlock];
            _lookahead = lookahead.AssertNonNegative(nameof(lookahead));
            _noiseShaping = noiseShaping;
        }

        public ushort BlockSizeBytes => (ushort)_bytesPerBlock;

        public override string Codec => AdpcmCodecFactory.CODEC_NAME;

        public override string CodecParams
        {
            get
            {
                return string.Format("samplerate={0} channels={1} layout={2} bits=4 bs={3}",
                    InputFormat.SampleRateHz,
                    InputFormat.NumChannels,
                    (int)InputFormat.ChannelMapping,
                    _bytesPerBlock);
            }
        }

        public override Task<AudioInitializationResult> Initialize(NonRealTimeStream outputStream, bool ownsStream, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (IsInitialized)
            {
                return Task.FromResult(AudioInitializationResult.Already_Initialized);
            }

            OutputStream = outputStream.AssertNonNull(nameof(outputStream));
            OwnsStream = ownsStream;
            IsInitialized = true;
            return Task.FromResult(AudioInitializationResult.Success);
        }

        protected override async ValueTask WriteAsyncInternal(float[] buffer, int offset, int count, CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            int samplesPerChannelReadFromCaller = 0;
            while (samplesPerChannelReadFromCaller < count)
            {
                // Try and fill a block
                if (_samplesPerChannelBuffered < _samplesPerChannelPerBlock)
                {
                    int samplesPerChannelCanCopyFromCaller = FastMath.Min(
                        _samplesPerChannelPerBlock - _samplesPerChannelBuffered,
                        count - samplesPerChannelReadFromCaller);
                    ArrayExtensions.MemCopy(
                        buffer,
                        offset + (samplesPerChannelReadFromCaller * InputFormat.NumChannels),
                        _inputBlock,
                        _samplesPerChannelBuffered * InputFormat.NumChannels,
                        samplesPerChannelCanCopyFromCaller * InputFormat.NumChannels);
                    _samplesPerChannelBuffered += samplesPerChannelCanCopyFromCaller;
                    samplesPerChannelReadFromCaller += samplesPerChannelCanCopyFromCaller;
                }

                // Encode a finished block and write
                if (_samplesPerChannelBuffered == _samplesPerChannelPerBlock)
                {
                    await EncodeBlockAndWriteToOutput(cancelToken, realTime).ConfigureAwait(false);
                }
            }
        }

        private async ValueTask EncodeBlockAndWriteToOutput(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            if (_encoder == null)
            {
                // if this is the first block, compute a decaying average (in reverse) so that we can let the
                // encoder know what kind of initial deltas to expect (helps initializing index)
                int[] average_deltas = new int[InputFormat.NumChannels];
                for (int ch = 0; ch < InputFormat.NumChannels; ch++)
                {
                    for (int i = (_samplesPerChannelPerBlock - 1) * InputFormat.NumChannels + ch; i >= InputFormat.NumChannels; i -= InputFormat.NumChannels)
                    {
                        average_deltas[ch] -= average_deltas[ch] >> 3;
                        average_deltas[ch] += (int)(32767.0f * Math.Abs(_inputBlock[i] - _inputBlock[i - InputFormat.NumChannels]));
                    }

                    average_deltas[ch] >>= 3;
                }

                _encoder = new Adpcm_xq.adpcm_context(InputFormat.NumChannels, _lookahead, _noiseShaping, average_deltas.AsSpan());
            }

            int bytesEncoded;
            _encoder.adpcm_encode_block(
                _outputBlock.AsSpan(),
                out bytesEncoded,
                _inputBlock.AsSpan(0, _samplesPerChannelPerBlock * InputFormat.NumChannels),
                _samplesPerChannelBuffered);
#if DEBUG
            if (bytesEncoded != _bytesPerBlock)
            {
                throw new Exception($"Wrong ADPCM output block size, expected {_bytesPerBlock} got {bytesEncoded}");
            }
#endif
            await OutputStream.WriteAsync(_outputBlock, 0, bytesEncoded, cancelToken, realTime).ConfigureAwait(false);
            _samplesPerChannelBuffered = 0;
        }

        protected override async ValueTask FinishInternal(CancellationToken cancelToken, IRealTimeProvider realTime)
        {
            // Finish a partial block if we have one, padding with silence
            // (technically the original library padded with the value of the last sample. whatever)
            if (_samplesPerChannelBuffered > 0)
            {
                ArrayExtensions.WriteZeroes(
                    _inputBlock,
                    _samplesPerChannelBuffered * InputFormat.NumChannels,
                    _inputBlock.Length - (_samplesPerChannelBuffered * InputFormat.NumChannels));
                _samplesPerChannelBuffered = _samplesPerChannelPerBlock;
                await EncodeBlockAndWriteToOutput(cancelToken, realTime).ConfigureAwait(false);
                _samplesPerChannelBuffered = 0;
            }
        }
    }
}
