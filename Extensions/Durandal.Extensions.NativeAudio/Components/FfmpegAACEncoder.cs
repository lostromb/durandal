using Durandal.Common.Audio;
using Durandal.Common.Collections;
using Durandal.Common.Logger;
using Durandal.Common.ServiceMgmt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Extensions.NativeAudio.Components
{
    public class FfmpegAACEncoder : FfmpegAudioEncoder
    {
        private readonly string _encoderParams;
        private readonly IReadOnlyDictionary<string, string> _metadata;

        private FfmpegAACEncoder(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat inputFormat,
            ILogger logger,
            int bitrateKbps,
            string nodeCustomName,
            IDictionary<string, string> trackMetadata = null,
            string additionalCommandLine = null,
            string ffmpegPath = null) : base(graph, inputFormat, logger, ffmpegPath, nameof(FfmpegAACEncoder), nodeCustomName)
        {
            if (bitrateKbps < 1)
            {
                throw new ArgumentOutOfRangeException("Bitrate must be a positive integer");
            }

            if (string.IsNullOrEmpty(additionalCommandLine))
            {
                _encoderParams = string.Format("-c:a aac -b:a {0}K", bitrateKbps);
            }
            else
            {
                _encoderParams = string.Format("-c:a aac -b:a {0}K {1}", bitrateKbps, additionalCommandLine);
            }

            if (trackMetadata == null)
            {
                _metadata = new SmallDictionary<string, string>(0);
            }
            else
            {
                _metadata = new SmallDictionary<string, string>(trackMetadata, trackMetadata.Count);
            }
        }

        public static async Task<FfmpegAACEncoder> Create(
            WeakPointer<IAudioGraph> graph,
            AudioSampleFormat inputFormat,
            ILogger logger,
            FileInfo outputFile,
            int bitrateKbps,
            string nodeCustomName,
            IDictionary<string, string> trackMetadata = null,
            string additionalCommandLine = null,
            bool overwriteExistingFile = true,
            string ffmpegPath = null)
        {
            FfmpegAACEncoder returnVal = new FfmpegAACEncoder(graph, inputFormat, logger, bitrateKbps, nodeCustomName, trackMetadata, additionalCommandLine, ffmpegPath);
            await returnVal.StartEncoderProcess(outputFile, overwriteExistingFile).ConfigureAwait(false);
            return returnVal;
        }

        protected override string OutputEncoderParameters => _encoderParams;

        public override IReadOnlyDictionary<string, string> AudioMetadata => _metadata;
    }
}
