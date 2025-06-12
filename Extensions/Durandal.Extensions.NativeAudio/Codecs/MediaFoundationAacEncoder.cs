using Durandal.Common.Audio;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using Durandal.Common.Utils;
using SharpDX.MediaFoundation;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Durandal.Extensions.NativeAudio.Codecs
{

    /// <summary>
    /// An audio encoder backed by Windows MediaFoundation which encodes to AAC/M4A.
    /// </summary>
    public class MediaFoundationAacEncoder : MediaFoundationEncoder
    {
        /// <summary>
        /// Constructs a new <see cref="MediaFoundationAacEncoder"/>
        /// </summary>
        /// <param name="graph">The audio graph this is a part of.</param>
        /// <param name="inputFormat">The input audio format.</param>
        /// <param name="logger">A logger</param>
        /// <param name="bitrateKbps">The desired bitrate, in kilobits per second (default 192)</param>
        /// <param name="nodeCustomName">An optional custom name for this graph node, for debugging.</param>
        public MediaFoundationAacEncoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat inputFormat,
            ILogger logger,
            int bitrateKbps = 192,
            string nodeCustomName = null)
            : base(logger,
                  TranscodeContainerTypeGuids.Mpeg4,
                  graph,
                  inputFormat,
                  nameof(MediaFoundationAacEncoder),
                  nodeCustomName)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("Media foundation libraries are only supported on Windows.");
            }

            bitrateKbps.AssertPositive(nameof(bitrateKbps));
            EncodedMediaType = SelectMediaType(AudioFormatGuids.Aac, inputFormat, bitrateKbps * 1024);
            if (EncodedMediaType != null)
            {
                BitrateBitsPerSecond = EncodedMediaType.Get(MediaTypeAttributeKeys.AudioAvgBytesPerSecond) * 8;
            }
        }

        /// <inheritdoc />
        public override string Codec => "aacm4a";
    }
}
