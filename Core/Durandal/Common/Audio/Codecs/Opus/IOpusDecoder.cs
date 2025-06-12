using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Audio.Codecs.Opus
{
    public interface IOpusDecoder : IDisposable
    {
        int Decode(byte[] in_data, int in_data_offset, int len, float[] out_pcm, int out_pcm_offset, int frame_size, bool decode_fec = false);
    }
}
