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
    internal static class AcousticModel
    {
        public const short SENSCR_DUMMY = 0x7fff;

        internal static int acmod_init_am(acmod_t acmod, FileAdapter fileAdapter)
        {
           Pointer<byte> mdeffn, tmatfn, mllrfn, hmmdir;

            /* Read model definition. */
            if ((mdeffn = CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("_mdef"))).IsNull)
            {
                if ((hmmdir = CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("-hmm"))).IsNull)
                    acmod.logger.E_ERROR("Acoustic model definition is not specified either with -mdef option or with -hmm\n");
                else
                    acmod.logger.E_ERROR(string.Format("Folder '{0}' does not contain acoustic model definition 'mdef'\n", cstring.FromCString(hmmdir)));

                return -1;
            }

            if ((acmod.mdef = BinaryModelDef.bin_mdef_read(acmod.config, mdeffn, fileAdapter, acmod.logger)) == null)
            {
                acmod.logger.E_ERROR(string.Format("Failed to read acoustic model definition from {0}\n", cstring.FromCString(mdeffn)));
                return -1;
            }

            /* Read transition matrices. */
            if ((tmatfn = CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("_tmat"))).IsNull)
            {
                acmod.logger.E_ERROR("No tmat file specified\n");
                return -1;
            }
            acmod.tmat = TransitionMatrix.tmat_init(tmatfn, fileAdapter, acmod.lmath,
                                    CommandLine.cmd_ln_float_r(acmod.config, cstring.ToCString("-tmatfloor")),
                                    1, acmod.logger);

            /* Read the acoustic models. */
            if ((CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("_mean")).IsNull)
                || (CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("_var")).IsNull)
                || (CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("_tmat")).IsNull))
            {
                acmod.logger.E_ERROR("No mean/var/tmat files specified\n");
                return -1;
            }

            if (CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("_senmgau")).IsNonNull)
            {
                acmod.logger.E_INFO("Using general multi-stream GMM computation\n");
                acmod.mgau = MultiStreamMixGaussian.ms_mgau_init(acmod, acmod.lmath, acmod.mdef, fileAdapter, acmod.logger);
                if (acmod.mgau == null)
                    return -1;
            }
            else
            {
                acmod.logger.E_INFO("Attempting to use PTM computation module\n");
                if ((acmod.mgau = PhoneticMixGaussian.ptm_mgau_init(acmod, acmod.mdef, fileAdapter, acmod.logger)) == null)
                {
                    acmod.logger.E_INFO("Attempting to use semi-continuous computation module\n");
                    if ((acmod.mgau = S2SemiMixGaussian.s2_semi_mgau_init(acmod, fileAdapter, acmod.logger)) == null)
                    {
                        acmod.logger.E_INFO("Falling back to general multi-stream GMM computation\n");
                        acmod.mgau = MultiStreamMixGaussian.ms_mgau_init(acmod, acmod.lmath, acmod.mdef, fileAdapter, acmod.logger);
                        if (acmod.mgau == null)
                        {
                            acmod.logger.E_ERROR("Failed to read acoustic model\n");
                            return -1;
                        }
                    }
                }
            }

            /* If there is an MLLR transform, apply it. */
            if ((mllrfn = CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("-mllr"))).IsNonNull)
            {
                ps_mllr_t mllr = MLLR.ps_mllr_read(mllrfn, fileAdapter, acmod.logger);
                if (mllr == null)
                    return -1;
                acmod_update_mllr(acmod, mllr, fileAdapter);
            }

            return 0;
        }

        internal static int acmod_init_feat(acmod_t acmod, FileAdapter fileAdapter)
        {
            acmod.fcb =
                Feature.feat_init(CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("-feat")),
                          CepstralMeanNormalization.cmn_type_from_str(CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("-cmn")), acmod.logger),
                          CommandLine.cmd_ln_boolean_r(acmod.config, cstring.ToCString("-varnorm")),
                          AcousticGainControl.agc_type_from_str(CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("-agc")), acmod.logger),
                          1,
                          (int)CommandLine.cmd_ln_int_r(acmod.config, cstring.ToCString("-ceplen")),
                          acmod.logger);
            if (acmod.fcb == null)
                return -1;

            if (CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("_lda")).IsNonNull)
            {
                acmod.logger.E_INFO(string.Format("Reading linear feature transformation from {0}\n",
                       cstring.FromCString(CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("_lda")))));
                if (lda.feat_read_lda(acmod.fcb,
                                  CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("_lda")),
                                  fileAdapter,
                                  (int)CommandLine.cmd_ln_int_r(acmod.config, cstring.ToCString("-ldadim")),
                                  acmod.logger) < 0)
                    return -1;
            }

            if (CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("-svspec")).IsNonNull)
            {
                Pointer<Pointer<int>> subvecs;
                acmod.logger.E_INFO(string.Format("Using subvector specification {0}\n", cstring.FromCString(CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("-svspec")))));
                if ((subvecs = Feature.parse_subvecs(CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("-svspec")), acmod.logger)).IsNull)
                    return -1;
                if ((Feature.feat_set_subvecs(acmod.fcb, subvecs)) < 0)
                    return -1;
            }

            if (CommandLine.cmd_ln_exists_r(acmod.config, cstring.ToCString("-agcthresh")) != 0
                && 0 != cstring.strcmp(CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("-agc")), cstring.ToCString("none")))
            {
                AcousticGainControl.agc_set_threshold(acmod.fcb.agc_struct, (float)CommandLine.cmd_ln_float_r(acmod.config, cstring.ToCString("-agcthresh")));
            }

            if (acmod.fcb.cmn_struct != null
                && CommandLine.cmd_ln_exists_r(acmod.config, cstring.ToCString("-cmninit")) != 0)
            {
                // LOGAN modified - this old method was way too obtuse
                //string valList = cstring.FromCString(cmd_ln.cmd_ln_str_r(acmod.Deref.config, cstring.ToCString("-cmninit")));
                //string[] parts = valList.Split(',');
                //for (int c = 0; c < parts.Length; c++)
                //{
                //    acmod.Deref.fcb.cmn_struct.Deref.cmn_mean[c] = float.Parse(parts[c]);
                //}

                Pointer<byte> c, cc, vallist;
                int nvals;

                vallist = CKDAlloc.ckd_salloc(CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("-cmninit")));
                c = vallist;
                nvals = 0;
                while (nvals < acmod.fcb.cmn_struct.veclen
                       && (cc = cstring.strchr(c, (byte)',')).IsNonNull)
                {
                    cc[0] = (byte)'\0';
                    acmod.fcb.cmn_struct.cmn_mean[nvals] = (float)(StringFuncs.atof_c(c));
                    c = cc + 1;
                    ++nvals;
                }
                if (nvals < acmod.fcb.cmn_struct.veclen && c[0] != '\0')
                {
                    acmod.fcb.cmn_struct.cmn_mean[nvals] = (float)(StringFuncs.atof_c(c));
                }
            }
            return 0;
        }

        internal static int acmod_fe_mismatch(acmod_t acmod, fe_t _fe)
        {
            /* Output vector dimension needs to be the same. */
            if (CommandLine.cmd_ln_int_r(acmod.config, cstring.ToCString("-ceplen")) != FrontendInterface.fe_get_output_size(_fe))
            {
                acmod.logger.E_ERROR(string.Format("Configured feature length {0} doesn't match feature extraction output size {1}\n",
                        CommandLine.cmd_ln_int_r(acmod.config, cstring.ToCString("-ceplen")),
                        FrontendInterface.fe_get_output_size(_fe)));
                return 1;
            }
            /* Feature parameters need to be the same. */
            /* ... */
            return 0;
        }

        internal static int acmod_feat_mismatch(acmod_t acmod, feat_t fcb)
        {
            /* Feature type needs to be the same. */
            if (0 != cstring.strcmp(CommandLine.cmd_ln_str_r(acmod.config, cstring.ToCString("-feat")), Feature.feat_name(fcb)))
                return 1;
            /* Input vector dimension needs to be the same. */
            if (CommandLine.cmd_ln_int_r(acmod.config, cstring.ToCString("-ceplen")) != Feature.feat_cepsize(fcb))
                return 1;
            /* FIXME: Need to check LDA and stuff too. */
            return 0;
        }

        internal static acmod_t acmod_init(cmd_ln_t config, logmath_t lmath, fe_t fe, feat_t fcb, FileAdapter fileAdapter, SphinxLogger logger)
        {
            acmod_t acmod = new acmod_t();
            acmod.logger = logger;
            acmod.config = config;
            acmod.lmath = lmath;
            acmod.state = acmod_state_e.ACMOD_IDLE;

            /* Initialize feature computation. */
            if (fe != null)
            {
                if (acmod_fe_mismatch(acmod, fe) != 0)
                    return null;
                acmod.fe = fe;
            }
            else
            {
                /* Initialize a new front end. */
                acmod.fe = FrontendInterface.fe_init_auto_r(config, logger);
                if (acmod.fe == null)
                    return null;
                if (acmod_fe_mismatch(acmod, acmod.fe) != 0)
                    return null;
            }
            if (fcb != null)
            {
                if (acmod_feat_mismatch(acmod, fcb) != 0)
                    return null;
                acmod.fcb = fcb;
            }
            else
            {
                /* Initialize a new fcb. */
                if (acmod_init_feat(acmod, fileAdapter) < 0)
                    return null;
            }

            /* Load acoustic model parameters. */
            if (acmod_init_am(acmod, fileAdapter) < 0)
                return null;


            /* The MFCC buffer needs to be at least as large as the dynamic
             * feature window.  */
            acmod.n_mfc_alloc = acmod.fcb.window_size * 2 + 1;
            acmod.mfc_buf = (Pointer<Pointer<float>>)
                CKDAlloc.ckd_calloc_2d<float>((uint)acmod.n_mfc_alloc, (uint)acmod.fcb.cepsize);

            /* Feature buffer has to be at least as large as MFCC buffer. */
            acmod.n_feat_alloc = checked((int)(acmod.n_mfc_alloc + CommandLine.cmd_ln_int_r(config, cstring.ToCString("-pl_window"))));
            acmod.feat_buf = Feature.feat_array_alloc(acmod.fcb, acmod.n_feat_alloc);
            acmod.framepos = CKDAlloc.ckd_calloc<long>(acmod.n_feat_alloc);

            acmod.utt_start_frame = 0;

            /* Senone computation stuff. */
            acmod.senone_scores = new short[BinaryModelDef.bin_mdef_n_sen(acmod.mdef)];
            acmod.senone_active_vec = BitVector.bitvec_alloc(BinaryModelDef.bin_mdef_n_sen(acmod.mdef));
            acmod.senone_active = CKDAlloc.ckd_calloc<byte>(BinaryModelDef.bin_mdef_n_sen(acmod.mdef));
            acmod.log_zero = LogMath.logmath_get_zero(acmod.lmath);
            acmod.compallsen = CommandLine.cmd_ln_boolean_r(config, cstring.ToCString("-compallsen"));
            return acmod;
        }

        internal static ps_mllr_t acmod_update_mllr(acmod_t acmod, ps_mllr_t _mllr, FileAdapter fileAdapter)
        {
            acmod.mllr = _mllr;
            acmod.mgau.vt.transform(acmod.mgau, _mllr, fileAdapter);

            return _mllr;
        }

        internal static void acmod_grow_feat_buf(acmod_t acmod, int nfr)
        {
            if (nfr > HiddenMarkovModel.MAX_N_FRAMES)
                acmod.logger.E_FATAL(string.Format("Decoder can not process more than {0} frames at once, requested {1}\n", HiddenMarkovModel.MAX_N_FRAMES, nfr));

            acmod.feat_buf = Feature.feat_array_realloc(acmod.fcb, acmod.feat_buf,
                                                 acmod.n_feat_alloc, nfr);
            acmod.framepos = CKDAlloc.ckd_realloc<long>(acmod.framepos, nfr);
            acmod.n_feat_alloc = nfr;
        }

        internal static int acmod_set_grow(acmod_t acmod, int grow_feat)
        {
            int tmp = acmod.grow_feat;
            acmod.grow_feat = (byte)grow_feat;

            /* Expand feat_buf to a reasonable size to start with. */
            if (grow_feat != 0 && acmod.n_feat_alloc < 128)
                acmod_grow_feat_buf(acmod, 128);

            return tmp;
        }

        internal static int acmod_start_utt(acmod_t acmod)
        {
            FrontendInterface.fe_start_utt(acmod.fe);
            acmod.state = acmod_state_e.ACMOD_STARTED;
            acmod.n_mfc_frame = 0;
            acmod.n_feat_frame = 0;
            acmod.mfc_outidx = 0;
            acmod.feat_outidx = 0;
            acmod.output_frame = 0;
            acmod.senscr_frame = -1;
            acmod.n_senone_active = 0;
            acmod.mgau.frame_idx = 0;
            acmod.rawdata_pos = 0;

            return 0;
        }

        internal static int acmod_end_utt(acmod_t acmod)
        {
            int nfr = 0;

            acmod.state = acmod_state_e.ACMOD_ENDED;
            if (acmod.n_mfc_frame < acmod.n_mfc_alloc)
            {
                int inptr;
                /* Where to start writing them (circular buffer) */
                inptr = (acmod.mfc_outidx + acmod.n_mfc_frame) % acmod.n_mfc_alloc;
                /* nfr is always either zero or one. */
                BoxedValueInt boxed_nfr = new BoxedValueInt(nfr);
                FrontendInterface.fe_end_utt(acmod.fe, acmod.mfc_buf[inptr], boxed_nfr);
                nfr = boxed_nfr.Val;
                acmod.n_mfc_frame += nfr;

                /* Process whatever's left, and any leadout or update stats if needed. */
                if (nfr != 0)
                    nfr = acmod_process_mfcbuf(acmod);
                else
                    Feature.feat_update_stats(acmod.fcb);
            }
            
            return nfr;
        }

        internal static int acmod_process_full_cep(acmod_t acmod,
                               BoxedValue<Pointer<Pointer<float>>> inout_cep,
                               BoxedValueInt inout_n_frames)
        {
            int nfr;
            
            /* Resize feat_buf to fit. */
            if (acmod.n_feat_alloc < inout_n_frames.Val)
            {

                if (inout_n_frames.Val > HiddenMarkovModel.MAX_N_FRAMES)
                    acmod.logger.E_FATAL(string.Format("Batch processing can not process more than {0} frames at once, requested {1}\n", HiddenMarkovModel.MAX_N_FRAMES, inout_n_frames.Val));
                
                acmod.feat_buf = Feature.feat_array_alloc(acmod.fcb, inout_n_frames.Val);
                acmod.n_feat_alloc = inout_n_frames.Val;
                acmod.n_feat_frame = 0;
                acmod.feat_outidx = 0;
            }
            /* Make dynamic features. */
            nfr = Feature.feat_s2mfc2feat_live(acmod.fcb, inout_cep.Val, inout_n_frames,
                                       1, 1, acmod.feat_buf);
            acmod.n_feat_frame = nfr;
            SphinxAssert.assert(acmod.n_feat_frame <= acmod.n_feat_alloc);
            inout_cep.Val += inout_n_frames.Val;
            inout_n_frames.Val = 0;

            return nfr;
        }

        internal static int acmod_process_full_raw(acmod_t acmod,
                               BoxedValue<Pointer<short>> inout_raw,
                               BoxedValueUInt inout_n_samps)
        {
            int nfr, ntail;
            Pointer<Pointer<float>> cepptr;

            /* Write to logging file if any. */
            if (inout_n_samps.Val + acmod.rawdata_pos < acmod.rawdata_size)
            {
                inout_raw.Val.MemCopyTo(acmod.rawdata + acmod.rawdata_pos, (int)inout_n_samps.Val);
                acmod.rawdata_pos += checked((int)inout_n_samps.Val);
            }

            /* Resize mfc_buf to fit. */
            // LOGAN fixme: bad conversion between boxed integer types here
            BoxedValueInt boxed_n_samps = new BoxedValueInt((int)inout_n_samps.Val);
            BoxedValueInt boxed_nfr = new BoxedValueInt();
            if (FrontendInterface.fe_process_frames(acmod.fe, null, boxed_n_samps, PointerHelpers.NULL<Pointer<float>>(), boxed_nfr, null) < 0)
                return -1;
            inout_n_samps.Val = (uint)boxed_n_samps.Val;
            nfr = boxed_nfr.Val;

            if (acmod.n_mfc_alloc < nfr + 1)
            {
                acmod.mfc_buf = CKDAlloc.ckd_calloc_2d<float>((uint)(nfr + 1), (uint)FrontendInterface.fe_get_output_size(acmod.fe));
                acmod.n_mfc_alloc = nfr + 1;
            }

            acmod.n_mfc_frame = 0;
            acmod.mfc_outidx = 0;
            FrontendInterface.fe_start_utt(acmod.fe);

            boxed_n_samps.Val = (int)inout_n_samps.Val;
            boxed_nfr.Val = nfr;
            if (FrontendInterface.fe_process_frames(acmod.fe, inout_raw, boxed_n_samps,
                                  acmod.mfc_buf, boxed_nfr, null) < 0)
                return -1;
            nfr = boxed_nfr.Val;
            inout_n_samps.Val = (uint)boxed_n_samps.Val;

            BoxedValueInt boxed_ntail = new BoxedValueInt();
            FrontendInterface.fe_end_utt(acmod.fe, acmod.mfc_buf[nfr], boxed_ntail);
            ntail = boxed_ntail.Val;
            nfr += ntail;

            cepptr = acmod.mfc_buf;
            BoxedValue<Pointer<Pointer<float>>> boxed_cepptr = new BoxedValue<Pointer<Pointer<float>>>(cepptr);
            boxed_nfr.Val = nfr;
            nfr = acmod_process_full_cep(acmod, boxed_cepptr, boxed_nfr);
            cepptr = boxed_cepptr.Val;
            nfr = boxed_nfr.Val;
            acmod.n_mfc_frame = 0;
            return nfr;
        }

        /**
         * Process MFCCs that are in the internal buffer into features.
         */
        internal static int acmod_process_mfcbuf(acmod_t acmod)
        {
            // LOGAN OPT lots of boxing and unboxing here
            BoxedValue<Pointer<Pointer<float>>> mfcptr = new BoxedValue<Pointer<Pointer<float>>>();
            BoxedValueInt ncep = new BoxedValueInt();
            BoxedValueInt ncep1 = new BoxedValueInt();

            ncep.Val = acmod.n_mfc_frame;
            /* Also do this in two parts because of the circular mfc_buf. */
            if (acmod.mfc_outidx + ncep.Val > acmod.n_mfc_alloc)
            {
                ncep1.Val = acmod.n_mfc_alloc - acmod.mfc_outidx;
                int saved_state = acmod.state;

                /* Make sure we don't end the utterance here. */
                if (acmod.state == acmod_state_e.ACMOD_ENDED)
                    acmod.state = acmod_state_e.ACMOD_PROCESSING;
                mfcptr.Val = acmod.mfc_buf + acmod.mfc_outidx;
                ncep1.Val = acmod_process_cep(acmod, mfcptr, ncep1, 0);
                /* It's possible that not all available frames were filled. */
                ncep.Val -= ncep1.Val;
                acmod.n_mfc_frame -= ncep1.Val;
                acmod.mfc_outidx += ncep1.Val;
                acmod.mfc_outidx %= acmod.n_mfc_alloc;
                /* Restore original state (could this really be the end) */
                acmod.state = checked((byte)saved_state);
            }
            mfcptr.Val = acmod.mfc_buf + acmod.mfc_outidx;
            ncep.Val = acmod_process_cep(acmod, mfcptr, ncep, 0);
            acmod.n_mfc_frame -= ncep.Val;
            acmod.mfc_outidx += ncep.Val;
            acmod.mfc_outidx %= acmod.n_mfc_alloc;
            return ncep.Val;
        }

        internal static int acmod_process_raw(acmod_t acmod,
                          BoxedValue<Pointer<short>> inout_raw,
                          BoxedValueUInt inout_n_samps,
                          int full_utt)
        {
            int ncep;
            BoxedValueInt boxed_n_samps = new BoxedValueInt();
            BoxedValueInt out_frameidx = new BoxedValueInt();
            Pointer<short> prev_audio_inptr;

            /* If this is a full utterance, process it all at once. */
            if (full_utt != 0)
                return acmod_process_full_raw(acmod, inout_raw, inout_n_samps);

            /* Append MFCCs to the end of any that are previously in there
             * (in practice, there will probably be none) */
            if (inout_n_samps != null && inout_n_samps.Val != 0)
            {
                int inptr;
                int processed_samples;

                prev_audio_inptr = inout_raw.Val;
                /* Total number of frames available. */
                ncep = acmod.n_mfc_alloc - acmod.n_mfc_frame;
                /* Where to start writing them (circular buffer) */
                inptr = (acmod.mfc_outidx + acmod.n_mfc_frame) % acmod.n_mfc_alloc;

                /* Write them in two (or more) parts if there is wraparound. */
                while (inptr + ncep > acmod.n_mfc_alloc)
                {
                    int ncep1 = acmod.n_mfc_alloc - inptr;
                    BoxedValueInt boxed_ncep1 = new BoxedValueInt(ncep1);
                    boxed_n_samps.Val = checked((int)inout_n_samps.Val);
                    if (FrontendInterface.fe_process_frames(acmod.fe, inout_raw, boxed_n_samps,
                                          acmod.mfc_buf + inptr, boxed_ncep1, out_frameidx) < 0)
                    {
                        ncep1 = boxed_ncep1.Val;
                        inout_n_samps.Val = checked((uint)boxed_n_samps.Val);
                        return -1;
                    }
                    ncep1 = boxed_ncep1.Val;
                    inout_n_samps.Val = checked((uint)boxed_n_samps.Val);

                    if (out_frameidx.Val > 0)
                        acmod.utt_start_frame = out_frameidx.Val;

                    processed_samples = inout_raw.Val - prev_audio_inptr;
                    if (processed_samples + acmod.rawdata_pos < acmod.rawdata_size)
                    {
                        prev_audio_inptr.MemCopyTo(acmod.rawdata + acmod.rawdata_pos, processed_samples);
                        acmod.rawdata_pos += processed_samples;
                    }
                    prev_audio_inptr = inout_raw.Val;

                    /* ncep1 now contains the number of frames actually
                     * processed.  This is a good thing, but it means we
                     * actually still might have some room left at the end of
                     * the buffer, hence the while loop.  Unfortunately it
                     * also means that in the case where we are really
                     * actually done, we need to get out totally, hence the
                     * goto. */
                    acmod.n_mfc_frame += ncep1;
                    ncep -= ncep1;
                    inptr += ncep1;
                    inptr %= acmod.n_mfc_alloc;
                    if (ncep1 == 0)
                        goto alldone;
                }

                SphinxAssert.assert(inptr + ncep <= acmod.n_mfc_alloc);
                boxed_n_samps.Val = checked((int)inout_n_samps.Val);
                BoxedValueInt boxed_ncep = new BoxedValueInt(ncep);
                if (FrontendInterface.fe_process_frames(acmod.fe, inout_raw, boxed_n_samps,
                                      acmod.mfc_buf + inptr, boxed_ncep, out_frameidx) < 0)
                {
                    inout_n_samps.Val = checked((uint)boxed_n_samps.Val);
                    ncep = boxed_ncep.Val;
                    return -1;
                }
                inout_n_samps.Val = checked((uint)boxed_n_samps.Val);
                ncep = boxed_ncep.Val;

                if (out_frameidx.Val > 0)
                    acmod.utt_start_frame = out_frameidx.Val;


                processed_samples = inout_raw.Val - prev_audio_inptr;
                if (processed_samples + acmod.rawdata_pos < acmod.rawdata_size)
                {
                    prev_audio_inptr.MemCopyTo(acmod.rawdata + acmod.rawdata_pos, processed_samples);
                    acmod.rawdata_pos += processed_samples;
                }
                prev_audio_inptr = inout_raw.Val;
                acmod.n_mfc_frame += ncep;
                alldone:
                ;
            }

            /* Hand things off to acmod_process_cep. */
            return acmod_process_mfcbuf(acmod);
        }

        internal static int acmod_process_cep(acmod_t acmod,
                          BoxedValue<Pointer<Pointer<float>>> inout_cep,
                          BoxedValueInt inout_n_frames,
                          int full_utt)
        {
            int nfeat, ncep, inptr;
            int orig_n_frames;

            /* If this is a full utterance, process it all at once. */
            if (full_utt != 0)
                return acmod_process_full_cep(acmod, inout_cep, inout_n_frames);
            
            /* Maximum number of frames we're going to generate. */
            orig_n_frames = ncep = nfeat = inout_n_frames.Val;

            /* FIXME: This behaviour isn't guaranteed... */
            if (acmod.state == acmod_state_e.ACMOD_ENDED)
                nfeat += Feature.feat_window_size(acmod.fcb);
            else if (acmod.state == acmod_state_e.ACMOD_STARTED)
                nfeat -= Feature.feat_window_size(acmod.fcb);

            /* Clamp number of features to fit available space. */
            if (nfeat > acmod.n_feat_alloc - acmod.n_feat_frame)
            {
                /* Grow it as needed - we have to grow it at the end of an
                 * utterance because we can't return a short read there. */
                if (acmod.grow_feat != 0 || acmod.state == acmod_state_e.ACMOD_ENDED)
                    acmod_grow_feat_buf(acmod, acmod.n_feat_alloc + nfeat);
                else
                    ncep -= (nfeat - (acmod.n_feat_alloc - acmod.n_feat_frame));
            }

            /* Where to start writing in the feature buffer. */
            if (acmod.grow_feat != 0)
            {
                /* Grow to avoid wraparound if grow_feat == TRUE. */
                inptr = acmod.feat_outidx + acmod.n_feat_frame;
                while (inptr + nfeat >= acmod.n_feat_alloc)
                    acmod_grow_feat_buf(acmod, acmod.n_feat_alloc * 2);
            }
            else
            {
                inptr = (acmod.feat_outidx + acmod.n_feat_frame) % acmod.n_feat_alloc;
            }


            /* FIXME: we can't split the last frame drop properly to be on the bounary,
             *        so just return
             */
            if (inptr + nfeat > acmod.n_feat_alloc && acmod.state == acmod_state_e.ACMOD_ENDED)
            {
                inout_n_frames.Val -= ncep;
                inout_cep.Val += ncep;
                return 0;
            }

            /* Write them in two parts if there is wraparound. */
            if (inptr + nfeat > acmod.n_feat_alloc)
            {
                BoxedValueInt ncep1 = new BoxedValueInt(acmod.n_feat_alloc - inptr);

                /* Make sure we don't end the utterance here. */
                nfeat = Feature.feat_s2mfc2feat_live(acmod.fcb, inout_cep.Val,
                                             ncep1,
                                             (acmod.state == acmod_state_e.ACMOD_STARTED) ? 1 : 0,
                                             0,
                                             acmod.feat_buf + inptr);
                if (nfeat < 0)
                    return -1;
                /* Move the output feature pointer forward. */
                acmod.n_feat_frame += nfeat;
                SphinxAssert.assert(acmod.n_feat_frame <= acmod.n_feat_alloc);
                inptr += nfeat;
                inptr %= acmod.n_feat_alloc;
                /* Move the input feature pointers forward. */
                inout_n_frames.Val -= ncep1.Val;
                inout_cep.Val += ncep1.Val;
                ncep -= ncep1.Val;
            }

            BoxedValueInt boxed_ncep = new BoxedValueInt(ncep);
            nfeat = Feature.feat_s2mfc2feat_live(acmod.fcb, inout_cep.Val,
                                         boxed_ncep,
                                         (acmod.state == acmod_state_e.ACMOD_STARTED) ? 1 : 0,
                                         (acmod.state == acmod_state_e.ACMOD_ENDED) ? 1 : 0,
                                         acmod.feat_buf + inptr);
            ncep = boxed_ncep.Val;

            if (nfeat < 0)
                return -1;
            acmod.n_feat_frame += nfeat;
            SphinxAssert.assert(acmod.n_feat_frame <= acmod.n_feat_alloc);
            /* Move the input feature pointers forward. */
            inout_n_frames.Val -= ncep;
            inout_cep.Val += ncep;
            if (acmod.state == acmod_state_e.ACMOD_STARTED)
                acmod.state = acmod_state_e.ACMOD_PROCESSING;

            return orig_n_frames - inout_n_frames.Val;
        }

        internal static int acmod_process_feat(acmod_t acmod,
                           Pointer<Pointer<float>> feats)
        {
            int i, inptr;

            if (acmod.n_feat_frame == acmod.n_feat_alloc)
            {
                if (acmod.grow_feat != 0)
                    acmod_grow_feat_buf(acmod, acmod.n_feat_alloc * 2);
                else
                    return 0;
            }

            if (acmod.grow_feat != 0)
            {
                /* Grow to avoid wraparound if grow_feat == TRUE. */
                inptr = acmod.feat_outidx + acmod.n_feat_frame;
                while (inptr + 1 >= acmod.n_feat_alloc)
                    acmod_grow_feat_buf(acmod, acmod.n_feat_alloc * 2);
            }
            else
            {
                inptr = (acmod.feat_outidx + acmod.n_feat_frame) % acmod.n_feat_alloc;
            }
            for (i = 0; i < Feature.feat_dimension1(acmod.fcb); ++i)
                feats[i].MemCopyTo(acmod.feat_buf[inptr][i], (int)Feature.feat_dimension2(acmod.fcb, i)); // LOGAN watch out, this is a memcopy of pointers
            ++acmod.n_feat_frame;
            SphinxAssert.assert(acmod.n_feat_frame <= acmod.n_feat_alloc);

            return 1;
        }

        internal static int acmod_read_senfh_header(acmod_t acmod)
        {
            Pointer<Pointer<byte>> name;
            Pointer<Pointer<byte>> val;
            int swap;
            int i;

            BoxedValue<Pointer<Pointer<byte>>> boxed_argname = new BoxedValue<Pointer<Pointer<byte>>>();
            BoxedValue<Pointer<Pointer<byte>>> boxed_argval = new BoxedValue<Pointer<Pointer<byte>>>();
            if (BinaryIO.bio_readhdr(acmod.insenfh, boxed_argname, boxed_argval, out swap, acmod.logger) < 0)
                return -1;

            name = boxed_argname.Val;
            val = boxed_argval.Val;

            for (i = 0; name[i].IsNonNull; ++i)
            {
                if (cstring.strcmp(name[i], cstring.ToCString("n_sen")) == 0)
                {
                    if (cstring.atoi(val[i]) != BinaryModelDef.bin_mdef_n_sen(acmod.mdef))
                    {
                        acmod.logger.E_ERROR(string.Format("Number of senones in senone file ({0}) does not match mdef ({1})\n", cstring.atoi(val[i]),
                                BinaryModelDef.bin_mdef_n_sen(acmod.mdef)));
                        return -1;
                    }
                }

                if (cstring.strcmp(name[i], cstring.ToCString("logbase")) == 0)
                {
                    if (Math.Abs(StringFuncs.atof_c(val[i]) - LogMath.logmath_get_base(acmod.lmath)) > 0.001)
                    {
                        acmod.logger.E_ERROR(string.Format("Logbase in senone file ({0}) does not match acmod ({1})\n", StringFuncs.atof_c(val[i]),
                                LogMath.logmath_get_base(acmod.lmath)));
                        return -1;
                    }
                }
            }

            acmod.insen_swap = checked((byte)swap);
            return 0;
        }

        internal static int acmod_set_insenfh(acmod_t acmod, FILE senfh)
        {
            acmod.insenfh = senfh;
            if (senfh == null)
            {
                acmod.n_feat_frame = 0;
                acmod.compallsen = CommandLine.cmd_ln_boolean_r(acmod.config, cstring.ToCString("-compallsen"));
                return 0;
            }
            acmod.compallsen = 1;
            return acmod_read_senfh_header(acmod);
        }

        internal static int acmod_rewind(acmod_t acmod)
        {
            /* If the feature buffer is circular, this is not possible. */
            if (acmod.output_frame > acmod.n_feat_alloc)
            {
                acmod.logger.E_ERROR(string.Format("Circular feature buffer cannot be rewound (output frame {0}, alloc {1})\n", acmod.output_frame, acmod.n_feat_alloc));
                return -1;
            }

            /* Frames consumed + frames available */
            acmod.n_feat_frame = acmod.output_frame + acmod.n_feat_frame;

            /* Reset output pointers. */
            acmod.feat_outidx = 0;
            acmod.output_frame = 0;
            acmod.senscr_frame = -1;
            acmod.mgau.frame_idx = 0;

            return 0;
        }

        internal static int acmod_advance(acmod_t acmod)
        {
            /* Advance the output pointers. */
            if (++acmod.feat_outidx == acmod.n_feat_alloc)
                acmod.feat_outidx = 0;
            --acmod.n_feat_frame;
            ++acmod.mgau.frame_idx;

            return ++acmod.output_frame;
        }

        /**
         * Internal version, used for reading previous frames in acmod_score()
         */
        internal static int acmod_read_scores_internal(acmod_t acmod)
        {
            FILE senfh = acmod.insenfh;

            if (acmod.n_feat_frame == acmod.n_feat_alloc)
            {
                if (acmod.grow_feat != 0)
                    acmod_grow_feat_buf(acmod, acmod.n_feat_alloc * 2);
                else
                    return 0;
            }

            if (senfh == null)
                return -1;

            Stream fileStream = senfh.FileStream;
            using (BinaryReader reader = new BinaryReader(fileStream))
            {
                acmod.n_senone_active = reader.ReadInt16();
                short[] senones = new short[acmod.n_senone_active];
                if (acmod.n_senone_active == BinaryModelDef.bin_mdef_n_sen(acmod.mdef))
                {
                    for (int c = 0; c < acmod.n_senone_active; c++)
                        acmod.senone_scores[c] = reader.ReadInt16();
                }
                else
                {
                    byte[] active_senones = reader.ReadBytes(acmod.n_senone_active);
                    acmod.senone_active.MemCopyFrom(active_senones, 0, acmod.n_senone_active); // OPT: Can save a memcopy by just reading directly from the stream to the target

                    int i, n;
                    for (i = 0, n = 0; i < acmod.n_senone_active; ++i)
                    {
                        int j, sen = n + acmod.senone_active[i];
                        for (j = n + 1; j < sen; ++j)
                            acmod.senone_scores[j] = SENSCR_DUMMY;
                        acmod.senone_scores[sen] = reader.ReadInt16();
                        n = sen;
                    }

                    n++;
                    while (n < BinaryModelDef.bin_mdef_n_sen(acmod.mdef))
                        acmod.senone_scores[n++] = SENSCR_DUMMY;
                }

                return 1;
            }
        }

        internal static int acmod_read_scores(acmod_t acmod)
        {
            int inptr, rv;

            if (acmod.grow_feat != 0)
            {
                /* Grow to avoid wraparound if grow_feat == TRUE. */
                inptr = acmod.feat_outidx + acmod.n_feat_frame;
                /* Has to be +1, otherwise, next time acmod_advance() is
                 * called, this will wrap around. */
                while (inptr + 1 >= acmod.n_feat_alloc)
                    acmod_grow_feat_buf(acmod, acmod.n_feat_alloc * 2);
            }
            else
            {
                inptr = (acmod.feat_outidx + acmod.n_feat_frame) %
                        acmod.n_feat_alloc;
            }

            if ((rv = acmod_read_scores_internal(acmod)) != 1)
                return rv;

            /* Set acmod.senscr_frame appropriately so that these scores
               get reused below in acmod_score(). */
            acmod.senscr_frame = acmod.output_frame + acmod.n_feat_frame;

            acmod.logger.E_DEBUG(string.Format("Frame {0} has {1} active states\n",
                    acmod.senscr_frame, acmod.n_senone_active));

            /* Increment the "feature frame counter" and record the file
             * position for the relevant frame in the (possibly circular)
             * buffer. */
            ++acmod.n_feat_frame;
            acmod.framepos[inptr] = acmod.insenfh.ftell();

            return 1;
        }

        internal static int calc_frame_idx(acmod_t acmod, BoxedValueInt inout_frame_idx)
        {
            int frame_idx;

            /* Calculate the absolute frame index to be scored. */
            if (inout_frame_idx == null)
                frame_idx = acmod.output_frame;
            else if (inout_frame_idx.Val < 0)
                frame_idx = acmod.output_frame + 1 + inout_frame_idx.Val;
            else
                frame_idx = inout_frame_idx.Val;

            return frame_idx;
        }

        internal static int calc_feat_idx(acmod_t acmod, int frame_idx)
        {
            int n_backfr, feat_idx;

            n_backfr = acmod.n_feat_alloc - acmod.n_feat_frame;
            if (frame_idx < 0 || acmod.output_frame - frame_idx > n_backfr)
            {
                acmod.logger.E_ERROR(string.Format("Frame {0} outside queue of {1} frames, {2} alloc ({3} > {4}), cannot score\n", frame_idx, acmod.n_feat_frame,
                        acmod.n_feat_alloc, acmod.output_frame - frame_idx,
                        n_backfr));
                return -1;
            }

            /* Get the index in feat_buf/framepos of the frame to be scored. */
            feat_idx = (acmod.feat_outidx + frame_idx - acmod.output_frame) %
                       acmod.n_feat_alloc;
            if (feat_idx < 0)
                feat_idx += acmod.n_feat_alloc;

            return feat_idx;
        }

        internal static Pointer<Pointer<float>> acmod_get_frame(acmod_t acmod, BoxedValueInt inout_frame_idx)
        {
            int frame_idx, feat_idx;

            /* Calculate the absolute frame index requested. */
            frame_idx = calc_frame_idx(acmod, inout_frame_idx);

            /* Calculate position of requested frame in circular buffer. */
            if ((feat_idx = calc_feat_idx(acmod, frame_idx)) < 0)
                return PointerHelpers.NULL<Pointer<float>>();

            if (inout_frame_idx != null)
                inout_frame_idx.Val = frame_idx;

            return acmod.feat_buf[feat_idx];
        }

        internal static short[] acmod_score(acmod_t acmod, BoxedValueInt inout_frame_idx)
        {
            int frame_idx, feat_idx;

            /* Calculate the absolute frame index to be scored. */
            frame_idx = calc_frame_idx(acmod, inout_frame_idx);

            /* If all senones are being computed, or we are using a senone file,
               then we can reuse existing scores. */
            if ((acmod.compallsen != 0 || acmod.insenfh != null)
                && frame_idx == acmod.senscr_frame)
            {
                if (inout_frame_idx != null)
                    inout_frame_idx.Val = frame_idx;
                return acmod.senone_scores;
            }

            /* Calculate position of requested frame in circular buffer. */
            if ((feat_idx = calc_feat_idx(acmod, frame_idx)) < 0)
                return null;

            /*
             * If there is an input senone file locate the appropriate frame and read
             * it.
             */
            if (acmod.insenfh != null)
            {
                acmod.insenfh.fseek(acmod.framepos[feat_idx], FILE.SEEK_SET);
                if (acmod_read_scores_internal(acmod) < 0)
                    return null;
            }
            else
            {
                /* Build active senone list. */
                acmod_flags2list(acmod);

                /* Generate scores for the next available frame */
                acmod.mgau.vt.frame_eval(acmod.mgau,
                                   acmod.senone_scores,
                                   acmod.senone_active,
                                   acmod.n_senone_active,
                                   acmod.feat_buf[feat_idx],
                                   frame_idx,
                                   acmod.compallsen,
                                   acmod.logger);
            }

            if (inout_frame_idx != null)
                inout_frame_idx.Val = frame_idx;
            acmod.senscr_frame = frame_idx;

            /* Dump scores to the senone dump file if one exists. */
            // LOGAN cut this part out
            //if (acmod.senfh != null)
            //{
            //    if (acmod_write_scores(acmod, acmod.n_senone_active,
            //                           acmod.senone_active,
            //                           acmod.senone_scores,
            //                           acmod.senfh) < 0)
            //        return PointerHelpers.NULL<short>();
            //    Logger.E_DEBUG(string.Format("Frame {0} has {1} active states\n", frame_idx,
            //            acmod.n_senone_active));
            //}

            return acmod.senone_scores;
        }

        internal static int acmod_best_score(Pointer<acmod_t> acmod, BoxedValueInt out_best_senid)
        {
            int i, best;

            best = SENSCR_DUMMY;
            if (acmod.Deref.compallsen != 0)
            {
                for (i = 0; i < BinaryModelDef.bin_mdef_n_sen(acmod.Deref.mdef); ++i)
                {
                    if (acmod.Deref.senone_scores[i] < best)
                    {
                        best = acmod.Deref.senone_scores[i];
                        out_best_senid.Val = i;
                    }
                }
            }
            else
            {
                short[] senscr = acmod.Deref.senone_scores;
                int senscr_ptr = 0;
                for (i = 0; i < acmod.Deref.n_senone_active; ++i)
                {
                    senscr_ptr += acmod.Deref.senone_active[i];
                    if (senscr[senscr_ptr] < best)
                    {
                        best = senscr[senscr_ptr];
                        out_best_senid.Val = i;
                    }
                }
            }
            return best;
        }


        internal static void acmod_clear_active(acmod_t acmod)
        {
            if (acmod.compallsen != 0)
                return;
            BitVector.bitvec_clear_all(acmod.senone_active_vec, BinaryModelDef.bin_mdef_n_sen(acmod.mdef));
            acmod.n_senone_active = 0;
        }
        
        internal static void acmod_activate_hmm(acmod_t acmod, hmm_t _hmm)
        {
            int i;

            if (acmod.compallsen != 0)
                return;

            // LOGAN OPT The C code unrolled this loop a little bit, treating 3 and 5 as special cases

            if (HiddenMarkovModel.hmm_is_mpx(_hmm) != 0)
            {
                        for (i = 0; i < HiddenMarkovModel.hmm_n_emit_state(_hmm); ++i)
                        {
                    if (HiddenMarkovModel.hmm_mpx_ssid(_hmm, i) != BinaryModelDef.BAD_SSID)
                        BitVector.bitvec_set(acmod.senone_active_vec, HiddenMarkovModel.hmm_mpx_senid(_hmm, i));
                        }
                }
            else
            {
                        for (i = 0; i < HiddenMarkovModel.hmm_n_emit_state(_hmm); ++i)
                        {
                    BitVector.bitvec_set(acmod.senone_active_vec, HiddenMarkovModel.hmm_nonmpx_senid(_hmm, i));
                        }
                }
            }

        internal static int acmod_flags2list(acmod_t acmod)
        {
            int w, l, n, b, total_dists, total_words, extra_bits;
            Pointer<uint> flagptr;

            total_dists = BinaryModelDef.bin_mdef_n_sen(acmod.mdef);
            if (acmod.compallsen != 0)
            {
                acmod.n_senone_active = total_dists;
                return total_dists;
            }
            total_words = total_dists / BitVector.BITVEC_BITS;
            extra_bits = total_dists % BitVector.BITVEC_BITS;
            w = n = l = 0;
            for (flagptr = acmod.senone_active_vec; w < total_words; ++w, ++flagptr)
            {
                if (flagptr.Deref == 0)
                    continue;
                for (b = 0; b < BitVector.BITVEC_BITS; ++b)
                {
                    if ((flagptr.Deref & (1UL << b)) != 0)
                    {
                        int sen = w * BitVector.BITVEC_BITS + b;
                        int delta = sen - l;
                        /* Handle excessive deltas "lossily" by adding a few
                           extra senones to bridge the gap. */
                        while (delta > 255)
                        {
                            acmod.senone_active[n++] = 255;
                            delta -= 255;
                        }
                        acmod.senone_active[n++] = checked((byte)delta);
                        l = sen;
                    }
                }
            }

            for (b = 0; b < extra_bits; ++b)
            {
                if ((flagptr.Deref & (1UL << b)) != 0)
                {
                    int sen = w * BitVector.BITVEC_BITS + b;
                    int delta = sen - l;
                    /* Handle excessive deltas "lossily" by adding a few
                       extra senones to bridge the gap. */
                    while (delta > 255)
                    {
                        acmod.senone_active[n++] = 255;
                        delta -= 255;
                    }
                    acmod.senone_active[n++] = checked((byte)delta);
                    l = sen;
                }
            }

            acmod.n_senone_active = n;
            //Logger.E_DEBUG(string.Format("acmod_flags2list: {0} active in frame {1}\n",
            //        acmod.Deref.n_senone_active, acmod.Deref.output_frame));
            return n;
        }

        internal static int acmod_stream_offset(acmod_t acmod)
        {
            return acmod.utt_start_frame;
        }
    }
}
