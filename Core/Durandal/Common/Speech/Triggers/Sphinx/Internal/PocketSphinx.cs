#region License

/* ====================================================================
 * Copyright (c) 1999-2018 Carnegie Mellon University.  All rights
 * reserved.
 * Ported to C# by Logan Stromberg
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer. 
 *
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in
 *    the documentation and/or other materials provided with the
 *    distribution.
 *
 * This work was supported in part by funding from the Defense Advanced 
 * Research Projects Agency and the National Science Foundation of the 
 * United States of America, and the CMU Sphinx Speech Consortium.
 *
 * THIS SOFTWARE IS PROVIDED BY CARNEGIE MELLON UNIVERSITY ``AS IS'' AND 
 * ANY EXPRESSED OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, 
 * THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
 * PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL CARNEGIE MELLON UNIVERSITY
 * NOR ITS EMPLOYEES BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT 
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY 
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT 
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *
 * ====================================================================
 **/

#endregion

using Durandal.Common.Collections;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal class PocketSphinx
    {
        /* Model parameters and such. */
        public cmd_ln_t config;  /*< Configuration. */

        /* Basic units of computation. */
        public acmod_t acmod;    /*< Acoustic model. */
        public dict_t dict;    /*< Pronunciation dictionary. */
        public dict2pid_t d2p;   /*< Dictionary to senone mapping. */
        public logmath_t lmath;  /*< Log math computation. */

        /* Search modules. */
        public hash_table_t searches;        /*< Set of search modules. */
        /* TODO: Convert this to a stack of searches each with their own
         * lookahead value. */
        public ps_search_t search;     /*< Currently active search module. */
        public ps_search_t phone_loop; /*< Phone loop search for lookahead. */
        public int pl_window;           /*< Window size for phoneme lookahead. */

        /* Utterance-processing related stuff. */
        public uint uttno;       /*< Utterance counter. */
        public ptmr_t perf;        /*< Performance counter for all of decoding. */
        public uint n_frame;     /*< Total number of frames processed. */
        public SphinxLogger logger;

        private PocketSphinx()
        {
            perf = new ptmr_t();
        }

        /* Search names*/
        internal static readonly Pointer<byte> PS_DEFAULT_SEARCH = cstring.ToCString("_default");
        internal static readonly Pointer<byte> PS_DEFAULT_PL_SEARCH = cstring.ToCString("_default_pl");

        /* Search types */
        internal static readonly Pointer<byte> PS_SEARCH_TYPE_KWS = cstring.ToCString("kws");
        internal static readonly Pointer<byte> PS_SEARCH_TYPE_FSG = cstring.ToCString("fsg");
        internal static readonly Pointer<byte> PS_SEARCH_TYPE_NGRAM = cstring.ToCString("ngram");
        internal static readonly Pointer<byte> PS_SEARCH_TYPE_ALLPHONE = cstring.ToCString("allphone");
        internal static readonly Pointer<byte> PS_SEARCH_TYPE_STATE_ALIGN = cstring.ToCString("state_align");
        internal static readonly Pointer<byte> PS_SEARCH_TYPE_PHONE_LOOP = cstring.ToCString("phone_loop");

        internal static Pointer<arg_t> ps_args()
        {
            return ps_args_def();
        }

        internal static Pointer<arg_t> ps_args_def()
        {
            List<arg_t> args = CommandLineMacro.POCKETSPHINX_OPTIONS();
            return new Pointer<arg_t>(args.ToArray());
        }

        internal static Pointer<arg_t> feat_defn()
        {
            List<arg_t> args = new List<arg_t>();
            args.FastAddRangeList(CommandLineMacro.waveform_to_cepstral_command_line_macro());
            args.FastAddRangeList(CommandLineMacro.cepstral_to_feature_command_line_macro());
            args.Add(null);
            return new Pointer<arg_t>(args.ToArray());
        }

        internal static PocketSphinx ps_init(cmd_ln_t config, FileAdapter fileAdapter, SphinxLogger logger)
        {
            PocketSphinx ps;

            if (config == null)
            {
                logger.E_ERROR("No configuration specified");
                return null;
            }

            ps = new PocketSphinx();
            ps.logger = logger;
            if (ps.ps_reinit(config, fileAdapter) < 0)
            {
                return null;
            }

            return ps;
        }

        internal void ps_expand_file_config(Pointer<byte> arg, Pointer<byte> extra_arg,
                          Pointer<byte> hmmdir, Pointer<byte> file, FileAdapter fileAdapter)
        {
            Pointer<byte> val;
            if ((val = CommandLine.cmd_ln_str_r(config, arg)).IsNonNull)
            {
                CommandLine.cmd_ln_set_str_extra_r(config, extra_arg, val);
            }
            else if (hmmdir.IsNull)
            {
                CommandLine.cmd_ln_set_str_extra_r(config, extra_arg, PointerHelpers.NULL<byte>());
            }
            else
            {
                string path = System.IO.Path.Combine(cstring.FromCString(hmmdir), cstring.FromCString(file));
                Pointer<byte> tmp = cstring.ToCString(path);
                if (fileAdapter.file_exists(tmp))
                    CommandLine.cmd_ln_set_str_extra_r(config, extra_arg, tmp);
                else
                    CommandLine.cmd_ln_set_str_extra_r(config, extra_arg, PointerHelpers.NULL<byte>());
            }
        }

        internal void ps_expand_model_config(FileAdapter fileAdapter)
        {
            Pointer<byte> hmmdir;
            Pointer<byte> featparams;
            /* Get acoustic model filenames and add them to the command-line */
            hmmdir = CommandLine.cmd_ln_str_r(config, cstring.ToCString("-hmm"));
            ps_expand_file_config(cstring.ToCString("-mdef"), cstring.ToCString("_mdef"), hmmdir, cstring.ToCString("mdef"), fileAdapter);
            ps_expand_file_config(cstring.ToCString("-mean"), cstring.ToCString("_mean"), hmmdir, cstring.ToCString("means"), fileAdapter);
            ps_expand_file_config(cstring.ToCString("-var"), cstring.ToCString("_var"), hmmdir, cstring.ToCString("variances"), fileAdapter);
            ps_expand_file_config(cstring.ToCString("-tmat"), cstring.ToCString("_tmat"), hmmdir, cstring.ToCString("transition_matrices"), fileAdapter);
            ps_expand_file_config(cstring.ToCString("-mixw"), cstring.ToCString("_mixw"), hmmdir, cstring.ToCString("mixture_weights"), fileAdapter);
            ps_expand_file_config(cstring.ToCString("-sendump"), cstring.ToCString("_sendump"), hmmdir, cstring.ToCString("sendump"), fileAdapter);
            ps_expand_file_config(cstring.ToCString("-fdict"), cstring.ToCString("_fdict"), hmmdir, cstring.ToCString("noisedict"), fileAdapter);
            ps_expand_file_config(cstring.ToCString("-lda"), cstring.ToCString("_lda"), hmmdir, cstring.ToCString("feature_transform"), fileAdapter);
            ps_expand_file_config(cstring.ToCString("-featparams"), cstring.ToCString("_featparams"), hmmdir, cstring.ToCString("feat.params"), fileAdapter);
            ps_expand_file_config(cstring.ToCString("-senmgau"), cstring.ToCString("_senmgau"), hmmdir, cstring.ToCString("senmgau"), fileAdapter);

            /* Look for feat.params in acoustic model dir. */
            if ((featparams = CommandLine.cmd_ln_str_r(config, cstring.ToCString("_featparams"))).IsNonNull)
            {
                if (CommandLine.cmd_ln_parse_file_r(config, feat_defn(), featparams, 0, fileAdapter, logger) != null)
                    logger.E_INFO(string.Format("Parsed model-specific feature parameters from {0}\n",
                            cstring.FromCString(featparams)));
            }

            /* Print here because acmod_init might load feat.params file */
            //if (err_get_logfp() != NULL)
            //{
            //    cmd_ln.cmd_ln_print_values_r(ps.config, err_get_logfp(), ps_args());
            //}
        }

        internal ps_search_t ps_find_search(Pointer<byte> name)
        {
            BoxedValue<object> search = new BoxedValue<object>();
            HashTable.hash_table_lookup(searches, name, search);

            return (ps_search_t)search.Val;
        }

        internal int ps_reinit(cmd_ln_t newConfig, FileAdapter fileAdapter)
        {
            Pointer<byte> path;
            Pointer<byte> keyphrase;

            if (newConfig != null && newConfig != this.config)
            {
                this.config = newConfig;
            }

            /* Set up logging. We need to do this earlier because we want to dump
             * the information to the configured log, not to the stderr. */

            /* Fill in some default arguments. */
            ps_expand_model_config(fileAdapter);

            /* Free old searches (do this before other reinit) */
            this.searches = null;
            this.search = null;
            this.searches = HashTable.hash_table_new(3, HashTable.HASH_CASE_YES, this.logger);

            /* Free old acmod. */
            this.acmod = null;

            /* Free old dictionary (must be done after the two things above) */
            this.dict = null;

            /* Free d2p */
            this.d2p = null;

            /* Logmath computation (used in acmod and search) */
            if (this.lmath == null
                || (LogMath.logmath_get_base(this.lmath) !=
                    (double)CommandLine.cmd_ln_float_r(this.config, cstring.ToCString("-logbase"))))
            {
                this.lmath = LogMath.logmath_init
                    ((double)CommandLine.cmd_ln_float_r(this.config, cstring.ToCString("-logbase")), 0,
                     CommandLine.cmd_ln_boolean_r(this.config, cstring.ToCString("-bestpath")),
                     this.logger);
            }

            /* Acoustic model (this is basically everything that
             * uttproc.c, senscr.c, and others used to do) */
            if ((this.acmod = AcousticModel.acmod_init(this.config, this.lmath, null, null, fileAdapter, this.logger)) == null)
                return -1;

            if (CommandLine.cmd_ln_int_r(this.config, cstring.ToCString("-pl_window")) > 0)
            {
                /* Initialize an auxiliary phone loop search, which will run in
                 * "parallel" with FSG or N-Gram search. */
                if ((this.phone_loop = PhoneLoopSearch.phone_loop_search_init(this.config, this.acmod, this.dict, this.logger)) == null)
                    return -1;
                HashTable.hash_table_enter(this.searches,
                                 ps_search_name(this.phone_loop),
                                 this.phone_loop);
            }

            /* Dictionary and triphone mappings (depends on acmod). */
            /* FIXME: pass config, change arguments, implement LTS, etc. */
            if ((this.dict = Dictionary.dict_init(this.config, this.acmod.mdef, fileAdapter, this.logger)) == null)
                return -1;
            if ((this.d2p = DictToPID.dict2pid_build(this.acmod.mdef, this.dict, this.logger)) == null)
                return -1;

            /* Determine whether we are starting out in FSG or N-Gram search mode.
             * If neither is used skip search initialization. */

            /* Load KWS if one was specified in config */
            if ((keyphrase = CommandLine.cmd_ln_str_r(this.config, cstring.ToCString("-keyphrase"))).IsNonNull)
            {
                if (ps_set_keyphrase(PS_DEFAULT_SEARCH, keyphrase) != 0)
                    return -1;
                ps_set_search(PS_DEFAULT_SEARCH);
            }

            if ((path = CommandLine.cmd_ln_str_r(this.config, cstring.ToCString("-kws"))).IsNonNull)
            {
                if (ps_set_kws(PS_DEFAULT_SEARCH, path) != 0)
                    return -1;
                ps_set_search(PS_DEFAULT_SEARCH);
            }

            // LOGAN cut this out
            /* Load an FSG if one was specified in config */
            /*if ((path = cmd_ln.cmd_ln_str_r(this.config, "-fsg"))) {
                fsg_model_t *fsg = fsg_model_readfile(path, this.lmath, lw);
                if (!fsg)
                    return -1;
                if (ps_set_fsg(ps, PS_DEFAULT_SEARCH, fsg)) {
                    fsg_model_free(fsg);
                    return -1;
                }
                fsg_model_free(fsg);
                ps_set_search(ps, PS_DEFAULT_SEARCH);
            }*/

            /* Or load a JSGF grammar */
            /*if ((path = cmd_ln.cmd_ln_str_r(this.config, "-jsgf"))) {
                if (ps_set_jsgf_file(ps, PS_DEFAULT_SEARCH, path)
                    || ps_set_search(ps, PS_DEFAULT_SEARCH))
                    return -1;
            }

            if ((path = cmd_ln.cmd_ln_str_r(this.config, "-allphone"))) {
                if (ps_set_allphone_file(ps, PS_DEFAULT_SEARCH, path)
                        || ps_set_search(ps, PS_DEFAULT_SEARCH))
                        return -1;
            }

            if ((path = cmd_ln.cmd_ln_str_r(this.config, "-lm")) && 
                !cmd_ln.cmd_ln_boolean_r(this.config, "-allphone")) {
                if (ps_set_lm_file(ps, PS_DEFAULT_SEARCH, path)
                    || ps_set_search(ps, PS_DEFAULT_SEARCH))
                    return -1;
            }

            if ((path = cmd_ln.cmd_ln_str_r(this.config, "-lmctl"))) {
                const char *name;
                ngram_model_t *lmset;
                ngram_model_set_iter_t *lmset_it;

                if (!(lmset = ngram_model_set_read(this.config, path, this.lmath))) {
                    E_ERROR("Failed to read language model control file: %s\n", path);
                    return -1;
                }

                for(lmset_it = ngram_model_set_iter(lmset);
                    lmset_it; lmset_it = ngram_model_set_iter_next(lmset_it)) {    
                    ngram_model_t *lm = ngram_model_set_iter_model(lmset_it, &name);            
                    E_INFO("adding search %s\n", name);
                    if (ps_set_lm(ps, name, lm)) {
                        ngram_model_set_iter_free(lmset_it);
                    ngram_model_free(lmset);
                        return -1;
                    }
                }
                ngram_model_free(lmset);

                name = cmd_ln.cmd_ln_str_r(this.config, "-lmname");
                if (name)
                    ps_set_search(ps, name);
                else {
                    E_ERROR("No default LM name (-lmname) for `-lmctl'\n");
                    return -1;
                }
            }*/

            /* Initialize performance timer. */
            this.perf.name = "decode";
            Profiler.ptmr_init(this.perf);

            return 0;
        }

        internal logmath_t ps_get_logmath()
        {
            return this.lmath;
        }

        internal ps_mllr_t ps_update_mllr(ps_mllr_t mllr, FileAdapter fileAdapter)
        {
            return AcousticModel.acmod_update_mllr(this.acmod, mllr, fileAdapter);
        }

        internal int ps_set_search(Pointer<byte> name)
        {
            ps_search_t search;

            if (this.acmod.state != acmod_state_e.ACMOD_ENDED && this.acmod.state != acmod_state_e.ACMOD_IDLE)
            {
                this.logger.E_ERROR("Cannot change search while decoding, end utterance first\n");
                return -1;
            }

            if ((search = ps_find_search(name)) == null)
            {
                return -1;
            }

            this.search = search;
            /* Set pl window depending on the search */
            if (cstring.strcmp(PS_SEARCH_TYPE_NGRAM, ps_search_type(search)) == 0)
            {
                this.pl_window = (int)CommandLine.cmd_ln_int_r(this.config, cstring.ToCString("-pl_window"));
            }
            else
            {
                this.pl_window = 0;
            }

            return 0;
        }

        internal int set_search_internal(ps_search_t search)
        {
            ps_search_t old_search;

            if (search == null)
                return -1;

            search.pls = this.phone_loop;
            old_search = (ps_search_t)HashTable.hash_table_replace(this.searches, ps_search_name(search), search);

            return 0;
        }

        internal int ps_set_kws(Pointer<byte> name, Pointer<byte> keyfile)
        {
            ps_search_t search;
            search = KeywordSearch.kws_search_init(name, PointerHelpers.NULL<byte>(), keyfile, this.config, this.acmod, this.dict, this.d2p, this.logger);
            return set_search_internal(search);
        }

        internal int ps_set_keyphrase(Pointer<byte> name, Pointer<byte> keyphrase)
        {
            ps_search_t search;
            search = KeywordSearch.kws_search_init(name, keyphrase, PointerHelpers.NULL<byte>(), this.config, this.acmod, this.dict, this.d2p, this.logger);
            return set_search_internal(search);
        }

        internal int ps_start_utt()
        {
            int rv;
            Pointer<byte> uttid = PointerHelpers.Malloc<byte>(16);

            if (this.acmod.state == acmod_state_e.ACMOD_STARTED || this.acmod.state == acmod_state_e.ACMOD_PROCESSING)
            {
                this.logger.E_ERROR("Utterance already started\n");
                return -1;
            }

            if (this.search == null)
            {
                this.logger.E_ERROR("No search module is selected, did you forget to specify a language model or grammar?\n");
                return -1;
            }

            Profiler.ptmr_reset(this.perf);
            Profiler.ptmr_start(this.perf);

            stdio.sprintf(uttid, string.Format("{0}", this.uttno));
            ++this.uttno;

            /* Remove any residual word lattice and hypothesis. */
            this.search.post = 0;
            this.search.hyp_str = PointerHelpers.NULL<byte>();
            if ((rv = AcousticModel.acmod_start_utt(this.acmod)) < 0)
                return rv;
            
            /* Start auxiliary phone loop search. */
            if (this.phone_loop != null)
                ps_search_start(this.phone_loop);

            return ps_search_start(this.search);
        }

        internal int ps_search_forward()
        {
            int nfr;

            nfr = 0;
            while (this.acmod.n_feat_frame > 0)
            {
                int k;
                if (this.pl_window > 0)
                    if ((k = ps_search_step(this.phone_loop, this.acmod.output_frame)) < 0)
                        return k;
                if (this.acmod.output_frame >= this.pl_window)
                    if ((k = ps_search_step(this.search,
                                            this.acmod.output_frame - this.pl_window)) < 0)
                        return k;
                AcousticModel.acmod_advance(this.acmod);
                ++this.n_frame;
                ++nfr;
            }
            return nfr;
        }

        internal int ps_process_raw(Pointer<short> data,
                       uint n_samples,
                       int no_search,
                       int full_utt)
        {
            int n_searchfr = 0;

            if (this.acmod.state == acmod_state_e.ACMOD_IDLE)
            {
                this.logger.E_ERROR("Failed to process data, utterance is not started. Use start_utt to start it\n");
                return 0;
            }

            if (no_search != 0)
                AcousticModel.acmod_set_grow(this.acmod, 1);

            while (n_samples != 0)
            {
                int nfr;

                /* Process some data into features. */
                BoxedValue<Pointer<short>> boxed_data = new BoxedValue<Pointer<short>>(data);
                BoxedValueUInt boxed_n_samples = new BoxedValueUInt(n_samples);
                if ((nfr = AcousticModel.acmod_process_raw(this.acmod, boxed_data,
                                             boxed_n_samples, full_utt)) < 0)
                {
                    data = boxed_data.Val;
                    n_samples = boxed_n_samples.Val;
                    return nfr;
                }

                data = boxed_data.Val;
                n_samples = boxed_n_samples.Val;

                /* Score and search as much data as possible */
                if (no_search != 0)
                    continue;
                if ((nfr = ps_search_forward()) < 0)
                    return nfr;
                n_searchfr += nfr;
            }

            return n_searchfr;
        }

        internal int ps_end_utt()
        {
            int rv, i;

            if (this.acmod.state == acmod_state_e.ACMOD_ENDED || this.acmod.state == acmod_state_e.ACMOD_IDLE)
            {
                this.logger.E_ERROR("Utterance is not started\n");
                return -1;
            }
            AcousticModel.acmod_end_utt(this.acmod);

            /* Search any remaining frames. */
            if ((rv = ps_search_forward()) < 0)
            {
                Profiler.ptmr_stop(this.perf);
                return rv;
            }
            /* Finish phone loop search. */
            if (this.phone_loop != null)
            {
                if ((rv = ps_search_finish(this.phone_loop)) < 0)
                {
                    Profiler.ptmr_stop(this.perf);
                    return rv;
                }
            }
            /* Search any frames remaining in the lookahead window. */
            if (this.acmod.output_frame >= this.pl_window)
            {
                for (i = this.acmod.output_frame - this.pl_window;
                     i < this.acmod.output_frame; ++i)
                    ps_search_step(this.search, i);
            }
            /* Finish main search. */
            if ((rv = ps_search_finish(this.search)) < 0)
            {
                Profiler.ptmr_stop(this.perf);
                return rv;
            }

            Profiler.ptmr_stop(this.perf);

            /* Log a backtrace if requested. */
            if (CommandLine.cmd_ln_boolean_r(this.config, cstring.ToCString("-backtrace")) != 0)
            {
                Pointer<byte> hyp;
                ps_seg_t seg;
                BoxedValueInt score = new BoxedValueInt();

                hyp = ps_get_hyp(score);

                if (hyp.IsNonNull)
                {
                    this.logger.E_INFO(string.Format("{0} ({1})\n", cstring.FromCString(hyp), score.Val));
                    this.logger.E_INFO_NOFN(string.Format("{0,-20} {1,-5} {2,-5} {3,-5} {4,-10} {5,-10} {6,-3}\n",
                            "word", "start", "end", "pprob", "ascr", "lscr", "lback"));
                    for (seg = ps_seg_iter(); seg != null; seg = ps_seg_next(seg))
                    {
                        Pointer<byte> word;
                        BoxedValueInt sf = new BoxedValueInt();
                        BoxedValueInt ef = new BoxedValueInt();
                        int post;
                        BoxedValueInt lscr = new BoxedValueInt();
                        BoxedValueInt ascr = new BoxedValueInt();
                        BoxedValueInt lback = new BoxedValueInt();

                        word = ps_seg_word(seg);
                        ps_seg_frames(seg, sf, ef);
                        post = ps_seg_prob(seg, ascr, lscr, lback);
                        this.logger.E_INFO_NOFN(string.Format("{0,-20} {1,-5} {2,-5} {3,-5:F3} {4,-10} {5,-10} {6,-3}\n",
                                        cstring.FromCString(word), sf.Val, ef.Val, LogMath.logmath_exp(ps_get_logmath(), post), ascr.Val, lscr.Val, lback.Val));
                    }
                }
            }
            return rv;
        }

        internal Pointer<byte> ps_get_hyp(BoxedValueInt out_best_score)
        {
            Pointer<byte> hyp;
            Profiler.ptmr_start(this.perf);
            hyp = ps_search_hyp(this.search, out_best_score);
            Profiler.ptmr_stop(this.perf);
            return hyp;
        }

        internal ps_seg_t ps_seg_iter()
        {
            ps_seg_t itor;
            Profiler.ptmr_start(this.perf);
            itor = ps_search_seg_iter(this.search);
            Profiler.ptmr_stop(this.perf);
            return itor;
        }

        internal byte ps_get_in_speech()
        {
            return FrontendInterface.fe_get_vad_state(this.acmod.fe);
        }

        internal static ps_seg_t ps_seg_next(ps_seg_t seg)
        {
            return ps_search_seg_next(seg);
        }

        internal static Pointer<byte> ps_seg_word(ps_seg_t seg)
        {
            return seg.word;
        }

        internal static void ps_seg_frames(ps_seg_t seg, BoxedValueInt out_sf, BoxedValueInt out_ef)
        {
            int uf;
            uf = AcousticModel.acmod_stream_offset(seg.search.acmod);
            if (out_sf != null) out_sf.Val = seg.sf + uf;
            if (out_ef != null) out_ef.Val = seg.ef + uf;
        }

        internal static int ps_seg_prob(ps_seg_t seg, BoxedValueInt out_ascr, BoxedValueInt out_lscr, BoxedValueInt out_lback)
        {
            if (out_ascr != null) out_ascr.Val = seg.ascr;
            if (out_lscr != null) out_lscr.Val = seg.lscr;
            if (out_lback != null) out_lback.Val = seg.lback;
            return seg.prob;
        }

        internal static void ps_search_init(
            ps_search_t search,
            ps_searchfuncs_t vt,
            Pointer<byte> type,
            Pointer<byte> name,
            cmd_ln_t config,
            acmod_t acousticmod,
            dict_t dictionary,
            dict2pid_t d2p)
        {
            search.vt = vt;
            search.name = CKDAlloc.ckd_salloc(name);
            search.type = CKDAlloc.ckd_salloc(type);

            search.config = config;
            search.acmod = acousticmod;
            if (d2p != null)
                search.d2p = d2p;
            else
                search.d2p = null;
            if (dictionary != null)
            {
                search.dict = dictionary;
                search.start_wid = Dictionary.dict_startwid(dictionary);
                search.finish_wid = Dictionary.dict_finishwid(dictionary);
                search.silence_wid = Dictionary.dict_silwid(dictionary);
                search.n_words = Dictionary.dict_size(dictionary);
            }
            else
            {
                search.dict = null;
                search.start_wid = search.finish_wid = search.silence_wid = -1;
                search.n_words = 0;
            }
        }

        internal static void ps_search_base_reinit(ps_search_t search, dict_t dictionary,
                              dict2pid_t d2p)
        {
            /* FIXME: _retain() should just return NULL if passed NULL. */
            if (dictionary != null)
            {
                search.dict = dictionary;
                search.start_wid = Dictionary.dict_startwid(dictionary);
                search.finish_wid = Dictionary.dict_finishwid(dictionary);
                search.silence_wid = Dictionary.dict_silwid(dictionary);
                search.n_words = Dictionary.dict_size(dictionary);
            }
            else
            {
                search.dict = null;
                search.start_wid = search.finish_wid = search.silence_wid = -1;
                search.n_words = 0;
            }
            if (d2p != null)
                search.d2p = d2p;
            else
                search.d2p = null;
        }

        internal static cmd_ln_t ps_search_config(ps_search_t s)
        {
            return s.config;
        }

        internal static acmod_t ps_searchacmod(ps_search_t s)
        {
            return s.acmod;
        }

        internal static dict_t ps_search_dict(ps_search_t s)
        {
            return s.dict;
        }

        internal static dict2pid_t ps_search_dict2pid(ps_search_t s)
        {
            return s.d2p;
        }

        internal static int ps_search_post(ps_search_t s)
        {
            return s.post;
        }

        internal static ps_search_t ps_search_lookahead(ps_search_t s)
        {
            return s.pls;
        }

        internal static int ps_search_n_words(ps_search_t s)
        {
            return s.n_words;
        }

        internal static Pointer<byte> ps_search_type(ps_search_t s)
        {
            return s.type;
        }

        internal static Pointer<byte> ps_search_name(ps_search_t s)
        {
            return s.name;
        }

        internal static int ps_search_start(ps_search_t s)
        {
            return s.vt.start(s);
        }

        internal static int ps_search_step(ps_search_t s, int i)
        {
            return s.vt.step(s, i);
        }

        internal static int ps_search_finish(ps_search_t s)
        {
            return s.vt.finish(s);
        }

        internal static int ps_search_reinit(ps_search_t s, dict_t dict, dict2pid_t d2p)
        {
            return s.vt.reinit(s, dict, d2p);
        }

        internal static Pointer<byte> ps_search_hyp(ps_search_t s, BoxedValueInt out_score)
        {
            return s.vt.hyp(s, out_score);
        }

        internal static int ps_search_prob(ps_search_t s)
        {
            return s.vt.prob(s);
        }

        internal static ps_seg_t ps_search_seg_iter(ps_search_t s)
        {
            return s.vt.seg_iter(s);
        }

        internal static ps_seg_t ps_search_seg_next(ps_seg_t seg)
        {
            return seg.vt.seg_next(seg);
        }

        internal static void ps_search_seg_free(ps_seg_t seg)
        {
            seg.vt.seg_free(seg);
        }
    }
}
