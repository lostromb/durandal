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
    public sealed class RawPcmCodecFactory : IAudioCodecFactory
    {
        /// <summary>
        /// Codec name for PCM, signed int16, little endian.
        /// This should be "pcm_s16le" for consistency but it has to stay pcm for legacy reasons
        /// </summary>
        public static readonly string CODEC_NAME_PCM_S16LE = "pcm";

        /// <summary>
        /// Codec name for PCM, signed int24, little endian.
        /// </summary>
        public static readonly string CODEC_NAME_PCM_S24LE = "pcm_s24le";

        /// <summary>
        /// Codec name for PCM, signed int32, little endian.
        /// </summary>
        public static readonly string CODEC_NAME_PCM_S32LE = "pcm_s32le";

        /// <summary>
        /// Codec name for PCM, float32, little endian.
        /// </summary>
        public static readonly string CODEC_NAME_PCM_F32LE = "pcm_f32le";

        private static readonly Durandal.Common.Collections.IReadOnlySet<string> SUPPORTED_CODECS = new ReadOnlySetWrapper<string>(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            CODEC_NAME_PCM_S16LE,
            CODEC_NAME_PCM_S24LE,
            CODEC_NAME_PCM_S32LE,
            CODEC_NAME_PCM_F32LE,
        });

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

            return new RawPcmDecoder(graph, codecParams, nodeCustomName, codecName);
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

            RiffWaveFormat sampleFormat = RiffWaveFormat.PCM_S16LE;
            if (string.Equals(codecName, CODEC_NAME_PCM_S16LE, StringComparison.OrdinalIgnoreCase))
            {
                sampleFormat = RiffWaveFormat.PCM_S16LE;
            }
            else if (string.Equals(codecName, CODEC_NAME_PCM_S24LE, StringComparison.OrdinalIgnoreCase))
            {
                sampleFormat = RiffWaveFormat.PCM_S24LE;
            }
            else if (string.Equals(codecName, CODEC_NAME_PCM_S32LE, StringComparison.OrdinalIgnoreCase))
            {
                sampleFormat = RiffWaveFormat.PCM_S32LE;
            }
            else if (string.Equals(codecName, CODEC_NAME_PCM_F32LE, StringComparison.OrdinalIgnoreCase))
            {
                sampleFormat = RiffWaveFormat.PCM_F32LE;
            }

            return new RawPcmEncoder(graph, desiredInputFormat, nodeCustomName, sampleFormat);
        }
    }
}
