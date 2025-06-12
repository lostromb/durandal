using Durandal.Common.Audio.Codecs.Opus.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio.Codecs.Opus
{
    /// <summary>
    /// Factory class for creating Opus encoders or decoders.
    /// </summary>
    public interface IOpusCodecFactory
    {
        IOpusDecoder CreateDecoder(int sampleRate, int channelCount);

        IOpusMultistreamDecoder CreateMultistreamDecoder(int sampleRate, int channelCount, int streams, int coupledStreams, byte[] channelMapping);

        IOpusEncoder CreateEncoder(int sampleRate, int channelCount, OpusApplication application);

        IOpusMultistreamEncoder CreateMultistreamEncoder(int sampleRate, int channelCount, byte[] channelMapping, out int streams, out int coupledStreams, OpusApplication application);

        string GetVersionString();
    }
}
