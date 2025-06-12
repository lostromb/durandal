using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class MultiStreamMixGaussian
    {
        internal static ps_mgaufuncs_t ms_mgau_funcs = new ps_mgaufuncs_t()
        {
            name = cstring.ToCString("ms"),
            frame_eval = ms_cont_mgau_frame_eval,
            transform = ms_mgau_mllr_transform,
        };

        internal static gauden_t ms_mgau_gauden(ms_mgau_model_t msg)
        {
            return msg.g;
        }

        internal static senone_t ms_mgau_senone(ms_mgau_model_t msg)
        {
            return msg.s;
        }

        internal static int ms_mgau_topn(ms_mgau_model_t msg)
        {
            return msg.topn;
        }

        internal static ps_mgau_t ms_mgau_init(acmod_t acmod, logmath_t lmath, bin_mdef_t mdef, FileAdapter fileAdapter, SphinxLogger logger)
        {
            /* Codebooks */
            ms_mgau_model_t msg;
            ps_mgau_t mg;
            gauden_t g;
            senone_t s;
            cmd_ln_t config;
            int i;

            config = acmod.config;

            msg = new ms_mgau_model_t();
            msg.config = config;
            msg.g = null;
            msg.s = null;

            if ((g = msg.g = MultiStreamGaussDensity.gauden_init(CommandLine.cmd_ln_str_r(config, cstring.ToCString("_mean")),
                                     CommandLine.cmd_ln_str_r(config, cstring.ToCString("_var")),
                                     fileAdapter,
                                     (float)CommandLine.cmd_ln_float_r(config, cstring.ToCString("-varfloor")),
                                     lmath,
                                     logger)) == null)
            {
                logger.E_ERROR("Failed to read means and variances\n");
                return null;
            }

            /* Verify n_feat and veclen, against acmod. */
            if (g.n_feat != Feature.feat_dimension1(acmod.fcb))
            {
                logger.E_ERROR(string.Format("Number of streams does not match: {0} != {1}\n",
                        g.n_feat, Feature.feat_dimension1(acmod.fcb)));
                return null;
            }
            for (i = 0; i < g.n_feat; ++i)
            {
                if (g.featlen[i] != Feature.feat_dimension2(acmod.fcb, i))
                {
                    logger.E_ERROR(string.Format("Dimension of stream {0} does not match: {1} != {2}\n", i,
                            g.featlen[i], Feature.feat_dimension2(acmod.fcb, i)));
                    return null;
                }
            }

            s = msg.s = MultiStreamSenone.senone_init(msg.g,
                                     CommandLine.cmd_ln_str_r(config, cstring.ToCString("_mixw")),
                                     CommandLine.cmd_ln_str_r(config, cstring.ToCString("_senmgau")),
                                     fileAdapter,
                                     (float)CommandLine.cmd_ln_float_r(config, cstring.ToCString("-mixwfloor")),
                                     lmath, mdef, logger);

            s.aw = (int)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-aw"));

            /* Verify senone parameters against gauden parameters */
            if (s.n_feat != g.n_feat)
                logger.E_FATAL(string.Format("#Feature mismatch: gauden= {0}, senone= {1}\n", g.n_feat,
                        s.n_feat));
            if (s.n_cw != g.n_density)
                logger.E_FATAL(string.Format("#Densities mismatch: gauden= {0}, senone= {1}\n",
                        g.n_density, s.n_cw));
            if (s.n_gauden > g.n_mgau)
                logger.E_FATAL(string.Format("Senones need more codebooks ({0}) than present ({1})\n",
                        s.n_gauden, g.n_mgau));
            if (s.n_gauden < g.n_mgau)
                logger.E_ERROR(string.Format("Senones use fewer codebooks ({0}) than present ({1})\n",
                        s.n_gauden, g.n_mgau));

            msg.topn = (int)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-topn"));
            logger.E_INFO(string.Format("The value of topn: {0}\n", msg.topn));
            if (msg.topn == 0 || msg.topn > msg.g.n_density)
            {
                logger.E_WARN
                    (string.Format("-topn argument ({0}) invalid or > #density codewords ({1}); set to latter\n",
                     msg.topn, msg.g.n_density));
                msg.topn = msg.g.n_density;
            }

            msg.dist = CKDAlloc.ckd_calloc_struct_3d<gauden_dist_t>((uint)g.n_mgau, (uint)g.n_feat, (uint)msg.topn);
            msg.mgau_active = CKDAlloc.ckd_calloc<byte>(g.n_mgau);

            mg = (ps_mgau_t)msg;
            mg.vt = ms_mgau_funcs;
            return mg;
        }

        internal static int ms_mgau_mllr_transform(ps_mgau_t s,
                       ps_mllr_t mllr, FileAdapter fileAdapter)
        {
            ms_mgau_model_t msg = (ms_mgau_model_t)s;
            return MultiStreamGaussDensity.gauden_mllr_transform(msg.g, mllr, msg.config, fileAdapter);
        }

        internal static int ms_cont_mgau_frame_eval(ps_mgau_t mg,
                    short[] senscr,
                    Pointer<byte> senone_active,
                    int n_senone_active,
                    Pointer<Pointer<float>> feats,
                    int frame,
                    int compallsen,
                    SphinxLogger logger)
        {
            ms_mgau_model_t msg = (ms_mgau_model_t)mg;
            int gid;
            int topn;
            int best;
            gauden_t g;
            senone_t sen;

            topn = ms_mgau_topn(msg);
            g = ms_mgau_gauden(msg);
            sen = ms_mgau_senone(msg);

            if (compallsen != 0)
            {
                int s;

                for (gid = 0; gid < g.n_mgau; gid++)
                    MultiStreamGaussDensity.gauden_dist(g, gid, topn, feats, msg.dist[gid]);

                best = (int)0x7fffffff;
                for (s = 0; s < sen.n_sen; s++)
                {
                    senscr[s] = checked((short)(MultiStreamSenone.senone_eval(sen, s, msg.dist[sen.mgau[s]], topn)));
                    if (best > senscr[s])
                    {
                        best = senscr[s];
                    }
                }

                /* Normalize senone scores */
                for (s = 0; s < sen.n_sen; s++)
                {
                    int bs = senscr[s] - best;
                    if (bs > 32767)
                        bs = 32767;
                    if (bs < -32768)
                        bs = -32768;
                    senscr[s] = checked((short)bs);
                }
            }
            else
            {
                int i, n;
                /* Flag all active mixture-gaussian codebooks */
                for (gid = 0; gid < g.n_mgau; gid++)
                    msg.mgau_active[gid] = 0;

                n = 0;
                for (i = 0; i < n_senone_active; i++)
                {
                    /* senone_active consists of deltas. */
                    int s = senone_active[i] + n;
                    msg.mgau_active[sen.mgau[s]] = 1;
                    n = s;
                }

                /* Compute topn gaussian density values (for active codebooks) */
                for (gid = 0; gid < g.n_mgau; gid++)
                {
                    if (msg.mgau_active[gid] != 0)
                        MultiStreamGaussDensity.gauden_dist(g, gid, topn, feats, msg.dist[gid]);
                }

                best = (int)0x7fffffff;
                n = 0;
                for (i = 0; i < n_senone_active; i++)
                {
                    int s = senone_active[i] + n;
                    senscr[s] = checked((short)MultiStreamSenone.senone_eval(sen, s, msg.dist[sen.mgau[s]], topn));
                    if (best > senscr[s])
                    {
                        best = senscr[s];
                    }
                    n = s;
                }

                /* Normalize senone scores */
                n = 0;
                for (i = 0; i < n_senone_active; i++)
                {
                    int s = senone_active[i] + n;
                    int bs = senscr[s] - best;
                    if (bs > 32767)
                        bs = 32767;
                    if (bs < -32768)
                        bs = -32768;
                    senscr[s] = checked((short)bs);
                    n = s;
                }
            }

            return 0;
        }
    }
}
