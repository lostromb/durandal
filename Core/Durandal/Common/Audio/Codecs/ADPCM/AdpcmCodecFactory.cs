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

namespace Durandal.Common.Audio.Codecs.ADPCM
{
    public class AdpcmCodecFactory : IAudioCodecFactory
    {
        private readonly int _lookahead;
        private readonly NoiseShaping _noiseShaping;

        public static readonly string CODEC_NAME = "adpcm_ima";

        private static readonly Durandal.Common.Collections.IReadOnlySet<string> SUPPORTED_CODECS = new ReadOnlySetWrapper<string>(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CODEC_NAME,
        });

        /// <summary>
        /// Constructs a new <see cref="AdpcmCodecFactory"/> with default parameters.
        /// </summary>
        public AdpcmCodecFactory() : this(0, ADPCM.NoiseShaping.NOISE_SHAPING_DYNAMIC)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="AdpcmCodecFactory"/> with customized encoding parameters.
        /// </summary>
        /// <param name="lookahead">The number of samples to lookahead when encoding. "High" is about 3.
        /// Exponential complexity increase with each +1 lookahead</param>
        /// <param name="noiseShaping">The noise shaping to use when encoding. Has a small performance impact.</param>
        public AdpcmCodecFactory(int lookahead, ADPCM.NoiseShaping noiseShaping)
        {
            _lookahead = lookahead.AssertNonNegative(nameof(lookahead));
            _noiseShaping = noiseShaping;
        }

        public Durandal.Common.Collections.IReadOnlySet<string> SupportedEncodeFormats => SUPPORTED_CODECS;

        public Durandal.Common.Collections.IReadOnlySet<string> SupportedDecodeFormats => SUPPORTED_CODECS;

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

            return new AdpcmDecoder(graph, codecParams, nodeCustomName);
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

            return new AdpcmEncoder(graph, desiredInputFormat, nodeCustomName, _lookahead, _noiseShaping);
        }
    }
}
