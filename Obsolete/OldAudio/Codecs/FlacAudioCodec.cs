namespace Durandal.Common.Audio.Codecs
{
    using Durandal.API;
    using Durandal.Common.Audio;
    using Durandal.Common.AudioV2;
    using Durandal.Common.Logger;
    using System;
    using System.IO;

    /// <summary>
    /// Wrapper for Flac encoder/decoder
    /// This codec relies on the existence of ./ext/flac.exe
    /// </summary>
    public class FlacAudioCodec : IAudioCodec
    {
        private ILogger _logger;
        private string _flacExePath;

        public FlacAudioCodec(ILogger logger, string flacExePath = ".\\ext\\flac.exe")
        {
            _logger = logger;
            _flacExePath = new FileInfo(flacExePath).FullName;
        }
        
        public string GetFormatCode()
        {
            return "flac";
        }

        public string GetDescription()
        {
            return "Xiph.org Free Lossless audio codec";
        }

        public bool Initialize()
        {
            if (!File.Exists(_flacExePath))
            {
                _logger.Log("Flac executable was not found at " + _flacExePath, LogLevel.Err);
                _flacExePath = null;
                return false;
            }
            return true;
        }

        public IAudioCompressionStream CreateCompressionStream(int inputSampleRate, Guid? traceId = null)
        {
            if (_flacExePath == null)
            {
                _logger.Log("Attempting to encode .flac without flac.exe; aborting...", LogLevel.Err, traceId);
                return null;
            }

            string encodeParams =
                "--fast --force-raw-format --sample-rate=" + inputSampleRate + " --channels=1 --endian=little --sign=signed --bps=16 -";
            return new FlacCompressionStream(_flacExePath, encodeParams);
        }

        public IAudioDecompressionStream CreateDecompressionStream(string encodeParams, Guid? traceId = null)
        {
            if (_flacExePath == null)
            {
                _logger.Log("Attempting to decode .flac without flac.exe; aborting...", LogLevel.Err, traceId);
                return null;
            }

            string decodeParams = "-d -c -";
            return new FlacDecompressionStream(_flacExePath, decodeParams);
        }

        private class FlacCompressionStream : CommandLineEncoderStreamBase
        {
            public FlacCompressionStream(string flacPath, string flacParams)
                : base(flacPath, flacParams) { }
        }

        private class FlacDecompressionStream : CommandLineDecoderStreamBase
        {
            public FlacDecompressionStream(string flacPath, string flacParams)
                : base(flacPath, flacParams) { }
        }
    }
}
