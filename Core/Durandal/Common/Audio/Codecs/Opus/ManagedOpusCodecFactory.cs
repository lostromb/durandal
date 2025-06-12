using Durandal.Common.Audio.Codecs.Opus.Enums;
using Durandal.Common.Audio.Codecs.Opus.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs.Opus
{
    /// <summary>
    /// Implementation of <see cref="IOpusCodecFactory"/> using managed Concentus code, used as a fallback when P/Invoke is not available.
    /// </summary>
    internal class ManagedOpusCodecFactory : IOpusCodecFactory
    {
        /// <inheritdoc />
        public IOpusDecoder CreateDecoder(int sampleRate, int channelCount)
        {
            return new OpusDecoder(sampleRate, channelCount);
        }

        /// <inheritdoc />
        public IOpusMultistreamDecoder CreateMultistreamDecoder(int sampleRate, int channelCount, int streams, int coupledStreams, byte[] channelMapping)
        {
            return new OpusMSDecoder(sampleRate, channelCount, streams, coupledStreams, channelMapping);
        }

        /// <inheritdoc />
        public IOpusEncoder CreateEncoder(int sampleRate, int channelCount, OpusApplication application)
        {
            return new OpusEncoder(sampleRate, channelCount, application);
        }

        public IOpusMultistreamEncoder CreateMultistreamEncoder(int sampleRate, int channelCount, byte[] channelMapping, out int streams, out int coupledStreams, OpusApplication application)
        {
            return OpusMSEncoder.CreateSurround(sampleRate, channelCount, 1, out streams, out coupledStreams, channelMapping, application);
        }

        /// <inheritdoc />
        public string GetVersionString()
        {
            return Durandal.Common.Audio.Codecs.Opus.CodecHelpers.GetVersionString();
        }
    }
}
