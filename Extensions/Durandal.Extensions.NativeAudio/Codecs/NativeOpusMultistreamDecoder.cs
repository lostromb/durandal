using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Codecs.Opus.Enums;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Text;
using static Durandal.Extensions.NativeAudio.Codecs.NativeOpusCodecFactory;
using static System.Net.Mime.MediaTypeNames;

namespace Durandal.Extensions.NativeAudio.Codecs
{
    internal class NativeOpusMultistreamDecoder : SafeHandleZeroOrMinusOneIsInvalid, IOpusMultistreamDecoder
    {
        internal NativeOpusMultistreamDecoder() : base(ownsHandle: true)
        {
        }

        public unsafe static NativeOpusMultistreamDecoder Create(int sampleRate, int channelCount, int streams, int coupledStreams, byte[] channelMapping)
        {
            fixed (byte* mappingPtr = channelMapping)
            {
                int error;
                NativeOpusMultistreamDecoder returnVal = NativeOpus.opus_multistream_decoder_create(sampleRate, channelCount, streams, coupledStreams, mappingPtr, out error);
                if (error != OpusError.OPUS_OK)
                {
                    returnVal.Dispose();
                    throw new Exception($"Failed to create opus MS decoder: error {error}");
                }

                return returnVal;
            }
        }

        public unsafe int Decode(byte[] in_data, int in_data_offset, int len, float[] out_pcm, int out_pcm_offset, int frame_size, bool decode_fec = false)
        {
            fixed (byte* inPtr = &in_data[in_data_offset])
            fixed (float* outPtr = &out_pcm[out_pcm_offset])
            {
                return NativeOpus.opus_multistream_decode_float(this, inPtr, len, outPtr, frame_size, decode_fec ? 1 : 0);
            }
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            NativeOpus.opus_multistream_decoder_destroy(handle);
            return true;
        }
    }
}
