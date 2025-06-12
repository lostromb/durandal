using Durandal.Common.Audio;
using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Extensions.NativeAudio.Codecs
{
    /// <summary>
    /// Audio codec factory backed by Windows Media Foundation libs. Can encode or decode to a wide variety of formats.
    /// </summary>
    public class MediaFoundationCodecFactory : IAudioCodecFactory
    {
        private static readonly IReadOnlySet<string> ENCODE_FORMATS = new ReadOnlySetWrapper<string>(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mp3", "aacm4a"
        });
        private static readonly IReadOnlySet<string> DECODE_FORMATS = new ReadOnlySetWrapper<string>(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mp3", "aacm4a", "flac", "wma8", "wma9"
        });

        public MediaFoundationCodecFactory()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("Media foundation libraries are only supported on Windows.");
            }

            Encoder_Mp3_BitrateKbps = 192;
            Encoder_Aac_BitrateKbps = 128;
        }

        /// <summary>
        /// Gets or sets the bitrate for the mp3 encoder (default 192)
        /// </summary>
        public int Encoder_Mp3_BitrateKbps { get; set; }

        /// <summary>
        /// Gets or sets the bitrate for the AAC encoder (default 128)
        /// </summary>
        public int Encoder_Aac_BitrateKbps { get; set; }

        /// <inheritdoc />
        public IReadOnlySet<string> SupportedEncodeFormats => ENCODE_FORMATS;

        /// <inheritdoc />
        public IReadOnlySet<string> SupportedDecodeFormats => DECODE_FORMATS;

        /// <inheritdoc />
        public bool CanDecode(string codecName)
        {
            return DECODE_FORMATS.Contains(codecName);
        }

        /// <inheritdoc />
        public bool CanEncode(string codecName)
        {
            return ENCODE_FORMATS.Contains(codecName);
        }

        /// <inheritdoc />
        public AudioDecoder CreateDecoder(string codecName, string codecParams, WeakPointer<IAudioGraph> graph, ILogger traceLogger, string nodeCustomName)
        {
            MediaFoundationDecoder returnVal = new MediaFoundationDecoder(traceLogger, graph, nodeCustomName);
            return returnVal;
        }

        /// <inheritdoc />
        public AudioEncoder CreateEncoder(string codecName, WeakPointer<IAudioGraph> graph, AudioSampleFormat desiredInputFormat, ILogger traceLogger, string nodeCustomName)
        {
            if (string.Equals(codecName, "mp3", StringComparison.OrdinalIgnoreCase))
            {
                return new MediaFoundationMp3Encoder(graph, desiredInputFormat, traceLogger, Encoder_Mp3_BitrateKbps, nodeCustomName);
            }
            else if (string.Equals(codecName, "aacm4a", StringComparison.OrdinalIgnoreCase))
            {
                return new MediaFoundationAacEncoder(graph, desiredInputFormat, traceLogger, Encoder_Aac_BitrateKbps, nodeCustomName);
            }

            throw new NotSupportedException("Don't know how to encode to codec " + codecName);
        }
    }
}
