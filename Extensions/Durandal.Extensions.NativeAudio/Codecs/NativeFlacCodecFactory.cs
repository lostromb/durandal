using Durandal.Common.Audio;
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
using System.Threading;
using System.Threading.Tasks;

namespace Durandal.Extensions.NativeAudio.Codecs
{
    /// <summary>
    /// Audio codec factory which handles self-contained flac (not flac in ogg) using p/invoke to libflac library.
    /// Encoded outputs will ignore metadata and will assume forward-only (streaming) access, so they won't
    /// produce neatly indexed and tagged files for general playback. This is intended for realtime speech transcription
    /// and similar scenarios.
    /// </summary>
    public class NativeFlacCodecFactory : IAudioCodecFactory
    {
        /// <summary>
        /// The codec code for self-contained flac
        /// </summary>
        public static readonly string CODEC_NAME = "flac";

        private static readonly IReadOnlySet<string> SUPPORTED_CODECS = new ReadOnlySetWrapper<string>(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CODEC_NAME });

        private readonly INativeFlacCodecProvider _flacLibrary;
        private readonly int _complexity;
        private readonly ILogger _logger;

        public IReadOnlySet<string> SupportedEncodeFormats => SUPPORTED_CODECS;

        public IReadOnlySet<string> SupportedDecodeFormats => SUPPORTED_CODECS;

        /// <summary>
        /// Creates a native flac code factory.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="complexity">The complexity to use for encoding, between 0 and 8</param>
        public NativeFlacCodecFactory(ILogger logger, int complexity = 0)
        {
            if (complexity < 0 || complexity > 8)
            {
                throw new ArgumentOutOfRangeException("Flac complexity ranges from 0 to 8");
            }

            _complexity = complexity;
            _logger = logger.AssertNonNull(nameof(logger));
            _flacLibrary = CreateFlacAdapterForCurrentPlatform(_logger);
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

            return new NativeFlacDecoder(
                _flacLibrary,
                graph,
                codecParams,
                nodeCustomName,
                traceLogger);
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

            return new NativeFlacEncoder(
                _flacLibrary,
                graph,
                desiredInputFormat,
                nodeCustomName,
                traceLogger,
                _complexity);
        }

        internal static INativeFlacCodecProvider CreateFlacAdapterForCurrentPlatform(ILogger logger)
        {
            OSAndArchitecture currentPlatform = NativePlatformUtils.GetCurrentPlatform(logger);

            // The default linux library name is cased as "libFLAC.so" so we have to use upper-case import to make sure it matches
            if (NativePlatformUtils.PrepareNativeLibrary("FLAC", logger) == NativeLibraryStatus.Available)
            {
                return new NativeFlacCodecProvider();
            }
            else
            {
                throw new PlatformNotSupportedException("Native Flac not implemented for this platform");
            }
        }
    }
}
