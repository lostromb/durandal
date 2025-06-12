using Durandal.Common.Audio.Codecs.Opus.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Codecs.Opus
{
    public interface IOpusEncoder : IDisposable
    {
        int Encode(float[] in_pcm, int pcm_offset, int frame_size, byte[] out_data, int out_data_offset, int max_data_bytes);

        int Complexity { get; set; }

        bool UseDTX { get; set; }

        int Bitrate { get; set; }

        OpusMode ForceMode { get; set; }

        bool UseVBR { get; set; }
    }
}
