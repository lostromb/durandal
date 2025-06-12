
namespace Durandal.Extensions.NAudio.Codecs
{
    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Codecs;
    using Durandal.Common.AudioV2;
    using Durandal.Common.Utils;
    using global::NAudio.Codecs;
    using System;

    public class uLawAudioCodec : IAudioCodec
    {
        public string GetFormatCode()
        {
            return "ulaw";
        }

        public string GetDescription()
        {
            return "μ-law codec";
        }

        public bool Initialize()
        {
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
            return new Compressor(inputSampleRate);
        }

        public IAudioDecompressionStream CreateDecompressionStream(string encodeParams, Guid? traceId = null)
        {
            return new Decompressor(encodeParams);
        }

        internal class Compressor : IAudioCompressionStream
        {
            private int _sampleRate;

            internal Compressor(int sampleRate)
            {
                _sampleRate = sampleRate;
            }

            public byte[] Compress(AudioChunk audio)
            {
                short[] inputData = audio.Data;
                byte[] returnVal = new byte[inputData.Length];
                for (int c = 0; c < inputData.Length; c++)
                {
                    int next = inputData[c];
                    returnVal[c] = MuLawEncoder.LinearToMuLawSample(inputData[c]);
                }

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

            internal Decompressor(string encodeParams)
            {
                _sampleRate = GetSampleRateFromCodecParams(encodeParams).GetValueOrDefault(AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE);
            }

            public AudioChunk Decompress(ArraySegment<byte> input)
            {
                short[] audio = new short[input.Count];
                for (int cursor = 0; cursor < input.Count; cursor++)
                {
                    audio[cursor] = MuLawDecoder.MuLawToLinearSample(input.Array[input.Offset + cursor]);
                }

                return new AudioChunk(audio, _sampleRate);
            }

            public AudioChunk Close()
            {
                return new AudioChunk(new short[0], _sampleRate);
            }

            public void Dispose() { }
        }
    }
}
