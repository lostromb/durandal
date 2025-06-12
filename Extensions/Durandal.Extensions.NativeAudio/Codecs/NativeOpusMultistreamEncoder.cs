using Durandal.Common.Audio.Codecs.Opus;
using Durandal.Common.Audio.Codecs.Opus.Enums;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.ConstrainedExecution;
using System.Text;

namespace Durandal.Extensions.NativeAudio.Codecs
{
    internal class NativeOpusMultistreamEncoder : SafeHandleZeroOrMinusOneIsInvalid, IOpusMultistreamEncoder
    {
        internal NativeOpusMultistreamEncoder() : base(ownsHandle: true)
        {
        }

        public unsafe static NativeOpusMultistreamEncoder Create(int sampleRate, int channelCount, byte[] channelMapping, out int streams, out int coupledStreams, OpusApplication application)
        {
            fixed (byte* mappingTable = channelMapping)
            {
                int error;
                NativeOpusMultistreamEncoder returnVal = NativeOpus.opus_multistream_surround_encoder_create(sampleRate, channelCount, 1, out streams, out coupledStreams, mappingTable, (int)application, out error);
                if (error != OpusError.OPUS_OK)
                {
                    returnVal.Dispose();
                    throw new Exception($"Failed to create opus MS encoder: error {error}");
                }

                return returnVal;
            }
        }


        public unsafe int Encode(float[] in_pcm, int pcm_offset, int frame_size, byte[] out_data, int out_data_offset, int max_data_bytes)
        {
            fixed (float* inPtr = &in_pcm[pcm_offset])
            fixed (byte* outPtr = &out_data[out_data_offset])
            {
                return NativeOpus.opus_multistream_encode_float(this, inPtr, frame_size, outPtr, max_data_bytes);
            }
        }

        public int Complexity
        {
            get
            {
                int returnVal;
                NativeOpus.opus_multistream_encoder_ctl(this, NativeOpus.OPUS_GET_COMPLEXITY_REQUEST, out returnVal);
                return returnVal;
            }
            set
            {
                NativeOpus.opus_multistream_encoder_ctl(this, NativeOpus.OPUS_SET_COMPLEXITY_REQUEST, value);
            }
        }

        public bool UseDTX
        {
            get
            {
                int returnVal;
                NativeOpus.opus_multistream_encoder_ctl(this, NativeOpus.OPUS_GET_DTX_REQUEST, out returnVal);
                return returnVal != 0;
            }
            set
            {
                NativeOpus.opus_multistream_encoder_ctl(this, NativeOpus.OPUS_SET_DTX_REQUEST, value ? 1 : 0);
            }
        }

        public int Bitrate
        {
            get
            {
                int returnVal;
                NativeOpus.opus_multistream_encoder_ctl(this, NativeOpus.OPUS_GET_BITRATE_REQUEST, out returnVal);
                return returnVal;
            }
            set
            {
                NativeOpus.opus_multistream_encoder_ctl(this, NativeOpus.OPUS_SET_COMPLEXITY_REQUEST, value);
            }
        }

        public OpusMode ForceMode
        {
            get
            {
                int returnVal;
                NativeOpus.opus_multistream_encoder_ctl(this, NativeOpus.OPUS_GET_FORCE_MODE_REQUEST, out returnVal);
                return (OpusMode)returnVal;
            }
            set
            {
                NativeOpus.opus_multistream_encoder_ctl(this, NativeOpus.OPUS_SET_FORCE_MODE_REQUEST, (int)value);
            }
        }

        public bool UseVBR
        {
            get
            {
                int returnVal;
                NativeOpus.opus_multistream_encoder_ctl(this, NativeOpus.OPUS_GET_VBR_REQUEST, out returnVal);
                return returnVal != 0;
            }
            set
            {
                NativeOpus.opus_multistream_encoder_ctl(this, NativeOpus.OPUS_SET_VBR_REQUEST, value ? 1 : 0);
            }
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        protected override bool ReleaseHandle()
        {
            NativeOpus.opus_multistream_encoder_destroy(handle);
            return true;
        }
    }
}
