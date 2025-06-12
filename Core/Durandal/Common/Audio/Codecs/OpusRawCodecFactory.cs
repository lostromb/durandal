using Durandal.Common.Audio;
using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Codecs.Opus.Enums;
using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using Durandal.Common.Utils;
using Durandal.Common.Utils.NativePlatform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs
{
    public sealed class OpusRawCodecFactory : IAudioCodecFactory
    {
        /// <summary>
        /// The codec code for length-delimited opus
        /// </summary>
        public static readonly string CODEC_NAME = "opus";

        private static readonly Durandal.Common.Collections.IReadOnlySet<string> SUPPORTED_CODECS = new ReadOnlySetWrapper<string>(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CODEC_NAME });

        private readonly int _complexity;
        private readonly int _bitrateKbps;
        private readonly OpusMode _forceMode;
        private readonly OpusApplication _audioTypeHint;
        private readonly OpusFramesize _frameSize;
        private readonly ILogger _logger;
        private readonly TimeSpan? _realTimeDecodingBudget;
        private readonly AudioSampleFormat _maxSupportedOutputFormat;

        public Durandal.Common.Collections.IReadOnlySet<string> SupportedEncodeFormats => SUPPORTED_CODECS;

        public Durandal.Common.Collections.IReadOnlySet<string> SupportedDecodeFormats => SUPPORTED_CODECS;

        /// <summary>
        /// Constructs a new codec factory for passing self-delimited Opus audio packets around.
        /// </summary>
        /// <param name="logger">A logger</param>
        /// <param name="complexity">The encoder complexity to use, from 0 to 10</param>
        /// <param name="bitrateKbps">The encoder bitrate to use, in KBps from 6 to 510</param>
        /// <param name="forceMode">An optional forced mode for the encoder</param>
        /// <param name="audioTypeHint">A hint for the type of audio being encoded</param>
        /// <param name="frameSize">The default framesize to use in the encoder</param>
        /// <param name="maxSupportedOutputFormat">If you want to conserve resources by avoiding decoding high-fidelity audio, set this format to the highest fidelity audio you want to decode to</param>
        /// <param name="realTimeDecodingBudget">An optional limiter for the amount of time to spend on any one decoding loop, intended to avoid stutter when filling initial buffers</param>
        public OpusRawCodecFactory(
            ILogger logger,
            int complexity = 0,
            int bitrateKbps = 40,
            OpusMode forceMode = OpusMode.MODE_AUTO,
            OpusApplication audioTypeHint = OpusApplication.OPUS_APPLICATION_VOIP,
            OpusFramesize frameSize = OpusFramesize.OPUS_FRAMESIZE_5_MS,
            AudioSampleFormat maxSupportedOutputFormat = null,
            TimeSpan? realTimeDecodingBudget = null)
        {
            _complexity = complexity;
            _bitrateKbps = bitrateKbps;
            _forceMode = forceMode;
            _audioTypeHint = audioTypeHint;
            _frameSize = frameSize;
            _logger = logger;
            _realTimeDecodingBudget = realTimeDecodingBudget;
            _maxSupportedOutputFormat = maxSupportedOutputFormat;
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
            
            return new OpusRawDecoder(graph, nodeCustomName, traceLogger, codecParams, _maxSupportedOutputFormat, _realTimeDecodingBudget);
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

            return new OpusRawEncoder(
                graph,
                compatibleOpusEncodeFormat,
                nodeCustomName,
                _logger,
                _complexity,
                _bitrateKbps,
                _forceMode,
                _audioTypeHint,
                _frameSize);
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
