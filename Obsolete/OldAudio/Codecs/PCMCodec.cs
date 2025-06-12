using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs
{
    public class PCMCodec : IAudioCodec
    {
        public static string FORMAT_CODE => "pcm";
        private static readonly Regex SAMPLE_RATE_PARSER = new Regex("samplerate=([0-9]+)");
        private readonly ILogger _logger;

        public PCMCodec(ILogger logger = null)
        {
            if (logger != null)
                _logger = logger;
            else
                _logger = NullLogger.Singleton;
        }

        public static string CreateCodecParams(int sampleRate)
        {
            return "samplerate=" + sampleRate;
        }

        public static int? GetSampleRateFromCodecParams(string codecParams)
        {
            int returnVal;
            Match m = SAMPLE_RATE_PARSER.Match(codecParams);
            if (m.Success)
            {
                if (!int.TryParse(m.Groups[1].Value, out returnVal))
                {
                    return null;
                }

                return returnVal;
            }
            else
            {
                return null;
            }
        }

        public IAudioCompressionStream CreateCompressionStream(int inputSampleRate, Guid? traceId = null)
        {
            return new PCMCompressor(inputSampleRate);
        }

        public IAudioDecompressionStream CreateDecompressionStream(string encodeParams, Guid? traceId = null)
        {
            return new PCMDecompressor(encodeParams, _logger.CreateTraceLogger(traceId));
        }

        public string GetDescription()
        {
            return "Uncompressed PCM s16le";
        }

        public string GetFormatCode()
        {
            return FORMAT_CODE;
        }

        public bool Initialize()
        {
            return true;
        }

        private class PCMCompressor : IAudioCompressionStream
        {
            private int _sampleRate;

            public PCMCompressor(int sampleRate)
            {
                _sampleRate = sampleRate;
            }

            public byte[] Compress(AudioChunk input)
            {
                return input.GetDataAsBytes();
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

        public class PCMDecompressor : IAudioDecompressionStream
        {
            // This value is used to keep the block alignment to 2 bytes.
            // Otherwise the audio will get scrambled if we read an odd number of bytes.
            private int blockAlign = 0;

            private byte _oddByte;

            /// <summary>
            /// The sample rate, parsed from the encode params
            /// </summary>
            private int _sampleRate;

            private int _disposed = 0;

            public PCMDecompressor(int sampleRate)
            {
                _sampleRate = sampleRate;
            }

            public PCMDecompressor(string encodeParams, ILogger logger)
            {
                int? sampleRate = GetSampleRateFromCodecParams(encodeParams);
                if (!sampleRate.HasValue)
                {
                    logger.Log("Could not parse sample rate from wave stream parameters! Expecting \"samplerate=16000\". Params are \"" + encodeParams + "\"", LogLevel.Wrn);
                    _sampleRate = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE;
                }
                else
                {
                    _sampleRate = sampleRate.Value;
                }
            }

            public AudioChunk Decompress(ArraySegment<byte> input)
            {
                // Note that input may be an odd number of bytes. If so, keep the odd byte cached
                // That's what these variables are for
                int oldBlockAlign = blockAlign;
                byte oldOddByte = _oddByte;

                // Calculate the block size factoring in 2-byte alignment
                int blockSize = input.Count + oldBlockAlign;

                blockAlign = blockSize % 2;

                if (blockAlign == 1)
                {
                    blockSize -= 1;
                    _oddByte = input.Array[input.Offset + input.Count - 1];
                }

                byte[] actualBuffer = new byte[blockSize];

                if (oldBlockAlign == 1)
                {
                    // There is an odd byte carried over from the last block
                    actualBuffer[0] = oldOddByte;
                    Array.Copy(input.Array, input.Offset, actualBuffer, 1, blockSize - 1);
                }
                else
                {
                    Array.Copy(input.Array, input.Offset, actualBuffer, 0, blockSize);
                }

                return new AudioChunk(actualBuffer, _sampleRate);
            }

            public AudioChunk Close()
            {
                return null;
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                {
                    return;
                }

                if (!disposing) Durandal.Common.Utils.DebugMemoryLeaktracer.TraceDisposableItemFinalized(this.GetType());

                if (disposing)
                {
                }
            }
        }
    }
}
