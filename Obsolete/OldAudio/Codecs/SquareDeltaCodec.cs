using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.Audio.Interfaces;
using Durandal.Common.MathExt;

namespace Durandal.Common.Audio.Codecs
{
    using System.IO;
    using Durandal.Common.Utils;

    /// <summary>
    /// Uses Square-Root-Delta encoding to compress a 16-bit audio signal at a 2:1 ratio.
    /// The algorithm is just a slight variant on first-order linear prediction.
    /// This is considered a fallback codec if, for example, you are running on an embedded system and
    /// don't have access to more advanced Speex or Opus libraries, or you need to encode extremely quickly
    /// </summary>
    public class SquareDeltaCodec : IAudioCodec
    {
        public string GetFormatCode()
        {
            return "sqrt";
        }

        public string GetDescription()
        {
            return "Arithmetic SRD codec";
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

        // Determines the maximum delta range to use based on sample rate.
        // Lower numbers mean greater precision but worse handling of large jumps
        private static int GetCeiling(int sampleRate)
        {
            if (sampleRate > 44100)
            {
                return 5000;
            }
            else if (sampleRate > 16000)
            {
                return 8000;
            }
            else
            {
                return 16000;
            }
        }

        /// <summary>
        /// Maps the byte code points along a curve that ranges from 0 to ceil
        /// </summary>
        /// <param name="difference"></param>
        /// <param name="ceil"></param>
        /// <param name="actualDelta"></param>
        /// <returns></returns>
        private static byte EncodeLargeJump(short difference, int ceil, out int actualDelta)
        {
            byte code = (byte)Math.Min(127, Math.Sqrt(Math.Abs((double)difference) * (127 * 127) / (double)ceil));
            actualDelta = (int)Math.Round(code * code * (double)ceil / (127 * 127));
            if (actualDelta > short.MaxValue)
            {
                actualDelta = short.MaxValue;
            }

            if (difference < 0)
            {
                code |= 0x80;
                actualDelta = 0 - actualDelta;
            }

            return code;
        }

        /// <summary>
        /// Inverts the transformation performed by EncodeLargeJump. Converts the byte code into
        /// the actual magnitude of audio difference
        /// </summary>
        /// <param name="code"></param>
        /// <param name="ceil"></param>
        /// <returns></returns>
        private static short DecodeLargeJump(byte code, int ceil)
        {
            double c = (double)(code & 0x7F);
            int result = (int)Math.Round(c * c * (double)ceil / 16129);
            if ((code & 0x80) != 0)
            {
                result = 0 - result;
            }

            return (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, result));
        }

        public static bool SelfTest()
        {
            bool passed = true;
            int ceil = 16000;
            StaticAverage error = new StaticAverage();
            for (int diff = 0 - ceil; diff <= ceil; diff++)
            {
                int actualDelta;
                byte code = SquareDeltaCodec.EncodeLargeJump((short)diff, ceil, out actualDelta);
                short decoded = SquareDeltaCodec.DecodeLargeJump(code, ceil);
                passed = passed && (actualDelta == decoded);
                passed = passed && Math.Abs(decoded - diff) < 255;
                error.Add(Math.Abs(decoded - diff));
            }

            passed = passed && error.Average < 100;
            return passed;
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
            private bool _sampleRateWritten = false;
            private int _ceil;
            private int _cur;

            internal Compressor(int sampleRate)
            {
                _sampleRate = sampleRate;
                _cur = 0;
                _ceil = GetCeiling(_sampleRate);
            }
            
            public byte[] Compress(AudioChunk audio)
            {
                short[] inputData = audio.Data;
                byte[] returnVal;
                int offset = 0;

                // This is legacy code - originally we would write the sample rate as 4 bytes directly into the output stream.
                // However, the codec now ignores this and uses CodecParams as normal
                if (!_sampleRateWritten)
                {
                    byte[] sampleRateField = BitConverter.GetBytes(_sampleRate);
                    returnVal = new byte[inputData.Length + 4];
                    Array.Copy(sampleRateField, returnVal, 4);
                    _sampleRateWritten = true;
                    offset = 4;
                }
                else
                {
                    returnVal = new byte[inputData.Length];
                }

                for (int c = 0; c < inputData.Length; c++)
                {
                    int next = inputData[c];
                    int diff = next - _cur;
                    int actualDelta;
                    byte code = EncodeLargeJump((short)diff, _ceil, out actualDelta);
                    returnVal[c + offset] = code;
                    _cur += actualDelta;
                }

                return returnVal;
            }

            public byte[] Close()
            {
                return null;
            }

            public string GetEncodeParams()
            {
                return CreateCodecParams(_sampleRate);
            }

            public void Dispose()
            {
            }
        }

        internal class Decompressor : IAudioDecompressionStream
        {
            private bool _sampleRateRead = false;
            private int _sampleRate = -1;
            private short _cur;
            private int _ceil = -1;

            internal Decompressor(string encodeParams)
            {
                _cur = 0;
                _sampleRate = GetSampleRateFromCodecParams(encodeParams).GetValueOrDefault(-1);
            }

            public AudioChunk Decompress(ArraySegment<byte> input)
            {
                int inputOffset = 0;
                if (!_sampleRateRead)
                {
                    _sampleRateRead = true;
                    if (_sampleRate < 0)
                    {
                        // fallback - if we couldn't read sample rate from codecparams, read it from the bitstream
                        _sampleRate = BitConverter.ToInt32(input.Array, input.Offset);
                    }

                    inputOffset = 4;
                }

                if (_ceil < 0)
                {
                    _ceil = GetCeiling(_sampleRate);
                }

                int cursor = 0;
                short[] audio = new short[input.Count - inputOffset];
                while (cursor + inputOffset < input.Count)
                {
                    short diff = DecodeLargeJump(input.Array[input.Offset + cursor + inputOffset], _ceil);
                    // Should never hit these first two cases. But just be safe, clamp the output value
                    if (((int)_cur + (int)diff) > short.MaxValue)
                    {
                        _cur = short.MaxValue;
                    }
                    else if (((int)_cur + (int)diff) < short.MinValue)
                    {
                        _cur = short.MinValue;
                    }
                    else
                    {
                        _cur += diff;
                    }
                    audio[cursor++] = _cur;
                }

                return new AudioChunk(audio, _sampleRate);
            }

            public AudioChunk Close()
            {
                return null;
            }

            public void Dispose()
            {
            }
        }
    }
}
