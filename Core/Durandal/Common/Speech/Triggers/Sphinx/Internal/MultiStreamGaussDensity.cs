using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class MultiStreamGaussDensity
    {
        internal static readonly Pointer<byte> GAUDEN_PARAM_VERSION = cstring.ToCString("1.0");
        public const int WORST_DIST = unchecked((int)0x80000000);

        internal static Pointer<Pointer<Pointer<Pointer<float>>>> gauden_param_read(
            Pointer<byte> file_name,
            FileAdapter fileAdapter,
            BoxedValueInt out_n_mgau,
            BoxedValueInt out_n_feat,
            BoxedValueInt out_n_density,
            BoxedValue<Pointer<int>> out_veclen,
            SphinxLogger logger)
        {
            FILE fp;
            int i, j, k, l, n, blk;
            int n_mgau;
            int n_feat;
            int n_density;
            Pointer<int> veclen;
            int byteswap, chksum_present;
            Pointer<Pointer<Pointer<Pointer<float>>>> output;
            Pointer<float> buf;
            Pointer<Pointer<byte>> argname;
            Pointer<Pointer<byte>> argval;
            BoxedValueUInt chksum = new BoxedValueUInt();

            logger.E_INFO(string.Format("Reading mixture gaussian parameter: {0}\n", cstring.FromCString(file_name)));

            if ((fp = fileAdapter.fopen(file_name, "rb")) == null)
            {
                logger.E_ERROR_SYSTEM(string.Format("Failed to open file '{0}' for reading", cstring.FromCString(file_name)));
                return PointerHelpers.NULL<Pointer<Pointer<Pointer<float>>>>();
            }

            /* Read header, including argument-value info and 32-bit byteorder magic */
            BoxedValue<Pointer<Pointer<byte>>> boxed_argname = new BoxedValue<Pointer<Pointer<byte>>>();
            BoxedValue<Pointer<Pointer<byte>>> boxed_argval = new BoxedValue<Pointer<Pointer<byte>>>();
            if (BinaryIO.bio_readhdr(fp, boxed_argname, boxed_argval, out byteswap, logger) < 0)
            {
                logger.E_ERROR(string.Format("Failed to read header from file '{0}'\n", cstring.FromCString(file_name)));
                fp.fclose();
                return PointerHelpers.NULL<Pointer<Pointer<Pointer<float>>>>();
            }
            argname = boxed_argname.Val;
            argval = boxed_argval.Val;

            /* Parse argument-value list */
            chksum_present = 0;
            for (i = 0; argname[i].IsNonNull; i++)
            {
                if (cstring.strcmp(argname[i], cstring.ToCString("version")) == 0)
                {
                    if (cstring.strcmp(argval[i], GAUDEN_PARAM_VERSION) != 0)
                        logger.E_WARN(string.Format("Version mismatch({0}): {1}, expecting {2}\n",
                       cstring.FromCString(file_name), cstring.FromCString(argval[i]), cstring.FromCString(GAUDEN_PARAM_VERSION)));
                }
                else if (cstring.strcmp(argname[i], cstring.ToCString("chksum0")) == 0)
                {
                    chksum_present = 1; /* Ignore the associated value */
                }
            }
            argname = argval = PointerHelpers.NULL<Pointer<byte>>();

            chksum.Val = 0;
            byte[] tmp_buf = new byte[4];
            Pointer<byte> tmp = new Pointer<byte>(tmp_buf);

            /* #Codebooks */
            if (BinaryIO.bio_fread(tmp, 4, 1, fp, byteswap, chksum, logger) != 1)
            {
                logger.E_ERROR(string.Format("Failed to read number of codebooks from {0}\n", cstring.FromCString(file_name)));
                fp.fclose();
                return PointerHelpers.NULL<Pointer<Pointer<Pointer<float>>>>();
            }
            n_mgau = BitConverter.ToInt32(tmp_buf, 0);
            out_n_mgau.Val = n_mgau;

            /* #Features/codebook */
            if (BinaryIO.bio_fread(tmp, 4, 1, fp, byteswap, chksum, logger) != 1)
            {
                logger.E_ERROR(string.Format("Failed to read number of features from {0}\n", cstring.FromCString(file_name)));
                fp.fclose();
                return PointerHelpers.NULL<Pointer<Pointer<Pointer<float>>>>();
            }
            n_feat = BitConverter.ToInt32(tmp_buf, 0);
            out_n_feat.Val = n_feat;

            /* #Gaussian densities/feature in each codebook */
            if (BinaryIO.bio_fread(tmp, 4, 1, fp, byteswap, chksum, logger) != 1)
            {
                logger.E_ERROR(string.Format("fread({0}) (#density/codebook) failed\n", cstring.FromCString(file_name)));
            }
            n_density = BitConverter.ToInt32(tmp_buf, 0);
            out_n_density.Val = n_density;

            /* #Dimensions in each feature stream */
            veclen = CKDAlloc.ckd_calloc<int>(n_feat);
            out_veclen.Val = veclen;
            Pointer<byte> veclen_buf = PointerHelpers.Malloc<byte>(n_feat * 4);
            if (BinaryIO.bio_fread(veclen_buf, 4, n_feat, fp, byteswap, chksum, logger) != n_feat)
            {
                logger.E_ERROR(string.Format("fread({0}) (feature-lengths) failed\n", cstring.FromCString(file_name)));
                fp.fclose();
                return PointerHelpers.NULL<Pointer<Pointer<Pointer<float>>>>();
            }

            // LOGAN modified Convert file data from byte to int32
            Pointer<int> upcastVeclen = veclen_buf.ReinterpretCast<int>();
            upcastVeclen.MemCopyTo(veclen, n_feat);

            /* blk = total vector length of all feature streams */
            for (i = 0, blk = 0; i < n_feat; i++)
                blk += veclen[i];

            /* #Floats to follow; for the ENTIRE SET of CODEBOOKS */
            if (BinaryIO.bio_fread(tmp, 4, 1, fp, byteswap, chksum, logger) != 1)
            {
                logger.E_ERROR(string.Format("Failed to read number of parameters from {0}\n", cstring.FromCString(file_name)));
                fp.fclose();
                return PointerHelpers.NULL<Pointer<Pointer<Pointer<float>>>>();
            }
            n = BitConverter.ToInt32(tmp_buf, 0);

            if (n != n_mgau * n_density * blk)
            {
                logger.E_ERROR(string.Format("Number of parameters in {0}({1}) doesn't match dimensions: {2} x {3} x {4}\n", cstring.FromCString(file_name), n, n_mgau, n_density, blk));
                fp.fclose();
                return PointerHelpers.NULL<Pointer<Pointer<Pointer<float>>>>();
            }

            /* Allocate memory for mixture gaussian densities if not already allocated */
            output = CKDAlloc.ckd_calloc_3d<Pointer<float>>((uint)n_mgau, (uint)n_feat, (uint)n_density);
            buf = CKDAlloc.ckd_calloc<float>(n);
            for (i = 0, l = 0; i < n_mgau; i++)
            {
                for (j = 0; j < n_feat; j++)
                {
                    for (k = 0; k < n_density; k++)
                    {
                        output[i][j].Set(k, buf.Point(l));
                        l += veclen[j];
                    }
                }
            }

            /* Read mixture gaussian densities data */
            Pointer<byte> buf2 = PointerHelpers.Malloc<byte>(n * 4);
            if (BinaryIO.bio_fread(buf2, 4, n, fp, byteswap, chksum, logger) != n)
            {
                logger.E_ERROR(string.Format("Failed to read density data from file '{0}'\n", cstring.FromCString(file_name)));
                fp.fclose();
                return PointerHelpers.NULL<Pointer<Pointer<Pointer<float>>>>();
            }

            // LOGAN modified Convert file data from byte to float32
            Pointer<float> upcastBuf = buf2.ReinterpretCast<float>();
            upcastBuf.MemCopyTo(buf, n);

            if (chksum_present != 0)
                BinaryIO.bio_verify_chksum(fp, byteswap, chksum.Val, logger);

            if (fp.fread(tmp, 1, 1) == 1)
            {
                logger.E_ERROR(string.Format("More data than expected in {0}\n", cstring.FromCString(file_name)));
                fp.fclose();
                return PointerHelpers.NULL<Pointer<Pointer<Pointer<float>>>>();
            }

            fp.fclose();

            logger.E_INFO(string.Format("{0} codebook, {1} feature, size: \n", n_mgau, n_feat));
            for (i = 0; i < n_feat; i++)
            {
                logger.E_INFO(string.Format(" {0}x{1}\n", n_density, veclen[i]));
            }

            return output;
        }
        
        /*
         * Some of the gaussian density computation can be carried out in advance:
         * 	log(determinant) calculation,
         * 	1/(2*var) in the exponent,
         * NOTE; The density computation is performed in log domain.
         */
        internal static int gauden_dist_precompute(gauden_t g, logmath_t lmath, float varfloor)
        {
            int i, m, f, d, flen;
            Pointer<float> meanp;
            Pointer<float> varp;
            Pointer<float> detp;
            int floored;

            floored = 0;
            /* Allocate space for determinants */
            g.det = CKDAlloc.ckd_calloc_3d<float>((uint)g.n_mgau, (uint)g.n_feat, (uint)g.n_density);

            for (m = 0; m < g.n_mgau; m++)
            {
                for (f = 0; f < g.n_feat; f++)
                {
                    flen = g.featlen[f];

                    /* Determinants for all variance vectors in g.[m][f] */
                    for (d = 0, detp = g.det[m][f]; d < g.n_density; d++, detp++)
                    {
                        detp.Deref = 0;
                        for (i = 0, varp = g.var[m][f][d], meanp = g.mean[m][f][d];
                             i < flen; i++, varp++, meanp++)
                        {
                            Pointer<float> fvarp = varp;
                            if (fvarp.Deref < varfloor)
                            {
                                fvarp.Deref = varfloor;
                                ++floored;
                            }

                            detp.Deref = detp.Deref + (float)LogMath.logmath_log(lmath,
                                                         1.0 / Math.Sqrt(fvarp.Deref * 2.0 * 3.1415926535897932385e0));
                            /* Precompute this part of the exponential */
                            varp.Deref = (float)LogMath.logmath_ln_to_log(lmath,
                                                              (1.0 / (fvarp.Deref * 2.0)));
                        }
                        
                    }
                }
            }

            g.logger.E_INFO(string.Format("{0} variance values floored\n", floored));

            return 0;
        }


        internal static gauden_t gauden_init(Pointer<byte> meanfile, Pointer<byte> varfile, FileAdapter fileAdapter, float varfloor, logmath_t lmath, SphinxLogger logger)
        {
            int i, m, f, d;
            Pointer<int> flen;
            gauden_t g;

            SphinxAssert.assert(meanfile.IsNonNull);
            SphinxAssert.assert(varfile.IsNonNull);
            SphinxAssert.assert(varfloor > 0.0);

            g = new gauden_t();
            g.logger = logger;
            g.lmath = lmath;

            BoxedValueInt out_n_mgau = new BoxedValueInt();
            BoxedValueInt out_n_feat = new BoxedValueInt();
            BoxedValueInt out_n_density = new BoxedValueInt();
            BoxedValue<Pointer<int>> boxed_featlen = new BoxedValue<Pointer<int>>();
            g.mean = gauden_param_read(meanfile, fileAdapter, out_n_mgau, out_n_feat, out_n_density, boxed_featlen, logger);
            if (g.mean.IsNull)
            {
                return null;
            }
            g.n_mgau = out_n_mgau.Val;
            g.n_feat = out_n_feat.Val;
            g.n_density = out_n_density.Val;
            g.featlen = boxed_featlen.Val;

            g.var = gauden_param_read(varfile, fileAdapter, out_n_mgau, out_n_feat, out_n_density, boxed_featlen, logger);
            if (g.var.IsNull)
            {
                return null;
            }
            m = out_n_mgau.Val;
            f = out_n_feat.Val;
            d = out_n_density.Val;
            flen = boxed_featlen.Val;

            /* Verify mean and variance parameter dimensions */
            if ((m != g.n_mgau) || (f != g.n_feat) || (d != g.n_density))
            {
                logger.E_ERROR
                    ("Mixture-gaussians dimensions for means and variances differ\n");
                return null;
            }
            for (i = 0; i < g.n_feat; i++)
            {
                if (g.featlen[i] != flen[i])
                {
                    logger.E_ERROR("Feature lengths for means and variances differ\n");
                    return null;
                }
            }

            gauden_dist_precompute(g, lmath, varfloor);

            return g;
        }

        /* See compute_dist below */
        internal static int compute_dist_all(Pointer<gauden_dist_t> out_dist, Pointer<float> obs, int featlen,
                         Pointer<Pointer<float>> mean, Pointer<Pointer<float>> var, Pointer<float> det,
                         int n_density)
        {
            int i, d;

            for (d = 0; d < n_density; ++d)
            {
                Pointer<float> m;
                Pointer<float> v;
                float dval;

                m = mean[d];
                v = var[d];
                dval = det[d];

                for (i = 0; i < featlen; i++)
                {
                    float diff;
                    diff = obs[i] - m[i];
                    /* The compiler really likes this to be a single
                     * expression, for whatever reason. */
                    dval -= diff * diff * v[i];
                }

                out_dist[d].dist = dval;
                out_dist[d].id = d;
            }

            return 0;
        }


        /*
         * Compute the top-N closest gaussians from the chosen set (mgau,feat)
         * for the given input observation vector.
         */
        internal static int
        compute_dist(Pointer<gauden_dist_t> out_dist, int n_top,
                     Pointer<float> obs, int featlen,
                     Pointer<Pointer<float>> mean, Pointer<Pointer<float>> var, Pointer<float> det,
                     int n_density)
        {
            int i, j, d;
            Pointer<gauden_dist_t> worst;

            /* Special case optimization when n_density <= n_top */
            if (n_top >= n_density)
                return (compute_dist_all
                        (out_dist, obs, featlen, mean, var, det, n_density));

            for (i = 0; i < n_top; i++)
                out_dist[i].dist = WORST_DIST;
            worst = out_dist.Point(n_top - 1);

            for (d = 0; d < n_density; d++)
            {
                Pointer<float> m;
                Pointer<float> v;
                float dval;

                m = mean[d];
                v = var[d];
                dval = det[d];

                for (i = 0; (i < featlen) && (dval >= worst.Deref.dist); i++)
                {
                    float diff;
                    diff = obs[i] - m[i];
                    /* The compiler really likes this to be a single
                     * expression, for whatever reason. */
                    dval -= diff * diff * v[i];
                }

                if ((i < featlen) || (dval < worst.Deref.dist))     /* Codeword d worse than worst */
                    continue;

                /* Codeword d at least as good as worst so far; insert in the ordered list */
                for (i = 0; (i < n_top) && (dval < out_dist[i].dist); i++) ;
                SphinxAssert.assert(i < n_top);
                for (j = n_top - 1; j > i; --j)
                    out_dist[j] = out_dist[j - 1];
                out_dist[i].dist = dval;
                out_dist[i].id = d;
            }

            return 0;
        }


        /*
         * Compute distances of the input observation from the top N codewords in the given
         * codebook (g.{mean,var}[mgau]).  The input observation, obs, includes vectors for
         * all features in the codebook.
         */
        internal static int
        gauden_dist(gauden_t g,
                    int mgau, int n_top, Pointer<Pointer<float>> obs, Pointer<Pointer<gauden_dist_t>> out_dist)
        {
            int f;

            SphinxAssert.assert((n_top > 0) && (n_top <= g.n_density));

            for (f = 0; f < g.n_feat; f++)
            {
                compute_dist(out_dist[f], n_top,
                             obs[f], g.featlen[f],
                             g.mean[mgau][f], g.var[mgau][f], g.det[mgau][f],
                             g.n_density);
                g.logger.E_DEBUG(string.Format("Top CW({0},{1}) = {2} {3}\n", mgau, f, out_dist[f][0].id,
                        (int)out_dist[f][0].dist >> HiddenMarkovModel.SENSCR_SHIFT));
            }

            return 0;
        }

        internal static int gauden_mllr_transform(gauden_t g, ps_mllr_t _mllr, cmd_ln_t config, FileAdapter fileAdapter)
        {
            int i, m, f, d;
            Pointer<int> flen;

            /* Free data if already here */
            g.det = PointerHelpers.NULL<Pointer<Pointer<float>>>();
            g.featlen = PointerHelpers.NULL<int>();

            /* Reload means and variances (un-precomputed). */
            BoxedValueInt out_n_mgau = new BoxedValueInt();
            BoxedValueInt out_n_feat = new BoxedValueInt();
            BoxedValueInt out_n_density = new BoxedValueInt();
            BoxedValue<Pointer<int>> out_featlen = new BoxedValue<Pointer<int>>();
            g.mean = gauden_param_read(CommandLine.cmd_ln_str_r(config, cstring.ToCString("_mean")), fileAdapter, out_n_mgau, out_n_feat, out_n_density, out_featlen, g.logger);
            g.n_mgau = out_n_mgau.Val;
            g.n_feat = out_n_feat.Val;
            g.n_density = out_n_density.Val;
            g.featlen = out_featlen.Val;

            g.var = gauden_param_read(CommandLine.cmd_ln_str_r(config, cstring.ToCString("_var")), fileAdapter, out_n_mgau, out_n_feat, out_n_density, out_featlen, g.logger);
            m = out_n_mgau.Val;
            f = out_n_feat.Val;
            d = out_n_density.Val;
            flen = out_featlen.Val;

            /* Verify mean and variance parameter dimensions */
            if ((m != g.n_mgau) || (f != g.n_feat) || (d != g.n_density))
                g.logger.E_FATAL
                    ("Mixture-gaussians dimensions for means and variances differ\n");
            for (i = 0; i < g.n_feat; i++)
                if (g.featlen[i] != flen[i])
                    g.logger.E_FATAL("Feature lengths for means and variances differ\n");

            /* Transform codebook for each stream s */
            for (i = 0; i < g.n_mgau; ++i)
            {
                for (f = 0; f < g.n_feat; ++f)
                {
                    Pointer<double> temp;
                    temp = CKDAlloc.ckd_calloc<double>(g.featlen[f]);
                    /* Transform each density d in selected codebook */
                    for (d = 0; d < g.n_density; d++)
                    {
                        int l;
                        for (l = 0; l < g.featlen[f]; l++)
                        {
                            temp[l] = 0.0;
                            for (m = 0; m < g.featlen[f]; m++)
                            {
                                /* FIXME: For now, only one class, hence the zeros below. */
                                temp[l] += _mllr.A[f][0][l][m] * g.mean[i][f][d][m];
                            }
                            temp[l] += _mllr.b[f][0][l];
                        }
                        
                        for (l = 0; l < g.featlen[f]; l++)
                        {
                            g.mean[i][f][d].Set(l, (float)temp[l]);
                            g.var[i][f][d].Set(l, g.var[i][f][d][l] * _mllr.h[f][0][l]);
                        }
                    }
                }
            }

            /* Re-precompute (if we aren't adapting variances this isn't
             * actually necessary...) */
            gauden_dist_precompute(g, g.lmath, (float)CommandLine.cmd_ln_float_r(config, cstring.ToCString("-varfloor")));
            return 0;
        }
    }
}
