using Durandal.Common.Collections;
using Durandal.Common.IO;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs
{
    public class ULawCodecFactory : IAudioCodecFactory
    {
        /// <summary>
        /// The codec code for 8-bit μ-law codec
        /// </summary>
        public static readonly string CODEC_NAME = "ulaw";

        private static readonly Durandal.Common.Collections.IReadOnlySet<string> SUPPORTED_CODECS = new ReadOnlySetWrapper<string>(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CODEC_NAME });

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

            return new ULawDecoder(graph, codecParams, nodeCustomName);
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

            return new ULawEncoder(graph, desiredInputFormat, nodeCustomName);
        }
    }
}
