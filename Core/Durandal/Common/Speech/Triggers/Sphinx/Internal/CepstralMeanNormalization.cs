using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class CepstralMeanNormalization
    {
        internal static readonly Pointer<Pointer<byte>> cmn_type_str =
            new Pointer<Pointer<byte>>(new Pointer<byte>[]
            {
                cstring.ToCString("none"),
                cstring.ToCString("batch"),
                cstring.ToCString("live")
            });

        internal static readonly Pointer<Pointer<byte>> cmn_alt_type_str =
            new Pointer<Pointer<byte>>(new Pointer<byte>[]
            {
                cstring.ToCString("none"),
                cstring.ToCString("current"),
                cstring.ToCString("prior")
            });

        public const int n_cmn_type_str = 3;

        public const int CMN_WIN_HWM = 800;
        public const int CMN_WIN = 500;

        internal static int cmn_type_from_str(Pointer<byte> str, SphinxLogger logger)
        {
            int i;

            for (i = 0; i<n_cmn_type_str; ++i) {
                if (0 == cstring.strcmp(str, cmn_type_str[i]) || 0 == cstring.strcmp(str, cmn_alt_type_str[i]))
                    return i;
            }
            logger.E_FATAL(string.Format("Unknown CMN type '{0}'\n", cstring.FromCString(str)));
            return cmn_type_e.CMN_NONE;
        }

        internal static cmn_t cmn_init(int veclen, SphinxLogger logger)
        {
            cmn_t cmn = new cmn_t();
            cmn.logger = logger;
            cmn.veclen = veclen;
            cmn.cmn_mean = CKDAlloc.ckd_calloc<float>(veclen);
            cmn.cmn_var = CKDAlloc.ckd_calloc<float>(veclen);
            cmn.sum = CKDAlloc.ckd_calloc<float>(veclen);
            cmn.nframe = 0;

            return cmn;
        }
        
        internal static void cmn_run(cmn_t cmn, Pointer<Pointer<float>> mfc, int varnorm, int n_frame)
        {
            Pointer<float> mfcp;
            float t;
            int i, f;
            int n_pos_frame;

            SphinxAssert.assert(mfc.IsNonNull);

            if (n_frame <= 0)
                return;

            /* If cmn.cmn_mean wasn't NULL, we need to zero the contents */
            cmn.cmn_mean.MemSet(0, cmn.veclen);

            /* Find mean cep vector for this utterance */
            for (f = 0, n_pos_frame = 0; f < n_frame; f++)
            {
                mfcp = mfc[f];

                /* Skip zero energy frames */
                if (mfcp[0] < 0)
                    continue;

                for (i = 0; i < cmn.veclen; i++)
                {
                    cmn.cmn_mean[i] += mfcp[i];
                }

                n_pos_frame++;
            }

            for (i = 0; i < cmn.veclen; i++)
                cmn.cmn_mean[i] /= n_pos_frame;

            // FIXME reformat this log line
            cmn.logger.E_INFO("CMN: ");
            for (i = 0; i < cmn.veclen; i++)
                cmn.logger.E_INFOCONT(string.Format("{0} ", (cmn.cmn_mean[i])));
            cmn.logger.E_INFOCONT("\n");
            if (varnorm == 0)
            {
                /* Subtract mean from each cep vector */
                for (f = 0; f < n_frame; f++)
                {
                    mfcp = mfc[f];
                    for (i = 0; i < cmn.veclen; i++)
                        mfcp[i] -= cmn.cmn_mean[i];
                }
            }
            else
            {
                /* Scale cep vectors to have unit variance along each dimension, and subtract means */
                /* If cmn.cmn_var wasn't NULL, we need to zero the contents */
                cmn.cmn_var.MemSet(0, cmn.veclen);

                for (f = 0; f < n_frame; f++)
                {
                    mfcp = mfc[f];

                    for (i = 0; i < cmn.veclen; i++)
                    {
                        t = mfcp[i] - cmn.cmn_mean[i];
                        cmn.cmn_var[i] += (t * t);
                    }
                }
                for (i = 0; i < cmn.veclen; i++)
                    /* Inverse Std. Dev, RAH added type case from sqrt */
                    cmn.cmn_var[i] = (float)(Math.Sqrt((double)n_frame / (cmn.cmn_var[i])));

                for (f = 0; f < n_frame; f++)
                {
                    mfcp = mfc[f];
                    for (i = 0; i < cmn.veclen; i++)
                        mfcp[i] = ((mfcp[i] - cmn.cmn_mean[i]) * cmn.cmn_var[i]);
                }
            }
        }
    }
}
