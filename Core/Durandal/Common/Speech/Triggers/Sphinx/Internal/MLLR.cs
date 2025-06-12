using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class MLLR
    {
        internal static ps_mllr_t ps_mllr_read(Pointer<byte> regmatfile, FileAdapter fileAdapter, SphinxLogger logger)
        {
            ps_mllr_t mllr;
            FILE fp;
            int n, i, m, j, k;

            mllr = new ps_mllr_t();

            if ((fp = fileAdapter.fopen(regmatfile, "r")) == null)
            {
                logger.E_ERROR_SYSTEM(string.Format("Failed to open MLLR file '{0}' for reading", cstring.FromCString(regmatfile)));
                goto error_out;
            }
            else
                logger.E_INFO(string.Format("Reading MLLR transformation file '{0}'\n", cstring.FromCString(regmatfile)));

            if ((fp.fscanf_d(out n) != 1) || (n < 1))
            {
                logger.E_ERROR("Failed to read number of MLLR classes\n");
                goto error_out;
            }

            mllr.n_class = n;

            if ((fp.fscanf_d(out n) != 1))
            {
                logger.E_ERROR("Failed to read number of feature streams\n");
                goto error_out;
            }
            mllr.n_feat = n;
            mllr.veclen = CKDAlloc.ckd_calloc<int>(mllr.n_feat);

            mllr.A = CKDAlloc.ckd_calloc<Pointer<Pointer<Pointer<float>>>>(mllr.n_feat);
            mllr.b = CKDAlloc.ckd_calloc<Pointer<Pointer<float>>>(mllr.n_feat);
            mllr.h = CKDAlloc.ckd_calloc<Pointer<Pointer<float>>>(mllr.n_feat);

            for (i = 0; i < mllr.n_feat; ++i)
            {
                if (fp.fscanf_d(out n) != 1)
                {
                    logger.E_ERROR(string.Format("Failed to read stream length for feature {0}\n", i));
                    goto error_out;
                }
                mllr.veclen[i] = n;
                mllr.A[i] = CKDAlloc.ckd_calloc_3d<float>((uint)mllr.n_class, (uint)mllr.veclen[i], (uint)mllr.veclen[i]);
                mllr.b[i] = CKDAlloc.ckd_calloc_2d<float>((uint)mllr.n_class, (uint)mllr.veclen[i]);
                mllr.h[i] = CKDAlloc.ckd_calloc_2d<float>((uint)mllr.n_class, (uint)mllr.veclen[i]);

                float t;
                for (m = 0; m < mllr.n_class; ++m)
                {
                    for (j = 0; j < mllr.veclen[i]; ++j)
                    {
                        for (k = 0; k < mllr.veclen[i]; ++k)
                        {
                            if (fp.fscanf_f_(out t) != 1)
                            {
                                logger.E_ERROR(string.Format("Failed reading MLLR rotation ({0},{1},{2},{3})\n",
                                        i, m, j, k));
                                goto error_out;
                            }
                            mllr.A[i][m][j].Set(k, t);
                        }
                    }
                    for (j = 0; j < mllr.veclen[i]; ++j)
                    {
                        if (fp.fscanf_f_(out t) != 1)
                        {
                            logger.E_ERROR(string.Format("Failed reading MLLR bias ({0},{1},{2})\n",
                                    i, m, j));
                            goto error_out;
                        }
                        mllr.b[i][m].Set(j, t);
                    }
                    for (j = 0; j < mllr.veclen[i]; ++j)
                    {
                        if (fp.fscanf_f_(out t) != 1)
                        {
                            logger.E_ERROR(string.Format("Failed reading MLLR variance scale ({0},{1},{2})\n",
                                    i, m, j));
                            goto error_out;
                        }
                        mllr.h[i][m].Set(j, t);
                    }
                }
            }
            fp.fclose();
            return mllr;

            error_out:
            if (fp != null)
                fp.fclose();

            return null;
        }
    }
}
