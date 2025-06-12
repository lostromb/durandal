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
    /// An audio encoder backed by Windows MediaFoundation which encodes to mp3.
    /// </summary>
    public class MediaFoundationMp3Encoder : MediaFoundationEncoder
    {
        /// <summary>
        /// Constructs a new <see cref="MediaFoundationMp3Encoder"/>
        /// </summary>
        /// <param name="graph">The audio graph this is a part of.</param>
        /// <param name="inputFormat">The input audio format.</param>
        /// <param name="logger">A logger</param>
        /// <param name="bitrateKbps">The desired bitrate, in kilobits per second (default 192)</param>
        /// <param name="nodeCustomName">An optional custom name for this graph node, for debugging.</param>
        public MediaFoundationMp3Encoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat inputFormat,
            ILogger logger,
            int bitrateKbps = 192,
            string nodeCustomName = null)
            : base(logger,
                  TranscodeContainerTypeGuids.Mp3,
                  graph,
                  inputFormat,
                  nameof(MediaFoundationMp3Encoder),
                  nodeCustomName)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("Media foundation libraries are only supported on Windows.");
            }

            bitrateKbps.AssertPositive(nameof(bitrateKbps));
            EncodedMediaType = SelectMediaType(AudioFormatGuids.Mp3, inputFormat, bitrateKbps * 1024);
            if (EncodedMediaType != null)
            {
                BitrateBitsPerSecond = EncodedMediaType.Get(MediaTypeAttributeKeys.AudioAvgBytesPerSecond) * 8;
            }
        }

        /// <inheritdoc />
        public override string Codec => "mp3";
    }
}
