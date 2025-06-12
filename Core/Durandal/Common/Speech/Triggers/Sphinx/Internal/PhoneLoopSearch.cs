using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class PhoneLoopSearch
    {
        internal static readonly ps_searchfuncs_t phone_loop_search_funcs = new ps_searchfuncs_t()
        {
            start = phone_loop_search_start,
            step = phone_loop_search_step,
            finish = phone_loop_search_finish,
            reinit = phone_loop_search_reinit,
            hyp = phone_loop_search_hyp,
            prob = phone_loop_search_prob,
            seg_iter = phone_loop_search_seg_iter,
        };

        internal static int phone_loop_search_reinit(ps_search_t search, dict_t dict, dict2pid_t d2p)
        {
            phone_loop_search_t pls = (phone_loop_search_t)search;
            cmd_ln_t config = PocketSphinx.ps_search_config(search);
            acmod_t acmod = PocketSphinx.ps_searchacmod(search);
            int i;

            /* Free old dict2pid, dict, if necessary. */
            PocketSphinx.ps_search_base_reinit(search, dict, d2p);

            /* Initialize HMM context. */
            pls.hmmctx = HiddenMarkovModel.hmm_context_init(BinaryModelDef.bin_mdef_n_emit_state(acmod.mdef), acmod.tmat.tp, null, acmod.mdef.sseq, search.logger);
            if (pls.hmmctx == null)
                return -1;

            /* Initialize penalty storage */
            pls.n_phones = checked((short)BinaryModelDef.bin_mdef_n_ciphone(acmod.mdef));
            pls.window = (int)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-pl_window"));
            pls.penalties = CKDAlloc.ckd_calloc<int>(pls.n_phones);
            pls.pen_buf = CKDAlloc.ckd_calloc_2d<int>((uint)pls.window, (uint)pls.n_phones);

            /* Initialize phone HMMs. */
            pls.hmms = new hmm_t[pls.n_phones];
            for (i = 0; i < pls.n_phones; ++i)
            {
                pls.hmms[i] = new hmm_t();
                HiddenMarkovModel.hmm_init(pls.hmmctx, pls.hmms[i],
                         0,
                         BinaryModelDef.bin_mdef_pid2ssid(acmod.mdef, i),
                         BinaryModelDef.bin_mdef_pid2tmatid(acmod.mdef, i));
            }
            pls.penalty_weight = CommandLine.cmd_ln_float_r(config, cstring.ToCString("-pl_weight"));
            pls.beam = LogMath.logmath_log(acmod.lmath, CommandLine.cmd_ln_float_r(config, cstring.ToCString("-pl_beam"))) >> HiddenMarkovModel.SENSCR_SHIFT;
            pls.pbeam = LogMath.logmath_log(acmod.lmath, CommandLine.cmd_ln_float_r(config, cstring.ToCString("-pl_pbeam"))) >> HiddenMarkovModel.SENSCR_SHIFT;
            pls.pip = LogMath.logmath_log(acmod.lmath, CommandLine.cmd_ln_float_r(config, cstring.ToCString("-pl_pip"))) >> HiddenMarkovModel.SENSCR_SHIFT;
            pls.logger.E_INFO(string.Format("State beam {0} Phone exit beam {1} Insertion penalty {2}\n",
                   pls.beam, pls.pbeam, pls.pip));

            return 0;
        }

        internal static ps_search_t phone_loop_search_init(
            cmd_ln_t config,
                       acmod_t acmod,
                       dict_t dict,
                       SphinxLogger logger)
        {
            phone_loop_search_t pls = new phone_loop_search_t();
            pls.logger = logger;
            PocketSphinx.ps_search_init((ps_search_t)pls, phone_loop_search_funcs,
                   PocketSphinx.PS_SEARCH_TYPE_PHONE_LOOP, PocketSphinx.PS_DEFAULT_PL_SEARCH,
                           config, acmod, dict, null);
            phone_loop_search_reinit((ps_search_t)pls, pls.dict, pls.d2p);

            return (ps_search_t)pls;
        }

        internal static int phone_loop_search_start(ps_search_t search)
        {
            phone_loop_search_t pls = (phone_loop_search_t)search;
            int i;

            /* Reset and enter all phone HMMs. */
            for (i = 0; i < pls.n_phones; ++i)
            {
                hmm_t hmmModel = pls.hmms[i];
                HiddenMarkovModel.hmm_clear(hmmModel);
                HiddenMarkovModel.hmm_enter(hmmModel, 0, -1, 0);
            }

            pls.penalties.MemSet(0, pls.n_phones);
            for (i = 0; i < pls.window; i++)
                pls.pen_buf[i].MemSet(0, pls.n_phones);

            pls.best_score = 0;
            pls.pen_buf_ptr = 0;

            return 0;
        }

        internal static void renormalize_hmms(phone_loop_search_t pls, int frame_idx, int norm)
        {
            phone_loop_renorm_t rn = new phone_loop_renorm_t();
            int i;

            pls.renorm = GenericList.glist_add_ptr(pls.renorm, rn);
            rn.frame_idx = frame_idx;
            rn.norm = norm;
            
            for (i = 0; i < pls.n_phones; ++i)
            {
                HiddenMarkovModel.hmm_normalize(pls.hmms[i], norm);
            }
        }

        internal static void evaluate_hmms(phone_loop_search_t pls, short[] senscr, int frame_idx)
        {
            int bs = HiddenMarkovModel.WORST_SCORE;
            int i;

            HiddenMarkovModel.hmm_context_set_senscore(pls.hmmctx, senscr);

            for (i = 0; i < pls.n_phones; ++i)
            {
                hmm_t hmmodel = pls.hmms[i];
                int score;

                if (HiddenMarkovModel.hmm_frame(hmmodel) < frame_idx)
                    continue;
                score = HiddenMarkovModel.hmm_vit_eval(hmmodel);
                if (score > bs)
                {
                    bs = score;
                }
            }

            pls.best_score = bs;
        }

        internal static void store_scores(phone_loop_search_t pls, int frame_idx)
        {
            int i, j, itr;

            for (i = 0; i < pls.n_phones; ++i)
            {
                hmm_t hmModel = pls.hmms[i];
                pls.pen_buf[pls.pen_buf_ptr].Set(i, (int)((HiddenMarkovModel.hmm_bestscore(hmModel) - pls.best_score) * pls.penalty_weight)); // LOGAN Check wtf is with the implicit cast here?
            }
            pls.pen_buf_ptr++;
            pls.pen_buf_ptr = checked((short)(pls.pen_buf_ptr % pls.window));

            /* update penalties */
            for (i = 0; i < pls.n_phones; ++i)
            {
                pls.penalties[i] = HiddenMarkovModel.WORST_SCORE;
                for (j = 0, itr = pls.pen_buf_ptr + 1; j < pls.window; j++, itr++)
                {
                    itr = itr % pls.window;
                    if (pls.pen_buf[itr][i] > pls.penalties[i])
                        pls.penalties[i] = pls.pen_buf[itr][i];
                }
            }
        }

        internal static void prune_hmms(phone_loop_search_t pls, int frame_idx)
        {
            int thresh = pls.best_score + pls.beam;
            int nf = frame_idx + 1;
            int i;

            /* Check all phones to see if they remain active in the next frame. */
            for (i = 0; i < pls.n_phones; ++i)
            {
                hmm_t hmModel = pls.hmms[i];

                if (HiddenMarkovModel.hmm_frame(hmModel) < frame_idx)
                    continue;
                /* Retain if score better than threshold. */
                if (HiddenMarkovModel.hmm_bestscore(hmModel) > thresh)
                {
                    hmModel.frame = nf;
                }
                else
                {
                    HiddenMarkovModel.hmm_clear_scores(hmModel);
                }
            }
        }

        internal static void phone_transition(phone_loop_search_t pls, int frame_idx)
        {
            int thresh = pls.best_score + pls.pbeam;
            int nf = frame_idx + 1;
            int i;

            /* Now transition out of phones whose last states are inside the
             * phone transition beam. */
            for (i = 0; i < pls.n_phones; ++i)
            {
                hmm_t hmModel = pls.hmms[i];
                int newphone_score;
                int j;

                if (HiddenMarkovModel.hmm_frame(hmModel) != nf)
                    continue;

                newphone_score = HiddenMarkovModel.hmm_out_score(hmModel) + pls.pip;
                if (newphone_score > thresh)
                {
                    /* Transition into all phones using the usual Viterbi rule. */
                    for (j = 0; j < pls.n_phones; ++j)
                    {
                        hmm_t nhmModel = pls.hmms[j];

                        if (HiddenMarkovModel.hmm_frame(nhmModel) < frame_idx || newphone_score > HiddenMarkovModel.hmm_in_score(nhmModel))
                        {
                            HiddenMarkovModel.hmm_enter(nhmModel, newphone_score, HiddenMarkovModel.hmm_out_history(hmModel), nf);
                        }
                    }
                }
            }
        }

        internal static int phone_loop_search_step(ps_search_t search, int frame_idx)
        {
            phone_loop_search_t pls = (phone_loop_search_t)search;
            acmod_t acModel = PocketSphinx.ps_searchacmod(search);
            short[] senscr;
            int i;

            /* All CI senones are active all the time. */
            if (PocketSphinx.ps_searchacmod(pls).compallsen == 0)
            {
                AcousticModel.acmod_clear_active(PocketSphinx.ps_searchacmod(pls));
                for (i = 0; i < pls.n_phones; ++i)
                    AcousticModel.acmod_activate_hmm(acModel, pls.hmms[i]);
            }

            /* Calculate senone scores for current frame. */
            BoxedValueInt boxed_frame_idx = new BoxedValueInt(frame_idx);
            senscr = AcousticModel.acmod_score(acModel, boxed_frame_idx);
            frame_idx = boxed_frame_idx.Val;

            /* Renormalize, if necessary. */
            if (pls.best_score + (2 * pls.beam) < HiddenMarkovModel.WORST_SCORE)
            {
                pls.logger.E_INFO(string.Format("Renormalizing Scores at frame {0}, best score {1}\n",
                       frame_idx, pls.best_score));
                renormalize_hmms(pls, frame_idx, pls.best_score);
            }

            /* Evaluate phone HMMs for current frame. */
            evaluate_hmms(pls, senscr, frame_idx);

            /* Store hmm scores for senone penaly calculation */
            store_scores(pls, frame_idx);

            /* Prune phone HMMs. */
            prune_hmms(pls, frame_idx);

            /* Do phone transitions. */
            phone_transition(pls, frame_idx);

            return 0;
        }

        internal static int phone_loop_search_finish(ps_search_t search)
        {
            /* Actually nothing to do here really. */
            return 0;
        }

        internal static Pointer<byte> phone_loop_search_hyp(ps_search_t search, BoxedValueInt out_score)
        {
            search.logger.E_WARN("Hypotheses are not returned from phone loop search");
            return PointerHelpers.NULL<byte>();
        }

        internal static int phone_loop_search_prob(ps_search_t search)
        {
            /* FIXME: Actually... they ought to be. */
            search.logger.E_WARN("Posterior probabilities are not returned from phone loop search");
            return 0;
        }

        internal static ps_seg_t phone_loop_search_seg_iter(ps_search_t search)
        {
            search.logger.E_WARN("Hypotheses are not returned from phone loop search");
            return null;
        }
    }
}
