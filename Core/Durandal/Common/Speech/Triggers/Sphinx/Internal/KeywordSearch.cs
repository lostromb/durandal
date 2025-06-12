using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class KeywordSearch
    {
        public const int KWS_MAX = 1500;
        
        internal static int kws_search_prob(ps_search_t search)
        {
            return 0;
        }

        internal static void kws_seg_free(ps_seg_t seg)
        {
        }

        internal static int hmm_is_active(hmm_t _hmm)
        {
            return _hmm.frame > 0 ? 1 : 0;
        }

        internal static hmm_t kws_nth_hmm(kws_keyphrase_t keyphrase, int n)
        {
            return keyphrase.hmms[n];
        }

        internal static void kws_seg_fill(kws_seg_t itor)
        {
            kws_detection_t detection = (kws_detection_t)GenericList.gnode_ptr(itor.detection);

            itor.word = detection.keyphrase;
            itor.sf = detection.sf;
            itor.ef = detection.ef;
            itor.prob = detection.prob;
            itor.ascr = detection.ascr;
            itor.lscr = 0;
        }

        internal static ps_seg_t kws_seg_next(ps_seg_t seg)
        {
            kws_seg_t itor = (kws_seg_t)seg;

            Pointer<gnode_t> detect_head = GenericList.gnode_next(itor.detection);
            while (detect_head.IsNonNull && ((kws_detection_t)GenericList.gnode_ptr(detect_head)).ef > itor.last_frame)
                detect_head = GenericList.gnode_next(detect_head);
            itor.detection = detect_head;

            if (itor.detection.IsNull)
            {
                kws_seg_free(seg);
                return null;
            }

            kws_seg_fill(itor);

            return seg;
        }

        internal static ps_segfuncs_t kws_segfuncs = new ps_segfuncs_t()
        {
            seg_next = kws_seg_next,
            seg_free = kws_seg_free
        };

        internal static ps_seg_t kws_search_seg_iter(ps_search_t search)
        {
            kws_search_t kwss = (kws_search_t)search;
            kws_seg_t itor;
            Pointer<gnode_t> detect_head = kwss.detections.detect_list;

            while (detect_head.IsNonNull && ((kws_detection_t)GenericList.gnode_ptr(detect_head)).ef > kwss.frame - kwss.delay)
                detect_head = GenericList.gnode_next(detect_head);

            if (detect_head.IsNull)
                return null;

            itor = new kws_seg_t();
            itor.vt = kws_segfuncs;
            itor.search = search;
            itor.lwf = 1.0f;
            itor.detection = detect_head;
            itor.last_frame = kwss.frame - kwss.delay;
            kws_seg_fill(itor);
            return (ps_seg_t)itor;
        }

        internal static ps_searchfuncs_t kws_funcs = new ps_searchfuncs_t()
        {
            start = kws_search_start,
            step = kws_search_step,
            finish = kws_search_finish,
            reinit = kws_search_reinit,
            hyp = kws_search_hyp,
            prob = kws_search_prob,
            seg_iter = kws_search_seg_iter,
        };


        /* Activate senones for scoring */
        internal static void kws_search_sen_active(kws_search_t kwss)
        {
            int i;

            AcousticModel.acmod_clear_active(PocketSphinx.ps_searchacmod(kwss));

            /* active phone loop hmms */
            for (i = 0; i < kwss.n_pl; i++)
                AcousticModel.acmod_activate_hmm(PocketSphinx.ps_searchacmod(kwss), kwss.pl_hmms[i]);

            /* activate hmms in active nodes */
            foreach (kws_keyphrase_t keyphrase in kwss.keyphrases)
            {
                for (i = 0; i < keyphrase.n_hmms; i++)
                {
                    if (hmm_is_active(kws_nth_hmm(keyphrase, i)) != 0)
                        AcousticModel.acmod_activate_hmm(PocketSphinx.ps_searchacmod(kwss), kws_nth_hmm(keyphrase, i));
                }
            }
        }

        /*
        * Evaluate all the active HMMs.
        * (Executed once per frame.)
        */
        internal static void kws_search_hmm_eval(kws_search_t kwss, short[] senscr)
        {
            int i;
            int bestscore = HiddenMarkovModel.WORST_SCORE;

            HiddenMarkovModel.hmm_context_set_senscore(kwss.hmmctx, senscr);

            /* evaluate hmms from phone loop */
            for (i = 0; i < kwss.n_pl; ++i)
            {
                hmm_t _hmm = kwss.pl_hmms[i];
                int score;

                score = HiddenMarkovModel.hmm_vit_eval(_hmm);
                if (score > bestscore)
                    bestscore = score;
            }
            /* evaluate hmms for active nodes */
            foreach (kws_keyphrase_t keyphrase in kwss.keyphrases)
            {
                for (i = 0; i < keyphrase.n_hmms; i++)
                {
                    hmm_t _hmm = kws_nth_hmm(keyphrase, i);

                    if (hmm_is_active(_hmm) != 0)
                    {
                        int score;
                        score = HiddenMarkovModel.hmm_vit_eval(_hmm);
                        //Console.Write("HMM Eval {0} Score {1}\n", cstring.FromCString(keyphrase.word), score);
                        if (score > bestscore)
                            bestscore = score;
                    }
                }
            }

            kwss.bestscore = bestscore;
        }

        /*
        * (Beam) prune the just evaluated HMMs, determine which ones remain
        * active. Executed once per frame.
        */
        internal static void kws_search_hmm_prune(kws_search_t kwss)
        {
            int thresh, i;

            thresh = kwss.bestscore + kwss.beam;

            foreach (kws_keyphrase_t keyphrase in kwss.keyphrases)
            {
                for (i = 0; i < keyphrase.n_hmms; i++)
                {
                    hmm_t _hmm = kws_nth_hmm(keyphrase, i);
                    if (hmm_is_active(_hmm) != 0 && HiddenMarkovModel.hmm_bestscore(_hmm) < thresh)
                        HiddenMarkovModel.hmm_clear(_hmm);
                }
            }
        }


        /**
        * Do phone transitions
*/
        internal static void kws_search_trans(kws_search_t kwss)
        {
            hmm_t pl_best_hmm = null;
            int best_out_score = HiddenMarkovModel.WORST_SCORE;
            int i;

            /* select best hmm in phone-loop to be a predecessor */
            for (i = 0; i < kwss.n_pl; i++)
                if (HiddenMarkovModel.hmm_out_score(kwss.pl_hmms[i]) > best_out_score)
                {
                    best_out_score = HiddenMarkovModel.hmm_out_score(kwss.pl_hmms[i]);
                    pl_best_hmm = kwss.pl_hmms[i];
                }

            /* out probs are not ready yet */
            if (pl_best_hmm == null)
                return;

            /* Check whether keyphrase wasn't spotted yet */
            foreach (kws_keyphrase_t keyphrase in kwss.keyphrases)
            {
                hmm_t last_hmm;

                if (keyphrase.n_hmms < 1)
                    continue;

                last_hmm = kws_nth_hmm(keyphrase, keyphrase.n_hmms - 1);

                if (hmm_is_active(last_hmm) != 0
                    && HiddenMarkovModel.hmm_out_score(pl_best_hmm) > HiddenMarkovModel.WORST_SCORE)
                {

                    if (HiddenMarkovModel.hmm_out_score(last_hmm) - HiddenMarkovModel.hmm_out_score(pl_best_hmm)
                        >= keyphrase.threshold)
                    {

                        int prob = HiddenMarkovModel.hmm_out_score(last_hmm) - HiddenMarkovModel.hmm_out_score(pl_best_hmm) - KWS_MAX;
                        KeywordSearchDetections.kws_detections_add(kwss.detections, keyphrase.word,
                                          HiddenMarkovModel.hmm_out_history(last_hmm),
                                          kwss.frame, prob,
                                          HiddenMarkovModel.hmm_out_score(last_hmm));
                    } /* keyphrase is spotted */
                } /* last hmm of keyphrase is active */
            } /* keyphrase loop */

            /* Make transition for all phone loop hmms */
            for (i = 0; i < kwss.n_pl; i++)
            {
                if (HiddenMarkovModel.hmm_out_score(pl_best_hmm) + kwss.plp >
                    HiddenMarkovModel.hmm_in_score(kwss.pl_hmms[i]))
                {
                    HiddenMarkovModel.hmm_enter(kwss.pl_hmms[i],
                              HiddenMarkovModel.hmm_out_score(pl_best_hmm) + kwss.plp,
                              HiddenMarkovModel.hmm_out_history(pl_best_hmm), kwss.frame + 1);
                }
            }

            /* Activate new keyphrase nodes, enter their hmms */
            foreach (kws_keyphrase_t keyphrase in kwss.keyphrases)
            {
                if (keyphrase.n_hmms < 1)
                    continue;

                for (i = keyphrase.n_hmms - 1; i > 0; i--)
                {
                    hmm_t pred_hmm = kws_nth_hmm(keyphrase, i - 1);
                    hmm_t _hmm = kws_nth_hmm(keyphrase, i);

                    if (hmm_is_active(pred_hmm) != 0)
                    {
                        if (hmm_is_active(_hmm) == 0
                            || HiddenMarkovModel.hmm_out_score(pred_hmm) >
                            HiddenMarkovModel.hmm_in_score(_hmm))
                            HiddenMarkovModel.hmm_enter(_hmm, HiddenMarkovModel.hmm_out_score(pred_hmm),
                                      HiddenMarkovModel.hmm_out_history(pred_hmm), kwss.frame + 1);
                    }
                }

                /* Enter keyphrase start node from phone loop */
                if (HiddenMarkovModel.hmm_out_score(pl_best_hmm) >
                    HiddenMarkovModel.hmm_in_score(kws_nth_hmm(keyphrase, 0)))
                    HiddenMarkovModel.hmm_enter(kws_nth_hmm(keyphrase, 0), HiddenMarkovModel.hmm_out_score(pl_best_hmm),
                        kwss.frame, kwss.frame + 1);
            }
        }

        internal static int kws_search_read_list(kws_search_t kwss, Pointer<byte> keyfile)
        {
            // LOGAN modified this whole routine
            // It's easier in C#, honestly
            string keyFile = cstring.FromCString(keyfile);
            string[] lines = keyFile.Split('\n');
            int n_keyphrases = lines.Length;
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split('/');
                string word = parts[0];
                string thresh = parts[1];
                double parsedThresh = StringFuncs.atof_c(cstring.ToCString(thresh));
                kws_keyphrase_t keyphrase = new kws_keyphrase_t();
                keyphrase.threshold = LogMath.logmath_log(kwss.acmod.lmath, parsedThresh) >> HiddenMarkovModel.SENSCR_SHIFT;
                keyphrase.word = cstring.ToCString(word);
                kwss.keyphrases.Add(keyphrase);
            }

            return 0;
        }

        internal static ps_search_t kws_search_init(Pointer<byte> name,
                        Pointer<byte> keyphrase,
                        Pointer<byte> keyfile,
                        cmd_ln_t config,
                        acmod_t acmod,
                        dict_t dict,
                        dict2pid_t d2p,
                        SphinxLogger logger)
        {
            kws_search_t kwss = new kws_search_t();
            kwss.logger = logger;
            PocketSphinx.ps_search_init(kwss, kws_funcs, PocketSphinx.PS_SEARCH_TYPE_KWS, name, config, acmod, dict, d2p);

            kwss.detections = new kws_detections_t();

            kwss.beam =
                (int)LogMath.logmath_log(acmod.lmath,
                                    CommandLine.cmd_ln_float_r(config,
                                                     cstring.ToCString("-beam"))) >> HiddenMarkovModel.SENSCR_SHIFT;

            kwss.plp =
                (int)LogMath.logmath_log(acmod.lmath,
                                    CommandLine.cmd_ln_float_r(config,
                                                     cstring.ToCString("-kws_plp"))) >> HiddenMarkovModel.SENSCR_SHIFT;


            kwss.def_threshold =
                (int)LogMath.logmath_log(acmod.lmath,
                                    CommandLine.cmd_ln_float_r(config,
                                                     cstring.ToCString("-kws_threshold"))) >>
                HiddenMarkovModel.SENSCR_SHIFT;

            kwss.delay = (int)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-kws_delay"));
            kwss.keyphrases = new List<kws_keyphrase_t>();
            logger.E_INFO(string.Format("KWS(beam: {0}, plp: {1}, default threshold {2}, delay {3})\n",
                   kwss.beam, kwss.plp, kwss.def_threshold, kwss.delay));

            if (keyfile.IsNonNull)
            {
                if (kws_search_read_list(kwss, keyfile) < 0)
                {
                    logger.E_ERROR("Failed to create kws search\n");
                    kws_search_free(kwss);
                    return null;
                }
            }
            else
            {
                kws_keyphrase_t k = new kws_keyphrase_t();
                k.threshold = kwss.def_threshold;
                k.word = CKDAlloc.ckd_salloc(keyphrase);
                kwss.keyphrases.Add(k);
            }

            /* Reinit for provided keyphrase */
            if (kws_search_reinit(
                kwss,
                PocketSphinx.ps_search_dict(kwss),
                PocketSphinx.ps_search_dict2pid(kwss)) < 0)
            {
                return null;
            }

            Profiler.ptmr_init(kwss.perf);

            return kwss;
        }

        internal static void kws_search_free(ps_search_t search)
        {
            kws_search_t kwss;
            double n_speech;

            kwss = (kws_search_t)search;

            n_speech = (double)kwss.n_tot_frame / CommandLine.cmd_ln_int_r(PocketSphinx.ps_search_config(kwss), cstring.ToCString("-frate"));

            // LOGAN reimplement this
            //Logger.E_INFO("TOTAL kws %.2f CPU %.3f xRT\n",
            //       kwss.perf.t_tot_cpu,
            //       kwss.perf.t_tot_cpu / n_speech);
            //Logger.E_INFO("TOTAL kws %.2f wall %.3f xRT\n",
            //       kwss.perf.t_tot_elapsed,
            //       kwss.perf.t_tot_elapsed / n_speech);
            
            KeywordSearchDetections.kws_detections_reset(kwss.detections);
        }

        internal static int kws_search_reinit(ps_search_t search, dict_t dictionary, dict2pid_t d2p)
        {
            Pointer<Pointer<byte>> wrdptr;
            Pointer<byte> tmp_keyphrase;
            int wid, pronlen, in_dict;
            int n_hmms, n_wrds;
            int ssid, tmatid;
            int i, j, p;
            kws_search_t kwss = (kws_search_t)search;
            bin_mdef_t mdef = search.acmod.mdef;
            int silcipid = BinaryModelDef.bin_mdef_silphone(mdef);

            /* Free old dict2pid, dict */
            PocketSphinx.ps_search_base_reinit(search, dictionary, d2p);

            /* Initialize HMM context. */
            kwss.hmmctx =
                HiddenMarkovModel.hmm_context_init(BinaryModelDef.bin_mdef_n_emit_state(search.acmod.mdef),
                                 search.acmod.tmat.tp,
                                 null,
                                 search.acmod.mdef.sseq,
                                 search.logger);
            if (kwss.hmmctx == null)
                return -1;

            /* Initialize phone loop HMMs. */
            kwss.n_pl = BinaryModelDef.bin_mdef_n_ciphone(search.acmod.mdef);
            kwss.pl_hmms = new hmm_t[kwss.n_pl];
            for (i = 0; i < kwss.n_pl; ++i)
            {
                kwss.pl_hmms[i] = new hmm_t();
                HiddenMarkovModel.hmm_init(kwss.hmmctx,
                    kwss.pl_hmms[i],
                    0,
                    BinaryModelDef.bin_mdef_pid2ssid(search.acmod.mdef, i),
                    BinaryModelDef.bin_mdef_pid2tmatid(search.acmod.mdef, i));
            }

            foreach (kws_keyphrase_t keyphrase in kwss.keyphrases)
            {
                /* Initialize keyphrase HMMs */
                tmp_keyphrase = CKDAlloc.ckd_salloc(keyphrase.word);
                n_wrds = StringFuncs.str2words(tmp_keyphrase, PointerHelpers.NULL<Pointer<byte>>(), 0);
                wrdptr = CKDAlloc.ckd_calloc<Pointer<byte>>(n_wrds);
                StringFuncs.str2words(tmp_keyphrase, wrdptr, n_wrds);

                /* count amount of hmms */
                n_hmms = 0;
                in_dict = 1;
                for (i = 0; i < n_wrds; i++)
                {
                    wid = Dictionary.dict_wordid(dictionary, wrdptr[i]);
                    if (wid == SphinxTypes.BAD_S3WID)
                    {
                        kwss.logger.E_ERROR(string.Format("Word '{0}' in phrase '{1}' is missing in the dictionary\n", cstring.FromCString(wrdptr[i]), cstring.FromCString(keyphrase.word)));
                        in_dict = 0;
                        break;
                    }
                    pronlen = Dictionary.dict_pronlen(dictionary, wid);
                    n_hmms += pronlen;
                }

                if (in_dict == 0)
                {
                    continue;
                }

                /* allocate node array */
                keyphrase.hmms = new hmm_t[n_hmms];
                keyphrase.n_hmms = n_hmms;

                /* fill node array */
                j = 0;
                for (i = 0; i < n_wrds; i++)
                {
                    wid = Dictionary.dict_wordid(dictionary, wrdptr[i]);
                    pronlen = Dictionary.dict_pronlen(dictionary, wid);
                    for (p = 0; p < pronlen; p++)
                    {
                        int ci = Dictionary.dict_pron(dictionary, wid, p);
                        if (p == 0)
                        {
                            /* first phone of word */
                            int rc =
                                pronlen > 1 ? Dictionary.dict_pron(dictionary, wid, 1) : silcipid;
                            ssid = d2p.ldiph_lc[ci][rc][silcipid];
                        }
                        else if (p == pronlen - 1)
                        {
                            /* last phone of the word */
                            int lc = Dictionary.dict_pron(dictionary, wid, p - 1);
                            Pointer<xwdssid_t> rssid = d2p.rssid[ci].Point(lc);
                            int jjj = rssid.Deref.cimap[silcipid];
                            ssid = rssid.Deref.ssid[jjj]; // LOGAN WTF? Why does C allow you to declare the same variable twice in nested scopes? TODO file a bug on sphinx
                        }
                        else
                        {
                            /* word internal phone */
                            ssid = DictToPID.dict2pid_internal(d2p, wid, p);
                        }
                        tmatid = BinaryModelDef.bin_mdef_pid2tmatid(mdef, ci);
                        keyphrase.hmms[j] = new hmm_t();
                        HiddenMarkovModel.hmm_init(kwss.hmmctx, keyphrase.hmms[j], 0, ssid, tmatid);
                        j++;
                    }
                }
            }


            return 0;
        }

        internal static int kws_search_start(ps_search_t search)
        {
            int i;
            kws_search_t kwss = (kws_search_t)search;

            kwss.frame = 0;
            kwss.bestscore = 0;
            KeywordSearchDetections.kws_detections_reset(kwss.detections);

            /* Reset and enter all phone-loop HMMs. */
            for (i = 0; i < kwss.n_pl; ++i)
            {
                hmm_t _hmm = kwss.pl_hmms[i];
                HiddenMarkovModel.hmm_clear(_hmm);
                HiddenMarkovModel.hmm_enter(_hmm, 0, -1, 0);
            }

            Profiler.ptmr_reset(kwss.perf);
            Profiler.ptmr_start(kwss.perf);

            return 0;
        }

        internal static int kws_search_step(ps_search_t search, int frame_idx)
        {
            short[] senscr;
            kws_search_t kwss = (kws_search_t)search;
            acmod_t acmod = search.acmod;

            /* Activate senones */
            if (acmod.compallsen == 0)
                kws_search_sen_active(kwss);

            /* Calculate senone scores for current frame. */
            BoxedValueInt boxed_frame_idx = new BoxedValueInt(frame_idx);
            senscr = AcousticModel.acmod_score(acmod, boxed_frame_idx);
            frame_idx = boxed_frame_idx.Val;

            /* Evaluate hmms in phone loop and in active keyphrase nodes */
            kws_search_hmm_eval(kwss, senscr);

            /* Prune hmms with low prob */
            kws_search_hmm_prune(kwss);

            /* Do hmms transitions */
            kws_search_trans(kwss);

            ++kwss.frame;
            return 0;
        }

        internal static int kws_search_finish(ps_search_t search)
        {
            kws_search_t kwss;
            int cf;

            kwss = (kws_search_t)search;

            kwss.n_tot_frame += kwss.frame;

            /* Print out some statistics. */
            Profiler.ptmr_stop(kwss.perf);
            /* This is the number of frames processed. */
            cf = PocketSphinx.ps_searchacmod(kwss).output_frame;
            if (cf > 0)
            {
                double n_speech = (double)(cf + 1) / CommandLine.cmd_ln_int_r(PocketSphinx.ps_search_config(kwss), cstring.ToCString("-frate"));
                // LOGAN reimplement this
                //Logger.E_INFO("kws %.2f CPU %.3f xRT\n",
                //       kwss.perf.t_cpu, kwss.perf.t_cpu / n_speech);
                //Logger.E_INFO("kws %.2f wall %.3f xRT\n",
                //       kwss.perf.t_elapsed, kwss.perf.t_elapsed / n_speech);
            }

            return 0;
        }

        internal static Pointer<byte> kws_search_hyp(ps_search_t search, BoxedValueInt out_score)
        {
            kws_search_t kwss = (kws_search_t)search;
            if (out_score != null)
                out_score.Val = 0;
            
            search.hyp_str = KeywordSearchDetections.kws_detections_hyp_str(kwss.detections, kwss.frame, kwss.delay);

            return search.hyp_str;
        }

        internal static Pointer<byte> kws_search_get_keyphrases(ps_search_t search)
        {
            int c, len;
            kws_search_t kwss;
            Pointer<byte> line;

            kwss = (kws_search_t)search;

            len = 0;
            foreach (kws_keyphrase_t keyphrase in kwss.keyphrases)
                len += (int)cstring.strlen(keyphrase.word) + 1;

            c = 0;
            line = CKDAlloc.ckd_calloc<byte>(len);
            foreach (kws_keyphrase_t keyphrase in kwss.keyphrases)
            {
                Pointer<byte> str = keyphrase.word;
                str.MemCopyTo(line.Point(c), (int)cstring.strlen(str));
                c += (int)cstring.strlen(str);
                line[c++] = (byte)'\n';
            }
            line[--c] = (byte)'\0';

            return line;
        }
    }
}
