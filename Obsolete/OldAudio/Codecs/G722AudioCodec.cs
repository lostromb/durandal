
namespace Durandal.Extensions.NAudio.Codecs
{
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.AudioV2;
    using Durandal.Common.Utils;
    using global::NAudio.Codecs;
    using System;

    public class G722AudioCodec : IAudioCodec
    {
        private G722Codec _codec;
        
        public string GetFormatCode()
        {
            return "g722";
        }

        public string GetDescription()
        {
            return "G.722 (48kbit) codec";
        }

        public bool Initialize()
        {
            _codec = new G722Codec();
            return true;
        }

        public static string CreateCodecParams(int sampleRate)
        {
            return PCMCodec.CreateCodecParams(sampleRate);
        }

        public static int? GetSampleRateFromCodecParams(string codecParams)
        {
            return PCMCodec.GetSampleRateFromCodecParams(codecParams);
        }

        public IAudioCompressionStream CreateCompressionStream(int inputSampleRate, Guid? traceId = null)
        {
            return new Compressor(_codec, inputSampleRate);
        }

        public IAudioDecompressionStream CreateDecompressionStream(string encodeParams, Guid? traceId = null)
        {
            return new Decompressor(_codec, encodeParams);
        }

        internal class Compressor : IAudioCompressionStream
        {
            private int _sampleRate;
            private G722Codec _codec;
            private G722CodecState _state = new G722CodecState(48000, G722Flags.Packed);

            internal Compressor(G722Codec codec, int sampleRate)
            {
                _codec = codec;
                _sampleRate = sampleRate;
            }

            public byte[] Compress(AudioChunk audio)
            {
                short[] inputData = audio.Data;
                byte[] outBuf = new byte[inputData.Length];
                // TODO: Why length - 4???
                int compressedSize = _codec.Encode(_state, outBuf, inputData, inputData.Length - 4);
                byte[] returnVal = new byte[compressedSize];
                Array.Copy(outBuf, 0, returnVal, 0, compressedSize);

                return returnVal;
            }

            public byte[] Close()
            {
                return BinaryHelpers.EMPTY_BYTE_ARRAY;
            }

            public string GetEncodeParams()
            {
                return CreateCodecParams(_sampleRate);
            }

            public void Dispose() { }
        }

        internal class Decompressor : IAudioDecompressionStream
        {
            private int _sampleRate = -1;
            private G722Codec _codec;
            private G722CodecState _state = new G722CodecState(48000, G722Flags.Packed);

            internal Decompressor(G722Codec codec, string encodeParams)
            {
                _codec = codec;
                _sampleRate = GetSampleRateFromCodecParams(encodeParams).GetValueOrDefault(AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE);
            }

            public AudioChunk Decompress(ArraySegment<byte> input)
            {
                byte[] actualData = input.Array;
                if (input.Offset != 0)
                {
                    // OPT: slow copy because the g722 codec doesn't allow input offsets
                    actualData = new byte[input.Count];
                    Array.Copy(input.Array, input.Offset, actualData, 0, input.Count);
                }

                short[] outBuf = new short[input.Count * 4];
                int decompressedSize = _codec.Decode(_state, outBuf, actualData, input.Count);
                short[] returnVal = new short[decompressedSize];
                Array.Copy(outBuf, 0, returnVal, 0, decompressedSize);
                return new AudioChunk(returnVal, _sampleRate);
            }

            public AudioChunk Close()
            {
                return new AudioChunk(new short[0], _sampleRate);
            }

            public void Dispose() { }
        }
    }
}
