using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Interfaces;
using Durandal.Common.Logger;
using Durandal.Common.Audio.Codecs.ILBC;

namespace Durandal.Common.Audio.Codecs
{
    using Durandal.Common.Utils;
    using System.IO;

    /// <summary>
    /// A wrapper for the Internet Low Band Communication codec
    /// </summary>
    public class ILBCAudioCodec : IAudioCodec
    {
        private readonly ILogger _logger;

        public ILBCAudioCodec(ILogger logger)
        {
            _logger = logger;
        }

        public string GetFormatCode()
        {
            return "ilbc";
        }

        public string GetDescription()
        {
            return "Internet Low Bitrate Codec (iLBC)";
        }

        public bool Initialize()
        {
            return true;
        }

        public IAudioCompressionStream CreateCompressionStream(int inputSampleRate, Guid? traceId = null)
        {
            return new Compressor(_logger.CreateTraceLogger(traceId));
        }

        public IAudioDecompressionStream CreateDecompressionStream(string encodeParams, Guid? traceId = null)
        {
            return new Decompressor(_logger.CreateTraceLogger(traceId), encodeParams);
        }

        internal class Compressor : IAudioCompressionStream
        {
            private ILogger _logger;
            private ilbc_encoder _encoder;
            private string _encodeParams;
            private short[] _buffer;
            private int _bufferIndex;
            private int _frameSize;

            internal Compressor(ILogger logger)
            {
                _logger = logger;
                _encoder = new ilbc_encoder(30);
                _encodeParams = string.Format("block=30ms");
                _frameSize = 240;
                _buffer = new short[_frameSize];
                _bufferIndex = 0;
            }
            
            public byte[] Compress(AudioChunk audio)
            {
                if (audio == null || audio.Data == null || audio.Data.Length == 0)
                {
                    _logger.Log("Null data sent to iLBC encoder", LogLevel.Wrn);
                    return BinaryHelpers.EMPTY_BYTE_ARRAY;
                }

                // Resample audio to 16k if needed
                if (audio.SampleRate != AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE)
                {
                    _logger.Log("iLBC codec only supports 16khz audio samples; resampling input from " + audio.SampleRate, LogLevel.Wrn);
                    audio = audio.ResampleToBad(16000);
                }

                short[] inputData = audio.Data;
                short[] encodedBlock = new short[25];
                byte[] outputData = new byte[inputData.Length / 2];
                int outCursor = 0;
                int inCursor = 0;
                while (inCursor < inputData.Length)
                {
                    int toCopy = Math.Min(inputData.Length - inCursor, _frameSize - _bufferIndex);
                    Array.Copy(inputData, inCursor, _buffer, _bufferIndex, toCopy);
                    inCursor += toCopy;
                    _bufferIndex += toCopy;

                    // Buffer is filled, so we can now encode a frame and write it
                    if (_bufferIndex >= _frameSize)
                    {
                        // do the encode; encodedBytes should always be 50
                        short encodedBytes = _encoder.encode(encodedBlock, _buffer);

                        // dump output to outputData
                        byte[] byteOutput = AudioMath.ShortsToBytes(encodedBlock);
                        Array.Copy(byteOutput, 0, outputData, outCursor, encodedBytes);
                        outCursor += encodedBytes;
                        _bufferIndex = 0;
                    }
                }

                // Trim the output buffer to size and return it
                byte[] returnVal = new byte[outCursor];
                Array.Copy(outputData, returnVal, outCursor);
                return returnVal;
            }

            public byte[] Close()
            {
                // Check if there is any data to write
                if (_bufferIndex == 0)
                {
                    // Encoding ended exactly on a frame boundary. We're good!
                    return BinaryHelpers.EMPTY_BYTE_ARRAY;
                }
                else
                {
                    // If there's still a block left, pad it to the frame size of the encoder and write it
                    for (int c = _bufferIndex; c < _buffer.Length; c++)
                    {
                        _buffer[c] = 0;
                    }

                    short[] encodedBlock = new short[25];
                    short encodedBytes = _encoder.encode(encodedBlock, _buffer);
                    return AudioMath.ShortsToBytes(encodedBlock, 0, encodedBytes / 2);
                }
            }

            public string GetEncodeParams()
            {
                return _encodeParams;
            }

            public void Dispose()
            {
            }
        }

        internal class Decompressor : IAudioDecompressionStream
        {
            private const int _frameSize = 50;

            private ILogger _logger;
            private byte[] _buffer;
            private int _bufferIndex;
            ilbc_decoder _decoder;

            internal Decompressor(ILogger logger, string encodeParams)
            {
                _logger = logger;
                _buffer = new byte[_frameSize];
                _bufferIndex = 0;
                _decoder = new ilbc_decoder(30, 1);
            }

            public AudioChunk Decompress(ArraySegment<byte> inputData)
            {
                if (inputData.Array == null || inputData.Count == 0)
                {
                    _logger.Log("Null data sent to iLBC decoder", LogLevel.Wrn);
                    return null;
                }

                // The input is always compressed at a 9:1 ratio so we have a good idea of the output size
                short[] outputData = new short[inputData.Count * 6];
                short[] decodedBlock = new short[240];
                int outCursor = 0;
                int inCursor = 0;
                while (inCursor < inputData.Count)
                {
                    int toCopy = Math.Min(inputData.Count - inCursor, _frameSize - _bufferIndex);
                    Array.Copy(inputData.Array, inCursor + inputData.Offset, _buffer, _bufferIndex, toCopy);
                    inCursor += toCopy;
                    _bufferIndex += toCopy;

                    // Buffer is filled, so we can now encode a frame and write it
                    if (_bufferIndex >= _frameSize)
                    {
                        short[] encodedBlock = AudioMath.BytesToShorts(_buffer);
                        short decodedSamples = _decoder.decode(decodedBlock, encodedBlock, 1);
                        // decodedSamples should always be 240
                        Array.Copy(decodedBlock, 0, outputData, outCursor, decodedSamples);
                        outCursor += decodedSamples;
                        _bufferIndex = 0;
                    }
                }

                // Trim the output buffer to size and return it
                short[] returnVal = new short[outCursor];
                Array.Copy(outputData, returnVal, outCursor);
                return new AudioChunk(returnVal, AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE);
            }

            public AudioChunk Close()
            {
                // there should never be an incomplete block; so this is a noop
                return null;
            }

            public void Dispose()
            {
            }
        }
    }
}
