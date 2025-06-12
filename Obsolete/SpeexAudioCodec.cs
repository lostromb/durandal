using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NSpeex;
using Durandal.Common.Audio;
using Durandal.Common.Audio.Interfaces;
using Stromberg.Logger;

namespace Durandal.Common.Speech.Codecs
{
    using System.IO;

    /// <summary>
    /// Uses Speex libraries to compress audio at a 8% rate
    /// </summary>
    public class SpeexAudioCodec : IAudioCodec
    {
        // TODO: Add variable quality and wide/narrowband support
        private readonly ILogger _logger;
        
        public SpeexAudioCodec(ILogger logger)
        {
            _logger = logger;
        }

        public string GetFormatCode()
        {
            return "speex";
        }

        public string GetDescription()
        {
            return "Xiph.org Speex audio codec";
        }

        public bool Initialize()
        {
            return true;
        }

        public IAudioCompressionStream CreateCompressionStream(int inputSampleRate, string traceId = null)
        {
            return new Compressor(_logger.Clone(_logger.GetComponentName(), traceId), inputSampleRate);
        }

        public IAudioDecompressionStream CreateDecompressionStream(string encodeParams, string traceId = null)
        {
            return new Decompressor(_logger.Clone(_logger.GetComponentName(), traceId), encodeParams);
        }

        internal class Compressor : IAudioCompressionStream
        {
            private const int ENCODE_QUALITY = 5;
            
            private ILogger _logger;
            private SpeexEncoder _encoder;
            private string _encodeParams;
            private short[] _buffer;
            private int _bufferIndex;
            private int _frameSize;
            private int _sampleRate;

            internal Compressor(ILogger logger, int inputSampleRate)
            {
                _logger = logger;
                
                // Determine the sample rate (Speex "band") to use
                BandMode band = DetermineBandMode(inputSampleRate);

                _encoder = new SpeexEncoder(BandMode.Wide)
                    {
                        Quality = ENCODE_QUALITY,
                        VBR = false
                    };
                _encodeParams = string.Format("q={0} sr={1} ch=1", ENCODE_QUALITY, _sampleRate);
                _frameSize = _encoder.FrameSize;
                _buffer = new short[_frameSize];
                _bufferIndex = 0;
            }

            private BandMode DetermineBandMode(int inputSampleRate)
            {
                if (inputSampleRate >= 32000)
                {
                    _sampleRate = 32000;
                    return BandMode.UltraWide;
                }
                else if (inputSampleRate >= 16000)
                {
                    _sampleRate = 16000;
                    return BandMode.Wide;
                }
                else
                {
                    _sampleRate = 8000;
                    return BandMode.Narrow;
                }
            }

            public byte[] Compress(AudioChunk audio)
            {
                if (audio == null || audio.Data == null || audio.Data.Length == 0)
                {
                    _logger.Log("Null data sent to speex encoder", LogLevel.Wrn);
                    return new byte[0];
                }

                // Resample audio to 16k if needed
                if (audio.SampleRate != AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE)
                {
                    _logger.Log("Speex codec only supports 16khz audio samples; resampling input from " + audio.SampleRate, LogLevel.Wrn);
                    audio = audio.ResampleToBad(AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE);
                }

                short[] inputData = audio.Data;
                byte[] outputData = new byte[inputData.Length * 4];
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
                        int encodedSize = _encoder.Encode(_buffer, 0, _frameSize, outputData, outCursor, _frameSize);
                        outCursor += encodedSize;
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
                    return new byte[0];
                }
                else
                {
                    // If there's still a block left, pad it to the frame size of the encoder and write it
                    for (int c = _bufferIndex; c < _buffer.Length; c++)
                    {
                        _buffer[c] = 0;
                    }

                    byte[] outputData = new byte[_buffer.Length * 4];
                    int outCursor = 0;

                    if (_bufferIndex >= _frameSize)
                    {
                        int encodedSize = _encoder.Encode(_buffer, 0, _frameSize, outputData, outCursor, outputData.Length - outCursor);
                        outCursor += encodedSize;
                    }

                    byte[] returnVal = new byte[outCursor];
                    Array.Copy(outputData, returnVal, outCursor);
                    return returnVal;
                }
            }

            public string GetEncodeParams()
            {
                return _encodeParams;
            }
        }

        internal class Decompressor : IAudioDecompressionStream
        {
            private ILogger _logger;
            private SpeexDecoder _decoder;
            private byte[] _buffer;
            private int _bufferIndex;
            private int _frameSize;
            private int _outputSampleRate;

            internal Decompressor(ILogger logger, string encodeParams)
            {
                _logger = logger;

                // TODO: PARSE FRAME SIZE AND SAMPLE RATE FROM ENCODE PARAMS
                _frameSize = 42;
                _outputSampleRate = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE;

                try
                {
                    _decoder = new SpeexDecoder(BandMode.Wide);
                }
                catch (Exception e)
                {
                    _logger.Log(e.Message, LogLevel.Err);
                }

                _buffer = new byte[_frameSize];
                _bufferIndex = 0;
            }

            public AudioChunk Decompress(byte[] inputData)
            {
                if (inputData == null || inputData.Length == 0)
                {
                    _logger.Log("Null data sent to speex decoder", LogLevel.Wrn);
                    return null;
                }

                // FIXME: Possible silent buffer overflow
                short[] outputData = new short[inputData.Length * 20];
                int outCursor = 0;
                int inCursor = 0;
                while (inCursor < inputData.Length)
                {
                    int toCopy = Math.Min(inputData.Length - inCursor, _frameSize - _bufferIndex);
                    // todo: eliminate this copy by writing directly to a large output buffer
                    Array.Copy(inputData, inCursor, _buffer, _bufferIndex, toCopy);
                    inCursor += toCopy;
                    _bufferIndex += toCopy;

                    // Buffer is filled, so we can now encode a frame and write it
                    if (_bufferIndex >= _frameSize)
                    {
                        int encodedSize = _decoder.Decode(_buffer, 0, _frameSize, outputData, outCursor, false);
                        outCursor += encodedSize;
                        _bufferIndex = 0;
                    }
                }

                // Trim the output buffer to size and return it
                short[] returnVal = new short[outCursor];
                Array.Copy(outputData, returnVal, outCursor);
                return new AudioChunk(returnVal, _outputSampleRate);
            }

            public AudioChunk Close()
            {
                return null;
            }
        }
    }
}
