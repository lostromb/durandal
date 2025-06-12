using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class FrontendInterface
    {
        internal static int fe_parse_general_params(cmd_ln_t config, fe_t fet)
        {
            int j, frate;

            fet.config = config;
            fet.sampling_rate = (float)CommandLine.cmd_ln_float_r(config, cstring.ToCString("-samprate"));
            frate = (int)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-frate"));
            if (frate > short.MaxValue || frate > fet.sampling_rate || frate < 1)
            {
                fet.logger.E_ERROR
                    (string.Format("Frame rate {0} can not be bigger than sample rate {1}\n",
                     frate, fet.sampling_rate));
                return -1;
            }

            fet.frame_rate = (short)frate;
            if (CommandLine.cmd_ln_boolean_r(config, cstring.ToCString("-dither")) != 0)
            {
                fet.dither = 1;
                fet.dither_seed = (int)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-seed"));
            }
            fet.swap = cstring.strcmp(cstring.ToCString("little"), CommandLine.cmd_ln_str_r(config, cstring.ToCString("-input_endian"))) == 0 ? (byte)0 : (byte)1;
            fet.window_length = (float)CommandLine.cmd_ln_float_r(config, cstring.ToCString("-wlen"));
            fet.pre_emphasis_alpha = (float)CommandLine.cmd_ln_float_r(config, cstring.ToCString("-alpha"));

            fet.num_cepstra = (byte)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-ncep"));
            fet.fft_size = (short)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-nfft"));

            /* Check FFT size, compute FFT order (log_2(n)) */
            for (j = fet.fft_size, fet.fft_order = 0; j > 1; j >>= 1, fet.fft_order++)
            {
                if (((j % 2) != 0) || (fet.fft_size <= 0))
                {
                    fet.logger.E_ERROR(string.Format("fft: number of points must be a power of 2 (is {0})\n",
                            fet.fft_size));
                    return -1;
                }
            }
            /* Verify that FFT size is greater or equal to window length. */
            if (fet.fft_size < (int)(fet.window_length * fet.sampling_rate))
            {
                fet.logger.E_ERROR(string.Format("FFT: Number of points must be greater or equal to frame size ({0} samples)\n",
                        (int)(fet.window_length * fet.sampling_rate)));
                return -1;
            }

            fet.pre_speech = (short)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-vad_prespeech"));
            fet.post_speech = (short)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-vad_postspeech"));
            fet.start_speech = (short)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-vad_startspeech"));
            fet.vad_threshold = (float)CommandLine.cmd_ln_float_r(config, cstring.ToCString("-vad_threshold"));

            fet.remove_dc = CommandLine.cmd_ln_boolean_r(config, cstring.ToCString("-remove_dc"));
            fet.remove_noise = CommandLine.cmd_ln_boolean_r(config, cstring.ToCString("-remove_noise"));
            fet.remove_silence = CommandLine.cmd_ln_boolean_r(config, cstring.ToCString("-remove_silence"));

            if (0 == cstring.strcmp(CommandLine.cmd_ln_str_r(config, cstring.ToCString("-transform")), cstring.ToCString("dct")))
                fet.transform = Frontend.DCT_II;
            else if (0 == cstring.strcmp(CommandLine.cmd_ln_str_r(config, cstring.ToCString("-transform")), cstring.ToCString("legacy")))
                fet.transform = Frontend.LEGACY_DCT;
            else if (0 == cstring.strcmp(CommandLine.cmd_ln_str_r(config, cstring.ToCString("-transform")), cstring.ToCString("htk")))
                fet.transform = Frontend.DCT_HTK;
            else
            {
                fet.logger.E_ERROR("Invalid transform type (values are 'dct', 'legacy', 'htk')\n");
                return -1;
            }

            if (CommandLine.cmd_ln_boolean_r(config, cstring.ToCString("-logspec")) != 0)
                fet.log_spec = Frontend.RAW_LOG_SPEC;
            if (CommandLine.cmd_ln_boolean_r(config, cstring.ToCString("-smoothspec")) != 0)
                fet.log_spec = Frontend.SMOOTH_LOG_SPEC;

            return 0;
        }

        internal static int fe_parse_melfb_params(cmd_ln_t config, fe_t fet, melfb_t mel)
        {
            mel.sampling_rate = fet.sampling_rate;
            mel.fft_size = fet.fft_size;
            mel.num_cepstra = fet.num_cepstra;
            mel.num_filters = (int)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-nfilt"));

            if (fet.log_spec != 0)
                fet.feature_dimension = (byte)mel.num_filters;
            else
                fet.feature_dimension = fet.num_cepstra;

            mel.upper_filt_freq = (float)CommandLine.cmd_ln_float_r(config, cstring.ToCString("-upperf"));
            mel.lower_filt_freq = (float)CommandLine.cmd_ln_float_r(config, cstring.ToCString("-lowerf"));

            mel.doublewide = CommandLine.cmd_ln_boolean_r(config, cstring.ToCString("-doublebw"));

            mel.warp_type = CommandLine.cmd_ln_str_r(config, cstring.ToCString("-warp_type"));
            mel.warp_params = CommandLine.cmd_ln_str_r(config, cstring.ToCString("-warp_params"));
            mel.lifter_val = (int)CommandLine.cmd_ln_int_r(config, cstring.ToCString("-lifter"));

            mel.unit_area = CommandLine.cmd_ln_boolean_r(config, cstring.ToCString("-unit_area"));
            mel.round_filters = CommandLine.cmd_ln_boolean_r(config, cstring.ToCString("-round_filters"));

            if (Warp.fe_warp_set(mel, mel.warp_type, fet.logger) != Frontend.FE_SUCCESS)
            {
                fet.logger.E_ERROR("Failed to initialize the warping function.\n");
                return -1;
            }
            Warp.fe_warp_set_parameters(mel, mel.warp_params, mel.sampling_rate, fet.logger);
            return 0;
        }

        internal static fe_t fe_init_auto_r(cmd_ln_t config, SphinxLogger logger)
        {
            int prespch_frame_len;
            fe_t returnVal = new fe_t();
            returnVal.logger = logger;
            
            /* transfer params to front end */
            if (fe_parse_general_params(config, returnVal) < 0)
            {
                return null;
            }

            /* compute remaining fe parameters */
            /* We add 0.5 so approximate the float with the closest
             * integer. E.g., 2.3 is truncate to 2, whereas 3.7 becomes 4
             */
            returnVal.frame_shift = checked((short)(returnVal.sampling_rate / returnVal.frame_rate + 0.5));
            returnVal.frame_size = checked((short)(returnVal.window_length * returnVal.sampling_rate + 0.5));
            returnVal.pre_emphasis_prior = 0;

            fe_start_stream(returnVal);

            SphinxAssert.assert(returnVal.frame_shift > 1);

            if (returnVal.frame_size < returnVal.frame_shift)
            {
                logger.E_ERROR
                    (string.Format("Frame size {0} (-wlen) must be greater than frame shift {1} (-frate)\n",
                     returnVal.frame_size, returnVal.frame_shift));
                return null;
            }


            if (returnVal.frame_size > (returnVal.fft_size))
            {
                logger.E_ERROR
                    (string.Format("Number of FFT points has to be a power of 2 higher than {0}, it is {1}\n",
                     returnVal.frame_size, returnVal.fft_size));
                return null;
            }

            if (returnVal.dither != 0)
                fe_init_dither(returnVal.dither_seed, logger);

            /* establish buffers for overflow samps and hamming window */
            returnVal.overflow_samps = new short[returnVal.frame_size];
            returnVal.hamming_window = CKDAlloc.ckd_calloc<double>(returnVal.frame_size / 2);

            /* create hamming window */
            FrontendSignalProcessing.fe_create_hamming(returnVal.hamming_window, returnVal.frame_size);

            /* init and fill appropriate filter structure */
            returnVal.mel_fb = new melfb_t();
            returnVal.mel_fb.logger = logger;

            /* transfer params to mel fb */
            fe_parse_melfb_params(config, returnVal, returnVal.mel_fb);

            if (returnVal.mel_fb.upper_filt_freq > returnVal.sampling_rate / 2 + 1.0)
            {
                logger.E_ERROR(string.Format("Upper frequency {0} is higher than samprate/2 ({1})\n",
                    returnVal.mel_fb.upper_filt_freq, returnVal.sampling_rate / 2));
                return null;
            }

            FrontendSignalProcessing.fe_build_melfilters(returnVal.mel_fb);

            FrontendSignalProcessing.fe_compute_melcosine(returnVal.mel_fb);
            if (returnVal.remove_noise != 0 || returnVal.remove_silence != 0)
                returnVal.noise_stats = FrontendNoise.fe_init_noisestats(returnVal.mel_fb.num_filters);

            returnVal.vad_data = new vad_data_t();
            prespch_frame_len = returnVal.log_spec != Frontend.RAW_LOG_SPEC ? returnVal.num_cepstra : returnVal.mel_fb.num_filters;
            returnVal.vad_data.prespch_buf = FrontendPrespeechBuf.fe_prespch_init(returnVal.pre_speech + 1, prespch_frame_len, returnVal.frame_shift);

            /* Create temporary FFT, spectrum and mel-spectrum buffers. */
            /* FIXME: Gosh there are a lot of these. */
            returnVal.spch = new short[returnVal.frame_size];
            returnVal.frame = new double[returnVal.fft_size];
            returnVal.spec = new double[returnVal.fft_size];
            returnVal.mfspec = new double[returnVal.mel_fb.num_filters];

            /* create twiddle factors */
            returnVal.ccc = new double[returnVal.fft_size / 4];
            returnVal.sss = new double[returnVal.fft_size / 4];
            FrontendSignalProcessing.fe_create_twiddle(returnVal);

            // LOGAN removed
            //if (cmd_ln.cmd_ln_boolean_r(config, "-verbose")) {
            //    fe_print_current(fe);
            //}

            /*** Initialize the overflow buffers ***/
            fe_start_utt(returnVal);
            return returnVal;
        }

        internal static void fe_init_dither(int seed, SphinxLogger logger)
        {
            logger.E_INFO(string.Format("Using {0} as the seed.\n", seed));
            GenRand.genrand_seed((uint)seed);
        }

        internal static void fe_reset_vad_data(vad_data_t vad_data)
        {
            vad_data.in_speech = 0;
            vad_data.pre_speech_frames = 0;
            vad_data.post_speech_frames = 0;
            FrontendPrespeechBuf.fe_prespch_reset_cep(vad_data.prespch_buf);
        }

        internal static int fe_start_utt(fe_t fet)
        {
            fet.num_overflow_samps = 0;
            Arrays.MemSetShort(fet.overflow_samps, 0, fet.frame_size);
            fet.pre_emphasis_prior = 0;
            fe_reset_vad_data(fet.vad_data);
            return 0;
        }

        internal static void fe_start_stream(fe_t fet)
        {
            fet.num_processed_samps = 0;
            FrontendNoise.fe_reset_noisestats(fet.noise_stats);
        }

        internal static int fe_get_output_size(fe_t fet)
        {
            return (int)fet.feature_dimension;
        }

        internal static byte fe_get_vad_state(fe_t fet)
        {
            return fet.vad_data.in_speech;
        }

        internal static int fe_process_frames(fe_t fet,
                          BoxedValue<Pointer<short>> inout_spch,
                          BoxedValueInt inout_nsamps,
                          Pointer<Pointer<float>> buf_cep,
                          BoxedValueInt inout_nframes,
                          BoxedValueInt out_frameidx)
        {
            return fe_process_frames_ext(fet, inout_spch, inout_nsamps, buf_cep, inout_nframes, PointerHelpers.NULL<short>(), PointerHelpers.NULL<int>(), out_frameidx);
        }
   
        /**
         * Copy frames collected in prespeech buffer
         */
        internal static int fe_copy_from_prespch(fe_t fet, BoxedValueInt inout_nframes, Pointer<Pointer<float>> buf_cep, int outidx)
        {
            while ((inout_nframes.Val) > 0 && FrontendPrespeechBuf.fe_prespch_read_cep(fet.vad_data.prespch_buf, buf_cep[outidx]) > 0)
            {
                outidx++;
                (inout_nframes.Val)--;
            }
            return outidx;
        }

        /**
         * Update pointers after we processed a frame. A complex logic used in two places in fe_process_frames
         */
        internal static int fe_check_prespeech(fe_t fet, BoxedValueInt inout_nframes, Pointer<Pointer<float>> buf_cep, int outidx, BoxedValueInt out_frameidx, BoxedValueInt inout_nsamps, int orig_nsamps)
        {
            if (fet.vad_data.in_speech != 0)
            {
                if (FrontendPrespeechBuf.fe_prespch_ncep(fet.vad_data.prespch_buf) > 0)
                {
                    /* Previous frame triggered vad into speech state. Last frame is in the end of 
                       prespeech buffer, so overwrite it */
                    outidx = fe_copy_from_prespch(fet, inout_nframes, buf_cep, outidx);

                    /* Sets the start frame for the returned data so that caller can update timings */
                    if (out_frameidx != null)
                    {
                        out_frameidx.Val = checked((int)(fet.num_processed_samps + orig_nsamps - inout_nsamps.Val) / fet.frame_shift - fet.pre_speech);
                    }
                }
                else
                {
                    outidx++;
                    (inout_nframes.Val)--;
                }
            }
            /* Amount of data behind the original input which is still needed. */
            if (fet.num_overflow_samps > 0)
                fet.num_overflow_samps -= fet.frame_shift;

            return outidx;
        }

        internal static int fe_process_frames_ext(fe_t fet,
                          BoxedValue<Pointer<short>> inout_spch,
                          BoxedValueInt inout_nsamps,
                          Pointer<Pointer<float>> buf_cep,
                          BoxedValueInt inout_nframes,
                          Pointer<short> voiced_spch,
                          Pointer<int> voiced_spch_nsamps,
                          BoxedValueInt out_frameidx)
        {
            int outidx, n_overflow, orig_n_overflow;
            Pointer<short> orig_spch;
            int orig_nsamps;

            /* The logic here is pretty complex, please be careful with modifications */

            /* FIXME: Dump PCM data if needed */

            /* In the special case where there is no output buffer, return the
             * maximum number of frames which would be generated. */
            if (buf_cep.IsNull)
            {
                if (inout_nsamps.Val + fet.num_overflow_samps < (uint)fet.frame_size)
                    inout_nframes.Val = 0;
                else
                    inout_nframes.Val = (1 + ((inout_nsamps.Val + fet.num_overflow_samps - fet.frame_size)
                           / fet.frame_shift));
                if (fet.vad_data.in_speech == 0)
                    inout_nframes.Val += FrontendPrespeechBuf.fe_prespch_ncep(fet.vad_data.prespch_buf);
                return inout_nframes.Val;
            }

            if (out_frameidx != null)
                out_frameidx.Val = 0;

            /* Are there not enough samples to make at least 1 frame? */
            if (inout_nsamps.Val + fet.num_overflow_samps < (uint)fet.frame_size)
            {
                if (inout_nsamps.Val > 0)
                {
                    /* Append them to the overflow buffer. */
                    inout_spch.Val.MemCopyTo(fet.overflow_samps, fet.num_overflow_samps, (int)inout_nsamps.Val);
                    fet.num_overflow_samps = checked((short)(fet.num_overflow_samps + inout_nsamps.Val));
                    fet.num_processed_samps = checked((uint)(fet.num_processed_samps + inout_nsamps.Val));
                    inout_spch.Val += inout_nsamps.Val;
                    inout_nsamps.Val = 0;
                }
                /* We produced no frames of output, sorry! */
                inout_nframes.Val = 0;
                return 0;
            }

            /* Can't write a frame?  Then do nothing! */
            if (inout_nframes.Val < 1)
            {
                inout_nframes.Val = 0;
                return 0;
            }

            /* Index of output frame. */
            outidx = 0;

            /* Try to read from prespeech buffer */
            if (fet.vad_data.in_speech != 0 && FrontendPrespeechBuf.fe_prespch_ncep(fet.vad_data.prespch_buf) > 0)
            {
                outidx = fe_copy_from_prespch(fet, inout_nframes, buf_cep, outidx);
                if ((inout_nframes.Val) < 1)
                {
                    /* mfcc buffer is filled from prespeech buffer */
                    inout_nframes.Val = outidx;
                    return 0;
                }
            }

            /* Keep track of the original start of the buffer. */
            orig_spch = inout_spch.Val;
            orig_nsamps = inout_nsamps.Val;
            orig_n_overflow = fet.num_overflow_samps;

            /* Start processing, taking care of any incoming overflow. */
            if (fet.num_overflow_samps > 0)
            {
                int offset = fet.frame_size - fet.num_overflow_samps;
                /* Append start of spch to overflow samples to make a full frame. */
                inout_spch.Val.MemCopyTo(fet.overflow_samps, fet.num_overflow_samps, offset);
                FrontendSignalProcessing.fe_read_frame(fet, fet.overflow_samps.GetPointer(), fet.frame_size);
                /* Update input-output pointers and counters. */
                inout_spch.Val += offset;
                inout_nsamps.Val = inout_nsamps.Val - offset;
            }
            else
            {
                FrontendSignalProcessing.fe_read_frame(fet, inout_spch.Val, fet.frame_size);
                /* Update input-output pointers and counters. */
                inout_spch.Val += fet.frame_size;
                inout_nsamps.Val = inout_nsamps.Val - fet.frame_size;
            }

            FrontendSignalProcessing.fe_write_frame(fet, buf_cep[outidx], voiced_spch.IsNonNull ? 1 : 0);
            outidx = fe_check_prespeech(fet, inout_nframes, buf_cep, outidx, out_frameidx, inout_nsamps, orig_nsamps);

            /* Process all remaining frames. */
            while (inout_nframes.Val > 0 && inout_nsamps.Val >= (uint)fet.frame_shift)
            {
                FrontendSignalProcessing.fe_shift_frame(fet, inout_spch.Val, fet.frame_shift);
                FrontendSignalProcessing.fe_write_frame(fet, buf_cep[outidx], voiced_spch.IsNonNull ? 1 : 0);

                outidx = fe_check_prespeech(fet, inout_nframes, buf_cep, outidx, out_frameidx, inout_nsamps, orig_nsamps);

                /* Update input-output pointers and counters. */
                inout_spch.Val += fet.frame_shift;
                inout_nsamps.Val -= fet.frame_shift;
            }

            /* How many relevant overflow samples are there left? */
            if (fet.num_overflow_samps <= 0)
            {
                /* Maximum number of overflow samples past *inout_spch to save. */
                n_overflow = inout_nsamps.Val;
                if (n_overflow > fet.frame_shift)
                    n_overflow = fet.frame_shift;
                fet.num_overflow_samps = checked((short)(fet.frame_size - fet.frame_shift));
                /* Make sure this isn't an illegal read! */
                if (fet.num_overflow_samps > inout_spch.Val - orig_spch)
                    fet.num_overflow_samps = checked((short)(inout_spch.Val - orig_spch));
                fet.num_overflow_samps = checked((short)(fet.num_overflow_samps + n_overflow));
                if (fet.num_overflow_samps > 0)
                {
                    (inout_spch.Val - (fet.frame_size - fet.frame_shift)).MemCopyTo(fet.overflow_samps, 0, fet.num_overflow_samps);
                    /* Update the input pointer to cover this stuff. */
                    inout_spch.Val += n_overflow;
                    inout_nsamps.Val -= n_overflow;
                }
            }
            else
            {
                /* There is still some relevant data left in the overflow buffer. */
                /* Shift existing data to the beginning. */
                (fet.overflow_samps.GetPointer() + orig_n_overflow - fet.num_overflow_samps).MemMove(fet.num_overflow_samps - orig_n_overflow, fet.num_overflow_samps);
                // LOGAN TODO Check this memmove!
                //memmove(fet.overflow_samps, fet.overflow_samps + orig_n_overflow - fet.num_overflow_samps,  * sizeof(*fet.overflow_samps));
                /* Copy in whatever we had in the original speech buffer. */
                n_overflow = inout_spch.Val - orig_spch + inout_nsamps.Val;
                if (n_overflow > fet.frame_size - fet.num_overflow_samps)
                    n_overflow = fet.frame_size - fet.num_overflow_samps;
                orig_spch.MemCopyTo(fet.overflow_samps, fet.num_overflow_samps, n_overflow);
                fet.num_overflow_samps = checked((short)(fet.num_overflow_samps + n_overflow));
                /* Advance the input pointers. */
                if (n_overflow > inout_spch.Val - orig_spch)
                {
                    n_overflow -= (inout_spch.Val - orig_spch);
                    inout_spch.Val += n_overflow;
                    inout_nsamps.Val -= n_overflow;
                }
            }

            /* Finally update the frame counter with the number of frames
             * and global sample counter with number of samples we procesed */
            inout_nframes.Val = outidx; /* FIXME: Not sure why I wrote it this way... */
            fet.num_processed_samps = checked((uint)(fet.num_processed_samps + (orig_nsamps - inout_nsamps.Val)));

            return 0;
        }

        internal static int fe_end_utt(fe_t fet, Pointer<float> cepvector, BoxedValueInt nframes)
        {
            /* Process any remaining data, not very accurate for the VAD */
            nframes.Val = 0;
            if (fet.num_overflow_samps > 0)
            {
                FrontendSignalProcessing.fe_read_frame(fet, fet.overflow_samps.GetPointer(), fet.num_overflow_samps);
                FrontendSignalProcessing.fe_write_frame(fet, cepvector, 0);
                if (fet.vad_data.in_speech != 0)
                    nframes.Val = 1;
            }

            /* reset overflow buffers... */
            fet.num_overflow_samps = 0;

            return 0;
        }
    }
}
