using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class PhoneticMixGaussian
    {
        internal static readonly ps_mgaufuncs_t ptm_mgau_funcs = new ps_mgaufuncs_t()
        {
            name = cstring.ToCString("ptm"),
            frame_eval = ptm_mgau_frame_eval,
            transform = ptm_mgau_mllr_transform,
        };

        internal static void insertion_sort_topn(Pointer<ptm_topn_t> topn, int i, int d)
        {
            ptm_topn_t vtmp;
            int j;

            topn.Set(i, new ptm_topn_t()
            {
                score = d,
                cw = topn[i].cw
            });
            if (i == 0)
                return;
            vtmp = topn[i];
            for (j = i - 1; j >= 0 && d > topn[j].score; j--)
            {
                topn[j + 1] = topn[j];
            }
            topn[j + 1] = vtmp;
        }

        internal static int eval_topn(ptm_mgau_t s, int cb, int feat, Pointer<float> z)
        {
            Pointer<ptm_topn_t> topn;
            int i, ceplen;

            topn = s.f.Deref.topn[cb][feat];
            ceplen = s.g.featlen[feat];

            for (i = 0; i < s.max_topn; i++)
            {
                Pointer<float> mean;
                Pointer<float> diff = PointerHelpers.Malloc<float>(4);
                Pointer<float> sqdiff = PointerHelpers.Malloc<float>(4);
                Pointer<float> compl = PointerHelpers.Malloc<float>(4); /* diff, diff^2, component likelihood */
                Pointer<float> var;
                float d;
                Pointer<float> obs;
                int cw, j;

                cw = topn[i].cw;
                mean = s.g.mean[cb][feat][0] + cw * ceplen;
                var = s.g.var[cb][feat][0] + cw * ceplen;
                d = s.g.det[cb][feat][cw];
                obs = z;
                for (j = 0; j < ceplen % 4; ++j)
                {
                    diff[0] = (obs.Deref) - (mean.Deref);
                    obs++;
                    mean++;
                    sqdiff[0] = (diff[0] * diff[0]);
                    compl[0] = (sqdiff[0] * var.Deref);
                    d = (d - compl[0]);
                    ++var;
                }
                /* We could vectorize this but it's unlikely to make much
                 * difference as the outer loop here isn't very big. */
                for (; j < ceplen; j += 4)
                {
                    diff[0] = obs[0] - mean[0];
                    sqdiff[0] = (diff[0] * diff[0]);
                    compl[0] = (sqdiff[0] * var[0]);

                    diff[1] = obs[1] - mean[1];
                    sqdiff[1] = (diff[1] * diff[1]);
                    compl[1] = (sqdiff[1] * var[1]);

                    diff[2] = obs[2] - mean[2];
                    sqdiff[2] = (diff[2] * diff[2]);
                    compl[2] = (sqdiff[2] * var[2]);

                    diff[3] = obs[3] - mean[3];
                    sqdiff[3] = (diff[3] * diff[3]);
                    compl[3] = (sqdiff[3] * var[3]);

                    d = (d - compl[0]);
                    d = (d - compl[1]);
                    d = (d - compl[2]);
                    d = (d - compl[3]);
                    var += 4;
                    obs += 4;
                    mean += 4;
                }
                insertion_sort_topn(topn, i, (int)d);
            }

            return topn[0].score;
        }

        internal static int eval_cb(ptm_mgau_t s, int cb, int feat, Pointer<float> z)
        {
            Pointer<ptm_topn_t> topn;
            Pointer<float> mean;
            Pointer<float> var;
            Pointer<float> det;
            int i, ceplen;

            int detP_ptr;
            int detE_ptr;

            int best_ptr = 0;
            int worst_ptr = 0;

            topn = s.f.Deref.topn[cb][feat];
            best_ptr = 0;
            worst_ptr = (s.max_topn - 1);
            mean = s.g.mean[cb][feat][0];
            var = s.g.var[cb][feat][0];
            det = s.g.det[cb][feat];
            detE_ptr = s.g.n_density;
            ceplen = s.g.featlen[feat];

            for (detP_ptr = 0; detP_ptr < detE_ptr; ++detP_ptr)
            {
                Pointer<float> diff = PointerHelpers.Malloc<float>(4);
                Pointer<float> sqdiff = PointerHelpers.Malloc<float>(4);
                Pointer<float> compl = PointerHelpers.Malloc<float>(4); /* diff, diff^2, component likelihood */
                float d, thresh;
                Pointer<float> obs;
                int cur_ptr;
                int cw, j;

                d = det[detP_ptr];
                thresh = (float)topn[worst_ptr].score; /* Avoid int-to-float conversions */
                obs = z;
                cw = detP_ptr;

                /* Unroll the loop starting with the first dimension(s).  In
                 * theory this might be a bit faster if this Gaussian gets
                 * "knocked out" by C0. In practice not. */
                for (j = 0; (j < ceplen % 4) && (d >= thresh); ++j)
                {
                    diff[0] = (obs.Deref) - (mean.Deref);
                    obs++;
                    mean++;
                    sqdiff[0] = (diff[0] * diff[0]);
                    compl[0] = (sqdiff[0] * (var.Deref));
                    var++;
                    d = (d - compl[0]);
                }
                /* Now do 4 dimensions at a time.  You'd think that GCC would
                 * vectorize this?  Apparently not.  And it's right, because
                 * that won't make this any faster, at least on x86-64. */
                for (; j < ceplen && d >= thresh; j += 4)
                {
                    diff[0] = obs[0] - mean[0];
                    sqdiff[0] = (diff[0] * diff[0]);
                    compl[0] = (sqdiff[0] * var[0]);

                    diff[1] = obs[1] - mean[1];
                    sqdiff[1] = (diff[1] * diff[1]);
                    compl[1] = (sqdiff[1] * var[1]);

                    diff[2] = obs[2] - mean[2];
                    sqdiff[2] = (diff[2] * diff[2]);
                    compl[2] = (sqdiff[2] * var[2]);

                    diff[3] = obs[3] - mean[3];
                    sqdiff[3] = (diff[3] * diff[3]);
                    compl[3] = (sqdiff[3] * var[3]);

                    d = (d - compl[0]);
                    d = (d - compl[1]);
                    d = (d - compl[2]);
                    d = (d - compl[3]);
                    var += 4;
                    obs += 4;
                    mean += 4;
                }
                if (j < ceplen)
                {
                    /* terminated early, so not in topn */
                    mean += (ceplen - j);
                    var += (ceplen - j);
                    continue;
                }
                if (d < thresh)
                    continue;
                for (i = 0; i < s.max_topn; i++)
                {
                    /* already there, so don't need to insert */
                    if (topn[i].cw == cw)
                        break;
                }
                if (i < s.max_topn)
                    continue;       /* already there.  Don't insert */

                /* This looks bad, but it actually isn't.  Less than 1% of eval_cb's
                    * time is spent doing this. */
                for (cur_ptr = worst_ptr - 1; cur_ptr >= best_ptr && d >= topn[cur_ptr].score; --cur_ptr)
                {
                    topn.Set(cur_ptr + 1, new ptm_topn_t()
                    {
                        cw = topn[cur_ptr].cw,
                        score = topn[cur_ptr].score
                    });
                }

                ++cur_ptr;
                topn.Set(cur_ptr, new ptm_topn_t()
                {
                    cw = cw,
                    score = (int)d
                });
            }

            return topn[best_ptr].score;
        }

        /**
         * Compute top-N densities for active codebooks (and prune)
         */
        internal static int ptm_mgau_codebook_eval(ptm_mgau_t s, Pointer<Pointer<float>> z, int frame)
        { 
            int i, j;

            /* First evaluate top-N from previous frame. */
            for (i = 0; i < s.g.n_mgau; ++i)
                for (j = 0; j < s.g.n_feat; ++j)
                    eval_topn(s, i, j, z[j]);

            /* If frame downsampling is in effect, possibly do nothing else. */
            if (frame % s.ds_ratio != 0)
                return 0;

            /* Evaluate remaining codebooks. */
            for (i = 0; i < s.g.n_mgau; ++i)
            {
                if (BitVector.bitvec_is_clear(s.f.Deref.mgau_active, i) != 0)
                    continue;
                for (j = 0; j < s.g.n_feat; ++j)
                {
                    eval_cb(s, i, j, z[j]);
                }
            }
            return 0;
        }

        /**
         * Normalize densities to produce "posterior probabilities",
         * i.e. things with a reasonable dynamic range, then scale and
         * clamp them to the acceptable range.  This is actually done
         * solely to ensure that we can use fast_logmath_add().  Note that
         * unless we share the same normalizer across all codebooks for
         * each feature stream we get defective scores (that's why these
         * loops are inside out - doing it per-feature should give us
         * greater precision). */
        internal static int ptm_mgau_codebook_norm(ptm_mgau_t s, Pointer<Pointer<float>> z, int frame)
        {
            int i, j;

            for (j = 0; j < s.g.n_feat; ++j)
            {
                int norm = HiddenMarkovModel.WORST_SCORE;
                for (i = 0; i < s.g.n_mgau; ++i)
                {
                    if (BitVector.bitvec_is_clear(s.f.Deref.mgau_active, i) != 0)
                        continue;
                    if (norm < s.f.Deref.topn[i][j][0].score >> HiddenMarkovModel.SENSCR_SHIFT)
                        norm = s.f.Deref.topn[i][j][0].score >> HiddenMarkovModel.SENSCR_SHIFT;
                }
                SphinxAssert.assert(norm != HiddenMarkovModel.WORST_SCORE);
                for (i = 0; i < s.g.n_mgau; ++i)
                {
                    int k;
                    if (BitVector.bitvec_is_clear(s.f.Deref.mgau_active, i) != 0)
                        continue;
                    for (k = 0; k < s.max_topn; ++k)
                    {
                        // LOGAN modified this func to avoid constant dereferencing of an inaccessible field
                        int scr = s.f.Deref.topn[i][j][k].score;
                        scr >>= HiddenMarkovModel.SENSCR_SHIFT;
                        scr -= norm;
                        scr = -scr;
                        if (scr > MixGaussianCommon.MAX_NEG_ASCR)
                            scr = MixGaussianCommon.MAX_NEG_ASCR;

                        s.f.Deref.topn[i][j].Set(k, new ptm_topn_t()
                        {
                            score = scr,
                            cw = s.f.Deref.topn[i][j][k].cw
                        });
                    }
                }
            }

            return 0;
        }

        internal static int ptm_mgau_calc_cb_active(ptm_mgau_t s, Pointer<byte> senone_active,
                                int n_senone_active, int compallsen, SphinxLogger logger)
        {
            int i, lastsen;

            if (compallsen != 0)
            {
                BitVector.bitvec_set_all(s.f.Deref.mgau_active, s.g.n_mgau);
                return 0;
            }
            BitVector.bitvec_clear_all(s.f.Deref.mgau_active, s.g.n_mgau);
            for (lastsen = i = 0; i < n_senone_active; ++i)
            {
                int sen = senone_active[i] + lastsen;
                int cb = s.sen2cb[sen];
                BitVector.bitvec_set(s.f.Deref.mgau_active, cb);
                lastsen = sen;
            }
            logger.E_DEBUG("Active codebooks:");
            for (i = 0; i < s.g.n_mgau; ++i)
            {
                if (BitVector.bitvec_is_clear(s.f.Deref.mgau_active, i) != 0)
                    continue;
                logger.E_DEBUG(string.Format(" {0}", i));
            }
            return 0;
        }

        /**
         * Compute senone scores from top-N densities for active codebooks.
         */
        internal static int ptm_mgau_senone_eval(ptm_mgau_t s, short[] senone_scores,
                             Pointer<byte> senone_active, int n_senone_active,
                             int compall, SphinxLogger logger)
        {
            int i, lastsen, bestscore;

            Arrays.MemSetShort(senone_scores, 0, s.n_sen);
            /* FIXME: This is the non-cache-efficient way to do this.  We want
             * to evaluate one codeword at a time but this requires us to have
             * a reverse codebook to senone mapping, which we don't have
             * (yet), since different codebooks have different top-N
             * codewords. */
            if (compall != 0)
                n_senone_active = s.n_sen;
            bestscore = 0x7fffffff;
            for (lastsen = i = 0; i < n_senone_active; ++i)
            {
                int sen, f, cb;
                int ascore;

                if (compall != 0)
                    sen = i;
                else
                    sen = senone_active[i] + lastsen;
                lastsen = sen;
                cb = s.sen2cb[sen];

                if (BitVector.bitvec_is_clear(s.f.Deref.mgau_active, cb) != 0)
                {
                    int j;
                    /* Because senone_active is deltas we can't really "knock
                     * out" senones from pruned codebooks, and in any case,
                     * it wouldn't make any difference to the search code,
                     * which doesn't expect senone_active to change. */
                    for (f = 0; f < s.g.n_feat; ++f)
                    {
                        for (j = 0; j < s.max_topn; ++j)
                        {
                            s.f.Deref.topn[cb][f].Set(j, new ptm_topn_t()
                            {
                                cw = s.f.Deref.topn[cb][f][j].cw,
                                score = MixGaussianCommon.MAX_NEG_ASCR
                            });
                        }
                    }
                }
                /* For each feature, log-sum codeword scores + mixw to get
                 * feature density, then sum (multiply) to get ascore */
                ascore = 0;
                for (f = 0; f < s.g.n_feat; ++f)
                {
                    Pointer<ptm_topn_t> topn;
                    int j, fden = 0;
                    topn = s.f.Deref.topn[cb][f];
                    for (j = 0; j < s.max_topn; ++j)
                    {
                        int mixw;
                        /* Find mixture weight for this codeword. */
                        if (s.mixw_cb.IsNonNull)
                        {
                            int dcw = s.mixw[f][topn[j].cw][sen / 2];
                            dcw = (dcw & 1) != 0 ? dcw >> 4 : dcw & 0x0f;
                            mixw = s.mixw_cb[dcw];
                        }
                        else
                        {
                            mixw = s.mixw[f][topn[j].cw][sen];
                        }
                        if (j == 0)
                            fden = mixw + topn[j].score;
                        else
                            fden = MixGaussianCommon.fast_logmath_add(s.lmath_8b, fden,
                                               mixw + topn[j].score);
                        logger.E_DEBUG(string.Format("fden[{0}][{1}] l+= {2} + {3} = {4}\n",
                                sen, f, mixw, topn[j].score, fden));
                    }
                    ascore += fden;
                }
                if (ascore < bestscore) bestscore = ascore;
                senone_scores[sen] = checked((short)ascore);
            }
            /* Normalize the scores again (finishing the job we started above
             * in ptm_mgau_codebook_eval...) */
            for (i = 0; i < s.n_sen; ++i)
            {
                senone_scores[i] = checked((short)(senone_scores[i] - bestscore));
            }

            return 0;
        }

        /**
         * Compute senone scores for the active senones.
         */
        internal static int ptm_mgau_frame_eval(ps_mgau_t ps,
                            short[] senone_scores,
                            Pointer<byte> senone_active,
                            int n_senone_active,
                            Pointer<Pointer<float>> featbuf,
                            int frame,
                            int compallsen,
                            SphinxLogger logger)
        {
            ptm_mgau_t s = (ptm_mgau_t)ps;
            int fast_eval_idx;

            /* Find the appropriate frame in the rotating history buffer
             * corresponding to the requested input frame.  No bounds checking
             * is done here, which just means you'll get semi-random crap if
             * you request a frame in the future or one that's too far in the
             * past.  Since the history buffer is just used for fast match
             * that might not be fatal. */
            fast_eval_idx = frame % s.n_fast_hist;
            s.f = s.hist + fast_eval_idx;
            /* Compute the top-N codewords for every codebook, unless this
             * is a past frame, in which case we already have them (we
             * hope!) */
            if (frame >= ps.frame_idx)
            {
                Pointer<ptm_fast_eval_t> lastf;
                /* Get the previous frame's top-N information (on the
                 * first frame of the input this is just all WORST_DIST,
                 * no harm in that) */
                if (fast_eval_idx == 0)
                    lastf = s.hist + s.n_fast_hist - 1;
                else
                    lastf = s.hist + fast_eval_idx - 1;
                /* Copy in initial top-N info */
                // LOGAN modified - need to do a deep copy of the data here because it is a struct
                for (int c = 0; c < s.g.n_mgau * s.g.n_feat * s.max_topn; c++)
                {
                    s.f.Deref.topn[0][0].Set(c, new ptm_topn_t()
                    {
                        cw = lastf.Deref.topn[0][0][c].cw,
                        score = lastf.Deref.topn[0][0][c].score
                    });
                }

                /* Generate initial active codebook list (this might not be
                 * necessary) */
                ptm_mgau_calc_cb_active(s, senone_active, n_senone_active, compallsen, logger);
                /* Now evaluate top-N, prune, and evaluate remaining codebooks. */
                ptm_mgau_codebook_eval(s, featbuf, frame);
                ptm_mgau_codebook_norm(s, featbuf, frame);
            }
            /* Evaluate intersection of active senones and active codebooks. */
            ptm_mgau_senone_eval(s, senone_scores, senone_active,
                                 n_senone_active, compallsen, logger);

            return 0;
        }

        internal static int read_sendump(ptm_mgau_t s, bin_mdef_t mdef, Pointer<byte> file, FileAdapter fileAdapter, SphinxLogger logger)
        {
            FILE fp;
            Pointer<byte> line = PointerHelpers.Malloc<byte>(1000);
            int i, n, r, c;
            int do_swap;
            uint offset;
            int n_clust = 0;
            int n_feat = s.g.n_feat;
            int n_density = s.g.n_density;
            int n_sen = BinaryModelDef.bin_mdef_n_sen(mdef);
            int n_bits = 8;

            s.n_sen = n_sen; /* FIXME: Should have been done earlier */

            if ((fp = fileAdapter.fopen(file, "rb")) == null)
                return -1;


            byte[] tmp_bytes = new byte[4];
            Pointer<byte> tmp_byte_ptr = new Pointer<byte>(tmp_bytes);
            Pointer<uint> tmp_uint_ptr = tmp_byte_ptr.ReinterpretCast<uint>();
            Pointer<int> tmp_int_ptr = tmp_byte_ptr.ReinterpretCast<int>();
            logger.E_INFO(string.Format("Loading senones from dump file {0}\n", cstring.FromCString(file)));
            /* Read title size, title */
            if (fp.fread(tmp_byte_ptr, 4, 1) != 1)
            {
                logger.E_ERROR_SYSTEM(string.Format("Failed to read title size from {0}", cstring.FromCString(file)));
                goto error_out;
            }
            n = tmp_int_ptr[0];

            /* This is extremely bogus */
            do_swap = 0;
            if (n < 1 || n > 999)
            {
                n = ByteOrder.SWAP_INT32(n);
                if (n < 1 || n > 999)
                {
                    logger.E_ERROR(string.Format("Title length {0} in dump file {1} out of range\n", n, cstring.FromCString(file)));
                    goto error_out;
                }
                do_swap = 1;
            }
            if (fp.fread(line, 1, (uint)n) != n)
            {
                logger.E_ERROR_SYSTEM("Cannot read title");
                goto error_out;
            }
            if (line[n - 1] != '\0')
            {
                logger.E_ERROR("Bad title in dump file\n");
                goto error_out;
            }
            logger.E_INFO(string.Format("{0}\n", cstring.FromCString(line)));

            /* Read header size, header */
            if (fp.fread(tmp_byte_ptr, 4, 1) != 1)
            {
                logger.E_ERROR_SYSTEM(string.Format("Failed to read header size from {0}", cstring.FromCString(file)));
                goto error_out;
            }
            n = tmp_int_ptr[0];

            if (do_swap != 0) n = ByteOrder.SWAP_INT32(n);
            if (fp.fread(line, 1, (uint)n) != n)
            {
                logger.E_ERROR_SYSTEM("Cannot read header");
                goto error_out;
            }
            if (line[n - 1] != '\0')
            {
                logger.E_ERROR("Bad header in dump file\n");
                goto error_out;
            }

            /* Read other header strings until string length = 0 */
            for (;;)
            {
                if (fp.fread(tmp_byte_ptr, 4, 1) != 1)
                {
                    logger.E_ERROR_SYSTEM(string.Format("Failed to read header string size from {0}", cstring.FromCString(file)));
                    goto error_out;
                }
                n = tmp_int_ptr[0];

                if (do_swap != 0) n = ByteOrder.SWAP_INT32(n);
                if (n == 0)
                    break;

                if (fp.fread(line, 1, (uint)n) != n)
                {
                    logger.E_ERROR_SYSTEM("Cannot read header");
                    goto error_out;
                }
                /* Look for a cluster count, if present */
                if (cstring.strncmp(line, cstring.ToCString("feature_count "), cstring.strlen(cstring.ToCString("feature_count "))) == 0)
                {
                    n_feat = cstring.atoi(line + cstring.strlen(cstring.ToCString("feature_count ")));
                }
                if (cstring.strncmp(line, cstring.ToCString("mixture_count "), cstring.strlen(cstring.ToCString("mixture_count "))) == 0)
                {
                    n_density = cstring.atoi(line + cstring.strlen(cstring.ToCString("mixture_count ")));
                }
                if (cstring.strncmp(line, cstring.ToCString("model_count "), cstring.strlen(cstring.ToCString("model_count "))) == 0)
                {
                    n_sen = cstring.atoi(line + cstring.strlen(cstring.ToCString("model_count ")));
                }
                if (cstring.strncmp(line, cstring.ToCString("cluster_count "), cstring.strlen(cstring.ToCString("cluster_count "))) == 0)
                {
                    n_clust = cstring.atoi(line + cstring.strlen(cstring.ToCString("cluster_count ")));
                }
                if (cstring.strncmp(line, cstring.ToCString("cluster_bits "), cstring.strlen(cstring.ToCString("cluster_bits "))) == 0)
                {
                    n_bits = cstring.atoi(line + cstring.strlen(cstring.ToCString("cluster_bits ")));
                }
            }

            /* Defaults for #rows, #columns in mixw array. */
            c = n_sen;
            r = n_density;
            if (n_clust == 0)
            {
                /* Older mixw files have them here, and they might be padded. */
                if (fp.fread(tmp_byte_ptr, 4, 1) != 1)
                {
                    logger.E_ERROR_SYSTEM("Cannot read #rows");
                    goto error_out;
                }
                r = tmp_int_ptr[0];

                if (do_swap != 0) r = ByteOrder.SWAP_INT32(r);
                if (fp.fread(tmp_byte_ptr, 4, 1) != 1)
                {
                    logger.E_ERROR_SYSTEM("Cannot read #columns");
                    goto error_out;
                }
                c = tmp_int_ptr[0];

                if (do_swap != 0) c = ByteOrder.SWAP_INT32(c);
                logger.E_INFO(string.Format("Rows: {0}, Columns: {1}\n", r, c));
            }

            if (n_feat != s.g.n_feat)
            {
                logger.E_ERROR(string.Format("Number of feature streams mismatch: {0} != {1}\n",
                        n_feat, s.g.n_feat));
                goto error_out;
            }
            if (n_density != s.g.n_density)
            {
                logger.E_ERROR(string.Format("Number of densities mismatch: {0} != {1}\n",
                        n_density, s.g.n_density));
                goto error_out;
            }
            if (n_sen != s.n_sen)
            {
                logger.E_ERROR(string.Format("Number of senones mismatch: {0} != {1}\n",
                        n_sen, s.n_sen));
                goto error_out;
            }

            if (!((n_clust == 0) || (n_clust == 15) || (n_clust == 16)))
            {
                logger.E_ERROR("Cluster count must be 0, 15, or 16\n");
                goto error_out;
            }
            if (n_clust == 15)
                ++n_clust;

            if (!((n_bits == 8) || (n_bits == 4)))
            {
                logger.E_ERROR("Cluster count must be 4 or 8\n");
                goto error_out;
            }

            offset = (uint)fp.ftell();

            /* Allocate memory for pdfs (or memory map them) */
            /* Get cluster codebook if any. */
            if (n_clust != 0)
            {
                s.mixw_cb = CKDAlloc.ckd_calloc<byte>(n_clust);
                if (fp.fread(s.mixw_cb, 1, (uint)n_clust) != (uint)n_clust)
                {
                    logger.E_ERROR(string.Format("Failed to read {0} bytes from sendump\n", n_clust));
                    goto error_out;
                }
            }

            /* Set up pointers, or read, or whatever */
            s.mixw = CKDAlloc.ckd_calloc_3d<byte>((uint)n_feat, (uint)n_density, (uint)n_sen);
            /* Read pdf values and ids */
            for (n = 0; n < n_feat; n++)
            {
                int step = c;
                if (n_bits == 4)
                    step = (step + 1) / 2;
                for (i = 0; i < r; i++)
                {
                    if (fp.fread(s.mixw[n][i], 1, (uint)step) != (uint)step)
                    {
                        logger.E_ERROR(string.Format("Failed to read {0} bytes from sendump\n", step));
                        goto error_out;
                    }
                }
            }

            fp.fclose();
            return 0;

            error_out:
            fp.fclose();
            return -1;
        }

        internal static int read_mixw(ptm_mgau_t s, Pointer<byte> file_name, FileAdapter fileAdapter, double SmoothMin, SphinxLogger logger)
        {
            Pointer<Pointer<byte>> argname;
            Pointer<Pointer<byte>> argval;
            FILE fp;
            int byteswap, chksum_present;
            BoxedValueUInt chksum = new BoxedValueUInt();
            Pointer<float> pdf;
            int i, f, c, n;
            int n_sen;
            int n_feat;
            int n_comp;
            int n_err;

            logger.E_INFO(string.Format("Reading mixture weights file '{0}'\n", cstring.FromCString(file_name)));

            if ((fp = fileAdapter.fopen(file_name, "rb")) == null)
                logger.E_FATAL_SYSTEM(string.Format("Failed to open mixture file '{0}' for reading", cstring.FromCString(file_name)));

            /* Read header, including argument-value info and 32-bit byteorder magic */
            BoxedValue<Pointer<Pointer<byte>>> boxed_argname = new BoxedValue<Pointer<Pointer<byte>>>();
            BoxedValue<Pointer<Pointer<byte>>> boxed_argval = new BoxedValue<Pointer<Pointer<byte>>>();
            if (BinaryIO.bio_readhdr(fp, boxed_argname, boxed_argval, out byteswap, logger) < 0)
                logger.E_FATAL(string.Format("Failed to read header from '{0}'\n", cstring.FromCString(file_name)));

            argname = boxed_argname.Val;
            argval = boxed_argval.Val;

            /* Parse argument-value list */
            chksum_present = 0;
            for (i = 0; argname[i].IsNonNull; i++)
            {
                if (cstring.strcmp(argname[i], cstring.ToCString("version")) == 0)
                {
                    if (cstring.strcmp(argval[i], MixGaussianCommon.MGAU_MIXW_VERSION) != 0)
                        logger.E_WARN(string.Format("Version mismatch({0}): {1}, expecting {2}\n",
                               cstring.FromCString(file_name), cstring.FromCString(argval[i]), cstring.FromCString(MixGaussianCommon.MGAU_MIXW_VERSION)));
                }
                else if (cstring.strcmp(argname[i], cstring.ToCString("chksum0")) == 0)
                {
                    chksum_present = 1; /* Ignore the associated value */
                }
            }
            
            argname = argval = PointerHelpers.NULL<Pointer<byte>>();

            chksum.Val = 0;

            byte[] tmp_bytes = new byte[4];
            Pointer<byte> tmp_byte_ptr = new Pointer<byte>(tmp_bytes);
            Pointer<uint> tmp_uint_ptr = tmp_byte_ptr.ReinterpretCast<uint>();
            Pointer<int> tmp_int_ptr = tmp_byte_ptr.ReinterpretCast<int>();
            /* Read #senones, #features, #codewords, arraysize */
            if ((BinaryIO.bio_fread(tmp_byte_ptr, 4, 1, fp, byteswap, chksum, logger) != 1))
            {
                logger.E_FATAL(string.Format("bio_fread({0}) (arraysize) failed\n", cstring.FromCString(file_name)));
            }
            n_sen = tmp_int_ptr[0];

            if ((BinaryIO.bio_fread(tmp_byte_ptr, 4, 1, fp, byteswap, chksum, logger) != 1))
            {
                logger.E_FATAL(string.Format("bio_fread({0}) (arraysize) failed\n", cstring.FromCString(file_name)));
            }
            n_feat = tmp_int_ptr[0];

            if ((BinaryIO.bio_fread(tmp_byte_ptr, 4, 1, fp, byteswap, chksum, logger) != 1))
            {
                logger.E_FATAL(string.Format("bio_fread({0}) (arraysize) failed\n", cstring.FromCString(file_name)));
            }
            n_comp = tmp_int_ptr[0];

            if ((BinaryIO.bio_fread(tmp_byte_ptr, 4, 1, fp, byteswap, chksum, logger) != 1))
            {
                logger.E_FATAL(string.Format("bio_fread({0}) (arraysize) failed\n", cstring.FromCString(file_name)));
            }
            n = tmp_int_ptr[0];


            if (n_feat != s.g.n_feat)
                logger.E_FATAL(string.Format("#Features streams({0}) != {1}\n", n_feat, s.g.n_feat));
            if (n != n_sen * n_feat * n_comp)
            {
                logger.E_FATAL
                    (string.Format("{0}: #float32s({1}) doesn't match header dimensions: {2} x {3} x {4}\n",
                     cstring.FromCString(file_name), i, n_sen, n_feat, n_comp));
            }

            /* n_sen = number of mixture weights per codeword, which is
             * fixed at the number of senones since we have only one codebook.
             */
            s.n_sen = n_sen;

            /* Quantized mixture weight arrays. */
            s.mixw = CKDAlloc.ckd_calloc_3d<byte>((uint)s.g.n_feat, (uint)s.g.n_density, (uint)n_sen);

            /* Temporary structure to read in floats before conversion to (int) logs3 */
            pdf = CKDAlloc.ckd_calloc<float>(n_comp);

            Pointer<byte> pdf_byte = PointerHelpers.Malloc<byte>(n_comp * 4);
            Pointer<float> pdf_float = pdf_byte.ReinterpretCast<float>();

            /* Read senone probs data, normalize, floor, convert to logs3, truncate to 8 bits */
            n_err = 0;
            for (i = 0; i < n_sen; i++)
            {
                for (f = 0; f < n_feat; f++)
                {
                    if (BinaryIO.bio_fread(pdf_byte, 4, n_comp, fp, byteswap, chksum, logger) != n_comp)
                    {
                        logger.E_FATAL(string.Format("bio_fread({0}) (arraydata) failed\n", cstring.FromCString(file_name)));
                    }
                    pdf_float.MemCopyTo(pdf, n_comp);

                    /* Normalize and floor */
                    if (Vector.vector_sum_norm(pdf, n_comp) <= 0.0)
                        n_err++;
                    Vector.vector_floor(pdf, n_comp, SmoothMin);
                    Vector.vector_sum_norm(pdf, n_comp);

                    /* Convert to LOG, quantize, and transpose */
                    for (c = 0; c < n_comp; c++)
                    {
                        int qscr;

                        qscr = -LogMath.logmath_log(s.lmath_8b, pdf[c]);
                        if ((qscr > MixGaussianCommon.MAX_NEG_MIXW) || (qscr < 0))
                            qscr = MixGaussianCommon.MAX_NEG_MIXW;
                        s.mixw[f][c].Set(i, checked((byte)qscr));
                    }
                }
            }
            if (n_err > 0)
                logger.E_WARN(string.Format("Weight normalization failed for {0} mixture weights components\n", n_err));

            if (chksum_present != 0)
                BinaryIO.bio_verify_chksum(fp, byteswap, chksum.Val, logger);

            if (fp.fread(tmp_byte_ptr, 1, 1) == 1)
                logger.E_FATAL(string.Format("More data than expected in {0}\n", cstring.FromCString(file_name)));

            fp.fclose();

            logger.E_INFO(string.Format("Read {0} x {1} x {2} mixture weights\n", n_sen, n_feat, n_comp));
            return n_sen;
        }

        internal static ps_mgau_t ptm_mgau_init(acmod_t acmod, bin_mdef_t mdef, FileAdapter fileAdapter, SphinxLogger logger)
        {
            ptm_mgau_t s;
            ps_mgau_t ps;
            Pointer <byte> sendump_path;
            int i;

            s = new ptm_mgau_t();
            s.config = acmod.config;

            s.lmath = acmod.lmath;
            /* Log-add table. */
            s.lmath_8b = LogMath.logmath_init(LogMath.logmath_get_base(acmod.lmath), HiddenMarkovModel.SENSCR_SHIFT, 1, logger);
            if (s.lmath_8b == null)
                return null;

            /* Ensure that it is only 8 bits wide so that fast_logmath_add() works. */
            if (LogMath.logmath_get_width(s.lmath_8b) != 1)
            {
                logger.E_ERROR(string.Format("Log base {0} is too small to represent add table in 8 bits\n",
                        LogMath.logmath_get_base(s.lmath_8b)));
                return null;
            }

            /* Read means and variances. */
            if ((s.g = MultiStreamGaussDensity.gauden_init(CommandLine.cmd_ln_str_r(s.config, cstring.ToCString("_mean")),
                CommandLine.cmd_ln_str_r(s.config, cstring.ToCString("_var")),
                fileAdapter,
                (float)CommandLine.cmd_ln_float_r(s.config, cstring.ToCString("-varfloor")),
                s.lmath,
                logger)) == null)
            {
                logger.E_ERROR("Failed to read means and variances\n");
                return null;
            }

            /* We only support 256 codebooks or less (like 640k or 2GB, this
             * should be enough for anyone) */
            if (s.g.n_mgau > 256)
            {
                logger.E_INFO(string.Format("Number of codebooks exceeds 256: {0}\n", s.g.n_mgau));
                return null;
            }
            if (s.g.n_mgau != BinaryModelDef.bin_mdef_n_ciphone(mdef))
            {
                logger.E_INFO(string.Format("Number of codebooks doesn't match number of ciphones, doesn't look like PTM: {0} != {1}\n", s.g.n_mgau, BinaryModelDef.bin_mdef_n_ciphone(mdef)));
                return null;
            }
            /* Verify n_feat and veclen, against acmod. */
            if (s.g.n_feat != Feature.feat_dimension1(acmod.fcb))
            {
                logger.E_ERROR(string.Format("Number of streams does not match: {0} != {1}\n",
                        s.g.n_feat, Feature.feat_dimension1(acmod.fcb)));
                return null;
            }
            for (i = 0; i < s.g.n_feat; ++i)
            {
                if (s.g.featlen[i] != Feature.feat_dimension2(acmod.fcb, i))
                {
                    logger.E_ERROR(string.Format("Dimension of stream {0} does not match: {1} != {2}\n",
                            i, s.g.featlen[i], Feature.feat_dimension2(acmod.fcb, i)));
                    return null;
                }
            }
            /* Read mixture weights. */
            if ((sendump_path = CommandLine.cmd_ln_str_r(s.config, cstring.ToCString("_sendump"))).IsNonNull)
            {
                if (read_sendump(s, acmod.mdef, sendump_path, fileAdapter, logger) < 0)
                {
                    return null;
                }
            }
            else
            {
                if (read_mixw(s, CommandLine.cmd_ln_str_r(s.config, cstring.ToCString("_mixw")),
                    fileAdapter,
                              CommandLine.cmd_ln_float_r(s.config, cstring.ToCString("-mixwfloor")), logger) < 0)
                {
                    return null;
                }
            }
            s.ds_ratio = checked((short)CommandLine.cmd_ln_int_r(s.config, cstring.ToCString("-ds")));
            s.max_topn = checked((short)CommandLine.cmd_ln_int_r(s.config, cstring.ToCString("-topn")));
            logger.E_INFO(string.Format("Maximum top-N: {0}\n", s.max_topn));

            /* Assume mapping of senones to their base phones, though this
             * will become more flexible in the future. */
            s.sen2cb = CKDAlloc.ckd_calloc<byte>(s.n_sen);
            for (i = 0; i < s.n_sen; ++i)
                s.sen2cb[i] = checked((byte)BinaryModelDef.bin_mdef_sen2cimap(acmod.mdef, i));

            /* Allocate fast-match history buffers.  We need enough for the
             * phoneme lookahead window, plus the current frame, plus one for
             * good measure? (FIXME: I don't remember why) */
            s.n_fast_hist = (int)CommandLine.cmd_ln_int_r(s.config, cstring.ToCString("-pl_window")) + 2;
            s.hist = CKDAlloc.ckd_calloc_struct<ptm_fast_eval_t>(s.n_fast_hist);
            /* s.f will be a rotating pointer into s.hist. */
            s.f = s.hist;
            for (i = 0; i < s.n_fast_hist; ++i)
            {
                int j, k, m;
                /* Top-N codewords for every codebook and feature. */
                s.hist[i].topn = CKDAlloc.ckd_calloc_struct_3d<ptm_topn_t>((uint)s.g.n_mgau, (uint)s.g.n_feat, (uint)s.max_topn);
                /* Initialize them to sane (yet arbitrary) defaults. */
                for (j = 0; j < s.g.n_mgau; ++j)
                {
                    for (k = 0; k < s.g.n_feat; ++k)
                    {
                        for (m = 0; m < s.max_topn; ++m)
                        {
                            s.hist[i].topn[j][k].Set(m, new ptm_topn_t()
                            {
                                cw = m,
                                score = MixGaussianCommon.WORST_DIST
                            });
                        }
                    }
                }
                /* Active codebook mapping (just codebook, not features,
                   at least not yet) */
                s.hist[i].mgau_active = BitVector.bitvec_alloc(s.g.n_mgau);
                /* Start with them all on, prune them later. */
                BitVector.bitvec_set_all(s.hist[i].mgau_active, s.g.n_mgau);
            }

            ps = s;
            ps.vt = ptm_mgau_funcs;
            return ps;
        }

        internal static int ptm_mgau_mllr_transform(ps_mgau_t ps,
                                    ps_mllr_t mllr, FileAdapter fileAdapter)
        {
            ptm_mgau_t s = (ptm_mgau_t)ps;
            return MultiStreamGaussDensity.gauden_mllr_transform(s.g, mllr, s.config, fileAdapter);
        }
    }
}
