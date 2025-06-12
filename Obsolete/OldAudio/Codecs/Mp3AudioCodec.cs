using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.Audio;
using Durandal.Common.AudioV2;
using Durandal.Common.Logger;

namespace Durandal.Common.Audio.Codecs
{
    using System.Diagnostics;
    using System.IO;
    using System.Threading;

    using Durandal.Common.Utils;
    using Durandal.API;

    /// <summary>
    /// Uses LAME libraries to manipulate mp3 audio
    /// </summary>
    public class Mp3AudioCodec : IAudioCodec
    {
        private readonly ILogger _logger;
        private string _lameExePath;

        public Mp3AudioCodec(ILogger logger, string lameExePath = ".\\ext\\lame.exe")
        {
            _logger = logger;
            _lameExePath = new FileInfo(lameExePath).FullName;
        }

        public string GetFormatCode()
        {
            return "mp3";
        }

        public string GetDescription()
        {
            return "MPEG Layer-3 audio codec (LAME)";
        }

        public bool Initialize()
        {
            if (!File.Exists(_lameExePath))
            {
                _logger.Log("LAME executable was not found at " + _lameExePath, LogLevel.Err);
                _lameExePath = null;
                return false;
            }

            return true;
        }

        public IAudioCompressionStream CreateCompressionStream(int inputSampleRate, Guid? traceId = null)
        {
            if (_lameExePath == null)
            {
                _logger.Log("Attempting to encode mp3 without lame.exe; aborting...", LogLevel.Err, traceId);
                return null;
            }

            if (inputSampleRate > 48000)
            {
                _logger.Log("Input audio to mp3 encoder is greater than 48khz; this is not allowed", LogLevel.Err, traceId);
                return null;
            }

            string encodeParams = "-V5 -m m -r -s " + ConvertSampleRateToCommandLineParam(inputSampleRate) + " --signed --little-endian --bitwidth 16 --flush --silent - -";
            return new Mp3CompressionStream(_lameExePath, encodeParams);
        }

        public IAudioDecompressionStream CreateDecompressionStream(string encodeParams, Guid? traceId = null)
        {
            if (_lameExePath == null)
            {
                _logger.Log("Attempting to decode mp3 without lame.exe; aborting...", LogLevel.Err, traceId);
                return null;
            }

            string decodeParams = "--decode --mp3input -m m - -";
            return new Mp3DecompressionStream(_lameExePath, decodeParams);
        }

        private class Mp3CompressionStream : CommandLineEncoderStreamBase
        {
            public Mp3CompressionStream(string lamePath, string lameParams)
                : base(lamePath, lameParams) { }
        }

        private class Mp3DecompressionStream : CommandLineDecoderStreamBase
        {
            public Mp3DecompressionStream(string lamePath, string lameParams)
                : base(lamePath, lameParams) { }
        }

        private string ConvertSampleRateToCommandLineParam(int input)
        {
            switch (input)
            {
                case 8000:
                    return "8";
                case 11025:
                    return "11.025";
                case 12000:
                    return "12";
                case 16000:
                    return "16";
                case 22050:
                    return "22.05";
                case 24000:
                    return "24";
                case 32000:
                    return "32";
                case 44100:
                    return "44.1";
                case 48000:
                    return "48";
                default:
                    return "44.1";
            }
        }
    }
}
