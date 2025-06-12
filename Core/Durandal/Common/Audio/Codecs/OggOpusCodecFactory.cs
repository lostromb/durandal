using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Codecs.Opus.Enums;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs
{
    public sealed class OggOpusCodecFactory : IAudioCodecFactory
    {
        /// <summary>
        /// The codec code for opus in ogg container
        /// </summary>
        public static readonly string CODEC_NAME = "oggopus";

        private static readonly Durandal.Common.Collections.IReadOnlySet<string> SUPPORTED_CODECS = new ReadOnlySetWrapper<string>(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CODEC_NAME });

        private readonly int _complexity;
        private readonly int _bitrateKbps;
        private readonly OpusMode _forceMode;
        private readonly OpusApplication _audioTypeHint;
        private readonly ILogger _logger;
        private readonly TimeSpan? _realTimeDecodingBudget;

        public Durandal.Common.Collections.IReadOnlySet<string> SupportedEncodeFormats => SUPPORTED_CODECS;

        public Durandal.Common.Collections.IReadOnlySet<string> SupportedDecodeFormats => SUPPORTED_CODECS;

        public OggOpusCodecFactory(
            int complexity = 2,
            int bitrateKbps = 96,
            OpusMode forceMode = OpusMode.MODE_AUTO,
            OpusApplication audioTypeHint = OpusApplication.OPUS_APPLICATION_AUDIO,
            TimeSpan? realTimeDecodingBudget = null)
        {
            _complexity = complexity;
            _bitrateKbps = bitrateKbps;
            _forceMode = forceMode;
            _audioTypeHint = audioTypeHint;
            _logger = NullLogger.Singleton; // FIXME hook this up
            _realTimeDecodingBudget = realTimeDecodingBudget;
        }

        public bool CanEncode(string codecName)
        {
            return SUPPORTED_CODECS.Contains(codecName);
        }

        public bool CanDecode(string codecName)
        {
            return SUPPORTED_CODECS.Contains(codecName);
        }

        public AudioDecoder CreateDecoder(
            string codecName,
            string codecParams,
            WeakPointer<IAudioGraph> graph,
            ILogger traceLogger,
            string nodeCustomName)
        {
            if (!CanDecode(codecName))
            {
                throw new ArgumentException("Cannot create a decoder for \"" + codecName + "\". Supported decode formats are " + string.Join(",", SupportedDecodeFormats));
            }

            return new OggOpusDecoder(graph, nodeCustomName, traceLogger, _realTimeDecodingBudget);
        }

        public AudioEncoder CreateEncoder(
            string codecName,
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat desiredInputFormat,
            ILogger traceLogger,
            string nodeCustomName)
        {
            if (!CanEncode(codecName))
            {
                throw new ArgumentException("Cannot create an encoder for \"" + codecName + "\". Supported encode formats are " + string.Join(",", SupportedEncodeFormats));
            }

            // Create an encoder input format which most closely matches the actual input format
            AudioSampleFormat compatibleOpusEncodeFormat = new AudioSampleFormat(
                ConvertToSupportedOpusSampleRate(desiredInputFormat.SampleRateHz),
                desiredInputFormat.NumChannels,
                desiredInputFormat.ChannelMapping);

            return new OggOpusEncoder(
                graph,
                compatibleOpusEncodeFormat,
                nodeCustomName,
                _logger,
                _complexity,
                _bitrateKbps,
                _forceMode,
                _audioTypeHint);
        }

        private static int ConvertToSupportedOpusSampleRate(int desiredSampleRate)
        {
            if (desiredSampleRate <= 8000)
            {
                return 8000;
            }
            else if (desiredSampleRate <= 12000)
            {
                return 12000;
            }
            else if (desiredSampleRate <= 16000)
            {
                return 16000;
            }
            else if (desiredSampleRate <= 24000)
            {
                return 24000;
            }
            else
            {
                return 48000;
            }
        }
    }
}
