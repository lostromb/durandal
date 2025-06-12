using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class TransitionMatrix
    {
        internal static readonly Pointer<byte> TMAT_PARAM_VERSION = cstring.ToCString("1.0");

        internal static int tmat_chk_uppertri(tmat_t tmat, logmath_t lmath, SphinxLogger logger)
        {
            int i, src, dst;

            /* Check that each tmat is upper-triangular */
            for (i = 0; i < tmat.n_tmat; i++)
            {
                for (dst = 0; dst < tmat.n_state; dst++)
                    for (src = dst + 1; src < tmat.n_state; src++)
                        if (tmat.tp[i][src][dst] < 255)
                        {
                            logger.E_ERROR(string.Format("tmat[{0}][{1}][{2}] = {3}\n",
                                    i, src, dst, tmat.tp[i][src][dst]));
                            return -1;
                        }
            }

            return 0;
        }

        internal static int tmat_chk_1skip(tmat_t tmat, logmath_t lmath, SphinxLogger logger)
        {
            int i, src, dst;

            for (i = 0; i < tmat.n_tmat; i++)
            {
                for (src = 0; src < tmat.n_state; src++)
                    for (dst = src + 3; dst <= tmat.n_state; dst++)
                        if (tmat.tp[i][src][dst] < 255)
                        {
                            logger.E_ERROR(string.Format("tmat[{0}][{1}][{2}] = {3}\n",
                                    i, src, dst, tmat.tp[i][src][dst]));
                            return -1;
                        }
            }

            return 0;
        }

        internal static tmat_t tmat_init(Pointer<byte> file_name, FileAdapter fileAdapter, logmath_t lmath, double tpfloor, int breport, SphinxLogger logger)
        {
            int n_src, n_dst, n_tmat;
            BinaryReader fp = null;
            int byteswap, chksum_present;
            BoxedValueUInt chksum = new BoxedValueUInt();
            Pointer<Pointer<float>> tp;
            int i, j, k, tp_per_tmat;
            Pointer<Pointer<byte>> argname;
            Pointer<Pointer<byte>> argval;
            tmat_t t;

            if (breport != 0)
            {
                logger.E_INFO(string.Format("Reading HMM transition probability matrices: {0}",
                       cstring.FromCString(file_name)));
            }

            t = new tmat_t();
            
            if ((fp = fileAdapter.fopen_new(file_name, "rb")) == null)
                logger.E_FATAL_SYSTEM(string.Format("Failed to open transition file '{0}' for reading", cstring.FromCString(file_name)));

            try
            {
                /* Read header, including argument-value info and 32-bit byteorder magic */
                BoxedValue<Pointer<Pointer<byte>>> boxed_argname = new BoxedValue<Pointer<Pointer<byte>>>();
                BoxedValue<Pointer<Pointer<byte>>> boxed_argval = new BoxedValue<Pointer<Pointer<byte>>>();
                if (BinaryIO.bio_readhdr(fp, boxed_argname, boxed_argval, out byteswap, logger) < 0)
                    logger.E_FATAL(string.Format("Failed to read header from file '{0}'", cstring.FromCString(file_name)));
                argname = boxed_argname.Val;
                argval = boxed_argval.Val;

                /* Parse argument-value list */
                chksum_present = 0;
                for (i = 0; argname[i].IsNonNull; i++)
                {
                    if (cstring.strcmp(argname[i], cstring.ToCString("version")) == 0)
                    {
                        if (cstring.strcmp(argval[i], TMAT_PARAM_VERSION) != 0)
                            logger.E_WARN(string.Format("Version mismatch({0}): {1}, expecting {2}",
                                   cstring.FromCString(file_name), cstring.FromCString(argval[i]), cstring.FromCString(TMAT_PARAM_VERSION)));
                    }
                    else if (cstring.strcmp(argname[i], cstring.ToCString("chksum0")) == 0)
                    {
                        chksum_present = 1; /* Ignore the associated value */
                    }
                }

                argname = argval = PointerHelpers.NULL<Pointer<byte>>();

                chksum.Val = 0;

                /* Read #tmat, #from-states, #to-states, arraysize */
                if (!BinaryIO.bio_fread_int32(out n_tmat, fp, byteswap, chksum, logger) ||
                    !BinaryIO.bio_fread_int32(out n_src, fp, byteswap, chksum, logger) ||
                    !BinaryIO.bio_fread_int32(out n_dst, fp, byteswap, chksum, logger) ||
                    !BinaryIO.bio_fread_int32(out i, fp, byteswap, chksum, logger))
                {
                    logger.E_FATAL(string.Format("Failed to read header from '{0}'", cstring.FromCString(file_name)));
                    return null;
                }

                if (n_tmat >= short.MaxValue)
                    logger.E_FATAL(string.Format("{0}: Number of transition matrices ({1}) exceeds limit ({2})", cstring.FromCString(file_name),
                    n_tmat, short.MaxValue));
                t.n_tmat = checked((short)n_tmat);

                if (n_dst != n_src + 1)
                    logger.E_FATAL(string.Format("{0}: Unsupported transition matrix. Number of source states ({1}) != number of target states ({2})-1", cstring.FromCString(file_name), n_src, n_dst));
                t.n_state = checked((short)n_src);

                if (i != t.n_tmat * n_src * n_dst)
                {
                    logger.E_FATAL(string.Format("{0}: Invalid transitions. Number of coefficients ({1}) doesn't match expected array dimension: {2} x {3} x {4}", cstring.FromCString(file_name), i, t.n_tmat, n_src, n_dst));
                }

                /* Allocate memory for tmat data */
                t.tp = CKDAlloc.ckd_calloc_3d<byte>((uint)t.n_tmat, (uint)n_src, (uint)n_dst);

                /* Temporary structure to read in the float data */
                tp = CKDAlloc.ckd_calloc_2d<float>((uint)n_src, (uint)n_dst);

                /* Read transition matrices, normalize and floor them, and convert to log domain */
                tp_per_tmat = n_src * n_dst;
                for (i = 0; i < t.n_tmat; i++)
                {
                    if (BinaryIO.bio_fread_float(tp[0], tp_per_tmat, fp,
                                  byteswap, chksum, logger) != tp_per_tmat)
                    {
                        logger.E_FATAL(string.Format("Failed to read transition matrix {0} from '{1}'", i, cstring.FromCString(file_name)));
                    }

                    /* Normalize and floor */
                    for (j = 0; j < n_src; j++)
                    {
                        if (Vector.vector_sum_norm(tp[j], n_dst) == 0.0)
                            logger.E_WARN(string.Format("Normalization failed for transition matrix {0} from state {1}",
                                   i, j));
                        Vector.vector_nz_floor(tp[j], n_dst, tpfloor);
                        Vector.vector_sum_norm(tp[j], n_dst);

                        /* Convert to logs3. */
                        for (k = 0; k < n_dst; k++)
                        {
                            int ltp;
                            /* Log and quantize them. */
                            ltp = -LogMath.logmath_log(lmath, tp[j][k]) >> HiddenMarkovModel.SENSCR_SHIFT;
                            if (ltp > 255) ltp = 255;
                            t.tp[i][j].Set(k, (byte)ltp);
                        }
                    }
                }

                if (chksum_present != 0)
                    BinaryIO.bio_verify_chksum(fp, byteswap, chksum.Val, logger);

                if (fp.PeekChar() != -1)
                    logger.E_WARN("Non-empty file beyond end of data in " + cstring.FromCString(file_name));

                fp.Dispose();

                if (tmat_chk_uppertri(t, lmath, logger) < 0)
                {
                    logger.E_FATAL("Tmat not upper triangular");
                    return null;
                }
                if (tmat_chk_1skip(t, lmath, logger) < 0)
                {
                    logger.E_FATAL("Topology not Left-to-Right or Bakis");
                    return null;
                }

                return t;
            }
            finally
            {
                fp?.Dispose();
            }
        }
    }
}
