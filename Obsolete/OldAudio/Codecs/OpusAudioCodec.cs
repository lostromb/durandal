using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.File;

namespace Durandal.Common.Audio.Codecs
{
    using System.Diagnostics;
    using System.IO;
    using System.Threading;

    using Durandal.Common.Audio;
    using Durandal.Common.Audio.Interfaces;
    using Durandal.Common.Logger;
    using Durandal.Common.Utils;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    using Durandal.Common.Audio.Codecs.Opus.Structs;
    using Durandal.Common.Audio.Codecs.Opus.Enums;
    using Durandal.Common.Audio.Codecs.Opus;
    using Durandal.Common.IO;

    /// <summary>
    /// Wrapper for OPUS encoder/decoder (using pure C# backend)
    /// </summary>
    public class OpusAudioCodec : IAudioCodec
    {
        private const int FRAME_SIZE = 10;
        private readonly ILogger _logger;
        private int _bitrate = 32;
        private int _complexity = 0;

        public OpusAudioCodec(ILogger logger, int complexity = 0, int bitrate = 32)
        {
            _logger = logger;
            _complexity = complexity;
            _bitrate = bitrate;
        }

        /// <summary>
        /// The bitrate to use for encoding from 6 to 500
        /// </summary>
        public int QualityKbps
        {
            get
            {
                return _bitrate;
            }
            set
            {
                _bitrate = value;
            }
        }

        /// <summary>
        /// The encoder complexity from 0 to 10
        /// </summary>
        public int Complexity
        {
            get
            {
                return _complexity;
            }
            set
            {
                _complexity = value;
            }
        }

        public string GetFormatCode()
        {
            return "opus";
        }

        public string GetDescription()
        {
            return "Opus audio codec 1.1.2 (via " + Durandal.Common.Audio.Codecs.Opus.CodecHelpers.GetVersionString() + ")";
        }

        public bool Initialize()
        {
            return true;
        }

        public IAudioCompressionStream CreateCompressionStream(int inputSampleRate, Guid? traceId = null)
        {
            OpusCompressionStream returnVal = new OpusCompressionStream(inputSampleRate, _bitrate, _complexity, _logger.CreateTraceLogger(traceId ?? _logger.TraceId));
            if (!returnVal.Initialize())
            {
                returnVal.Dispose();
                return null;
            }

            return returnVal;
        }

        public IAudioDecompressionStream CreateDecompressionStream(string encodeParams, Guid? traceId = null)
        {
            OpusDecompressionStream returnVal = new OpusDecompressionStream(encodeParams, _logger.CreateTraceLogger(traceId ?? _logger.TraceId));
            if (!returnVal.Initialize())
            {
                returnVal.Dispose();
                return null;
            }

            return returnVal;
        }

        private class OpusCompressionStream : IAudioCompressionStream
        {
            private readonly BasicBufferShort _incomingSamples;
            private readonly int _sampleRate = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE;
            private readonly int _qualityKbps;
            private readonly int _complexity;
            private readonly ILogger _logger;

            /// <summary>
            /// The encoder state object
            /// </summary>
            private OpusEncoder concentusEncoder;

            private int _disposed = 0;

            public OpusCompressionStream(int sampleRate, int qualityKbps, int complexity, ILogger logger)
            {
                _sampleRate = FindSampleRateFloor(sampleRate);
                _qualityKbps = qualityKbps;
                _logger = logger;
                _complexity = complexity;

                // Buffer for 1 second of input
                _incomingSamples = new BasicBufferShort(_sampleRate * 1);
            }

            private int FindSampleRateFloor(int desiredSampleRate)
            {
                if (desiredSampleRate >= 48000)
                {
                    return 48000;
                }
                if (desiredSampleRate >= 24000)
                {
                    return 24000;
                }
                if (desiredSampleRate >= 16000)
                {
                    return 16000;
                }
                if (desiredSampleRate >= 12000)
                {
                    return 12000;
                }

                return 8000;
            }

            public bool Initialize()
            {
                try
                {
                    concentusEncoder = new OpusEncoder(_sampleRate, 1, OpusApplication.OPUS_APPLICATION_AUDIO);

                    // Set the encoder bitrate and complexity
                    concentusEncoder.Bitrate = _qualityKbps * 1024;
                    concentusEncoder.Complexity = _complexity;
                    concentusEncoder.ForceMode = OpusMode.MODE_CELT_ONLY; // CELT mode is much faster than hybrid so force it
                    concentusEncoder.UseVBR = true;

                    _logger.Log("Initializing Opus compression stream with samplerate=" + _sampleRate + ", bitrate=" + _qualityKbps + ", complexity=" + _complexity);

                    return true;
                }
                catch (OpusException e)
                {
                    _logger.Log("Exception while initializing Opus encoder!", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    return false;
                }
            }

            private int GetFrameSize()
            {
                // 10ms window is used for all packets
                return _sampleRate * FRAME_SIZE / 1000;
            }

            public byte[] Compress(AudioChunk input)
            {
                int frameSize = GetFrameSize();

                if (input != null)
                {
                    short[] newData = input/*.ResampleTo(_sampleRate)*/.Data;
                    if (_incomingSamples.Capacity - _incomingSamples.Available < newData.Length)
                    {
                        _logger.Log("Buffer overrun! Too much input audio was piped into Opus compression stream at once", LogLevel.Wrn);
                    }

                    _incomingSamples.Write(newData);
                }
                else
                {
                    // If input is null, assume we are at end of stream and pad the output with zeroes
                    int paddingNeeded = _incomingSamples.Available % frameSize;
                    if (paddingNeeded > 0)
                    {
                        _incomingSamples.Write(new short[paddingNeeded]);
                    }
                }

                try
                {
                    // Calculate the approximate amount of bits required to compress the incoming audio using the current opus bitrate
                    float timeSpanMs = (float)(_incomingSamples.Available * 1000L / AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE);
                    float bytesPerMs = _qualityKbps * 128 / 1000;
                    int approxOpusFrameSize = Math.Min(1250, Math.Max(10, (int)(FRAME_SIZE * bytesPerMs * 1.10f)));
                    int approxOutBufferSize = (int)(timeSpanMs * bytesPerMs * 1.10f);
                    byte[] outputBuffer = new byte[approxOutBufferSize];
                    int outCursor = 0;

                    while (outCursor <= approxOutBufferSize - approxOpusFrameSize && _incomingSamples.Available >= frameSize)
                    {
                        short[] nextFrameData = _incomingSamples.Read(frameSize);
                        short thisPacketSize = (short)concentusEncoder.Encode(nextFrameData, 0, frameSize, outputBuffer, outCursor + 2, approxOpusFrameSize - 2);
                        byte[] packetSize = BitConverter.GetBytes(thisPacketSize);
                        outputBuffer[outCursor++] = packetSize[0];
                        outputBuffer[outCursor++] = packetSize[1];
                        outCursor += thisPacketSize;
                    }

                    byte[] finalOutput = new byte[outCursor];
                    Array.Copy(outputBuffer, 0, finalOutput, 0, outCursor);
                    //_logger.Log(BinaryHelpers.ToHexString(finalOutput, 0, outCursor));
                    return finalOutput;
                }
                catch (OpusException e)
                {
                    _logger.Log("Opus encoder threw an exception", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    return BinaryHelpers.EMPTY_BYTE_ARRAY;
                }
            }

            public byte[] Close()
            {
                byte[] trailer = Compress(null);
                return trailer;
            }

            public string GetEncodeParams()
            {
                return string.Format("samplerate={0} q=0 framesize={1}", _sampleRate, FRAME_SIZE);
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

        private class OpusDecompressionStream : IAudioDecompressionStream
        {
            private readonly BasicBufferByte _incomingBytes;
            private readonly float _outputFrameLengthMs;
            private readonly int _outputSampleRate;
            private readonly ILogger _logger;

            private OpusDecoder _hDecoder;
            private int _nextPacketSize = 0;
            private int _disposed = 0;

            public OpusDecompressionStream(string encodeParams, ILogger logger)
            {
                _incomingBytes = new BasicBufferByte(1275 + 2); // 1275 is maximum Opus packet size, plus two bytes for the packet length prefix
                _logger = logger;

                Match sampleRateParse = Regex.Match(encodeParams, "samplerate=([0-9]+)");
                if (sampleRateParse.Success)
                {
                    _outputSampleRate = int.Parse(sampleRateParse.Groups[1].Value);
                }
                else
                {
                    _outputSampleRate = AudioUtils.DURANDAL_INTERNAL_SAMPLE_RATE;
                }

                Match frameSizeParse = Regex.Match(encodeParams, "framesize=([0-9]+)");
                if (frameSizeParse.Success)
                {
                    _outputFrameLengthMs = float.Parse(frameSizeParse.Groups[1].Value);
                }
                else
                {
                    _outputFrameLengthMs = FRAME_SIZE;
                }

                _logger.Log("Initializing Opus decompression stream with samplerate=" + _outputSampleRate + " and framesize=" + _outputFrameLengthMs);
            }

            public bool Initialize()
            {
                try
                {
                    _hDecoder = new OpusDecoder(_outputSampleRate, 1);
                    return true;
                }
                catch (OpusException e)
                {
                    _logger.Log("Exception while initializing Opus decoder!", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    return false;
                }
            }

            public AudioChunk Decompress(ArraySegment<byte> input)
            {
                int frameSize = GetFrameSize();

                if (input.Array == null)
                {
                    return null;
                }

                IList<short[]> outputFrames = new List<short[]>();
                byte[] packetSizeBuf = new byte[2];
                byte[] packetBuf = new byte[1275];
                int outputLength = 0;
                int outCursor = 0;
                int inCursor = 0;

                try
                {
                    while (inCursor < input.Count)
                    {
                        // Fill up our packet buffer with data from input
                        int toReadFromInput = Math.Min(input.Count - inCursor, _incomingBytes.Capacity - _incomingBytes.Available);
                        _incomingBytes.Write(input.Array, input.Offset + inCursor, toReadFromInput);
                        inCursor += toReadFromInput;

                        // Read initial packet size if needed
                        if (_nextPacketSize <= 0 && _incomingBytes.Available >= 2)
                        {
                            _incomingBytes.Read(packetSizeBuf, 0, 2);
                            _nextPacketSize = BitConverter.ToInt16(packetSizeBuf, 0);
                        }

                        // And read as many packets as possible
                        while (_nextPacketSize > 0 && _incomingBytes.Available >= _nextPacketSize)
                        {
                            short[] outputBuffer = new short[frameSize];
                            _incomingBytes.Read(packetBuf, 0, _nextPacketSize);
                            int thisFrameSize = _hDecoder.Decode(packetBuf, 0, _nextPacketSize, outputBuffer, 0, frameSize, false);
                            outCursor += thisFrameSize * 2;
                            outputFrames.Add(outputBuffer);
                            outputLength += outputBuffer.Length;

                            if (_incomingBytes.Available >= 2)
                            {
                                _incomingBytes.Read(packetSizeBuf, 0, 2);
                                _nextPacketSize = BitConverter.ToInt16(packetSizeBuf, 0);
                            }
                            else
                            {
                                _nextPacketSize = 0;
                            }
                        }
                    }

                    if (outputLength == 0)
                        return null;

                    short[] finalOutput = new short[outputLength];
                    int outCur = 0;
                    foreach (short[] frame in outputFrames)
                    {
                        Array.Copy(frame, 0, finalOutput, outCur, frame.Length);
                        outCur += frame.Length;
                    }

                    return new AudioChunk(finalOutput, _outputSampleRate);
                }
                catch (OpusException e)
                {
                    _logger.Log("Opus decoder threw an exception", LogLevel.Err);
                    _logger.Log(e, LogLevel.Err);
                    return null;
                }
            }

            private int GetFrameSize()
            {
                // 10ms window is the default used for all packets in this encoder
                return (int)(_outputSampleRate * _outputFrameLengthMs / 1000);
            }

            public AudioChunk Close()
            {
                AudioChunk trailer = Decompress(new ArraySegment<byte>());
                return trailer;
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
