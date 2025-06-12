using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class FrontendPrespeechBuf
    {
        internal static prespch_buf_t fe_prespch_init(int num_frames, int num_cepstra, int num_samples)
        {
            prespch_buf_t prespch_buf;

            prespch_buf = new prespch_buf_t();

            prespch_buf.num_cepstra = checked((short)num_cepstra);
            prespch_buf.num_frames_cep = checked((short)num_frames);
            prespch_buf.num_samples = checked((short)num_samples);
            prespch_buf.num_frames_pcm = 0;

            prespch_buf.cep_write_ptr = 0;
            prespch_buf.cep_read_ptr = 0;
            prespch_buf.ncep = 0;

            prespch_buf.pcm_write_ptr = 0;
            prespch_buf.pcm_read_ptr = 0;
            prespch_buf.npcm = 0;

            prespch_buf.cep_buf = CKDAlloc.ckd_calloc_2d<float>((uint)num_frames, (uint)num_cepstra);

            prespch_buf.pcm_buf = CKDAlloc.ckd_calloc<short>(prespch_buf.num_frames_pcm * prespch_buf.num_samples);

            return prespch_buf;
        }


        internal static int fe_prespch_read_cep(prespch_buf_t prespch_buf, Pointer<float> feat)
        {
            if (prespch_buf.ncep == 0)
                return 0;
            prespch_buf.cep_buf[prespch_buf.cep_read_ptr].MemCopyTo(feat, prespch_buf.num_cepstra);
            prespch_buf.cep_read_ptr = checked((short)((prespch_buf.cep_read_ptr + 1) % prespch_buf.num_frames_cep));
            prespch_buf.ncep--;
            return 1;
        }

        internal static void fe_prespch_write_cep(prespch_buf_t prespch_buf, Pointer<float> feat)
        {
            feat.MemCopyTo(prespch_buf.cep_buf[prespch_buf.cep_write_ptr], prespch_buf.num_cepstra);
            prespch_buf.cep_write_ptr = checked((short)((prespch_buf.cep_write_ptr + 1) % prespch_buf.num_frames_cep));
            if (prespch_buf.ncep < prespch_buf.num_frames_cep)
            {
                prespch_buf.ncep++;
            }
            else
            {
                prespch_buf.cep_read_ptr = checked((short)((prespch_buf.cep_read_ptr + 1) % prespch_buf.num_frames_cep));
            }
        }

        internal static void fe_prespch_read_pcm(prespch_buf_t prespch_buf, Pointer<short> samples,
                            Pointer<int> samples_num)
        {
            int i;
            Pointer<short> cursample = samples;
            samples_num.Deref = prespch_buf.npcm * prespch_buf.num_samples;
            for (i = 0; i < prespch_buf.npcm; i++)
            {
                prespch_buf.pcm_buf.Point(prespch_buf.pcm_read_ptr * prespch_buf.num_samples).MemCopyTo(cursample, prespch_buf.num_samples);
                prespch_buf.pcm_read_ptr = checked((short)((prespch_buf.pcm_read_ptr + 1) % prespch_buf.num_frames_pcm));
            }

            prespch_buf.pcm_read_ptr = 0;
            prespch_buf.pcm_write_ptr = 0;
            prespch_buf.npcm = 0;
            return;
        }

        internal static void fe_prespch_write_pcm(prespch_buf_t prespch_buf, short[] samples)
        {
            int sample_ptr;

            sample_ptr = prespch_buf.pcm_write_ptr * prespch_buf.num_samples;
            samples.GetPointer().MemCopyTo(prespch_buf.pcm_buf.Point(sample_ptr), prespch_buf.num_samples);

            prespch_buf.pcm_write_ptr = checked((short)((prespch_buf.pcm_write_ptr + 1) % prespch_buf.num_frames_pcm));
            if (prespch_buf.npcm < prespch_buf.num_frames_pcm)
            {
                prespch_buf.npcm++;
            }
            else
            {
                prespch_buf.pcm_read_ptr = checked((short)((prespch_buf.pcm_read_ptr + 1) % prespch_buf.num_frames_pcm));
            }
        }

        internal static void fe_prespch_reset_cep(prespch_buf_t prespch_buf)
        {
            prespch_buf.cep_read_ptr = 0;
            prespch_buf.cep_write_ptr = 0;
            prespch_buf.ncep = 0;
        }

        internal static void fe_prespch_reset_pcm(prespch_buf_t prespch_buf)
        {
            prespch_buf.pcm_read_ptr = 0;
            prespch_buf.pcm_write_ptr = 0;
            prespch_buf.npcm = 0;
        }

        internal static int fe_prespch_ncep(prespch_buf_t prespch_buf)
        {
            return prespch_buf.ncep;
        }
    }
}
