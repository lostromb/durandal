using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class FrontendNoise
    {
        public const int SMOOTH_WINDOW = 4;
        public const double LAMBDA_POWER = 0.7;
        public const double LAMBDA_A = 0.995;
        public const double LAMBDA_B = 0.5;
        public const double LAMBDA_T = 0.85;
        public const double MU_T = 0.2;
        public const double MAX_GAIN = 20;
        public const double SLOW_PEAK_FORGET_FACTOR = 0.9995;
        public const double SLOW_PEAK_LEARN_FACTOR = 0.9;
        public const double SPEECH_VOLUME_RANGE = 8.0;

        internal static void fe_lower_envelope(noise_stats_t noise_stats, Pointer<double> buf, Pointer<double> floor_buf, int num_filt)
        {
            int i;

            for (i = 0; i < num_filt; i++)
            {
                if (buf[i] >= floor_buf[i])
                {
                    floor_buf[i] =
                        noise_stats.lambda_a * floor_buf[i] + noise_stats.comp_lambda_a * buf[i];
                }
                else
                {
                    floor_buf[i] =
                        noise_stats.lambda_b * floor_buf[i] + noise_stats.comp_lambda_b * buf[i];
                }
            }
        }

        /* update slow peaks, check if max signal level big enough compared to peak */
        internal static short fe_is_frame_quiet(noise_stats_t noise_stats, Pointer<double> buf, int num_filt)
        {
            int i;
            short is_quiet;
            double sum;
            double smooth_factor;

            sum = 0.0;
            for (i = 0; i < num_filt; i++)
            {
                sum += buf[i];
            }
            sum = Math.Log(sum);
            smooth_factor = (sum > noise_stats.slow_peak_sum) ? SLOW_PEAK_LEARN_FACTOR : SLOW_PEAK_FORGET_FACTOR;
            noise_stats.slow_peak_sum = noise_stats.slow_peak_sum * smooth_factor +
                                         sum * (1 - smooth_factor);

            is_quiet = noise_stats.slow_peak_sum - SPEECH_VOLUME_RANGE > sum ? (short)1 : (short)0;
            return is_quiet;
        }

        /* temporal masking */
        internal static void fe_temp_masking(noise_stats_t noise_stats, Pointer<double> buf, Pointer<double> peak, int num_filt)
        {
            double cur_in;
            int i;

            for (i = 0; i < num_filt; i++)
            {
                cur_in = buf[i];

                peak[i] *= noise_stats.lambda_t;
                if (buf[i] < noise_stats.lambda_t * peak[i])
                    buf[i] = peak[i] * noise_stats.mu_t;

                if (cur_in > peak[i])
                    peak[i] = cur_in;
            }
        }

        /* spectral weight smoothing */
        internal static void fe_weight_smooth(noise_stats_t noise_stats, Pointer<double> buf, Pointer<double> coefs, int num_filt)
        {
            int i, j;
            int l1, l2;
            double coef;

            for (i = 0; i < num_filt; i++)
            {
                l1 = ((i - SMOOTH_WINDOW) > 0) ? (i - SMOOTH_WINDOW) : 0;
                l2 = ((i + SMOOTH_WINDOW) <
                      (num_filt - 1)) ? (i + SMOOTH_WINDOW) : (num_filt - 1);

                coef = 0;
                for (j = l1; j <= l2; j++)
                {
                    coef += coefs[j];
                }
                buf[i] = buf[i] * (coef / (l2 - l1 + 1));

            }
        }

        internal static noise_stats_t fe_init_noisestats(int num_filters)
        {
            int i;
            noise_stats_t noise_stats = new noise_stats_t();

            noise_stats.power = CKDAlloc.ckd_calloc<double>(num_filters);
            noise_stats.noise = CKDAlloc.ckd_calloc<double>(num_filters);
            noise_stats.floor = CKDAlloc.ckd_calloc<double>(num_filters);
            noise_stats.peak = CKDAlloc.ckd_calloc<double>(num_filters);

            noise_stats.undefined = 1;
            noise_stats.num_filters = (uint)num_filters;

            noise_stats.lambda_power = LAMBDA_POWER;
            noise_stats.comp_lambda_power = 1 - LAMBDA_POWER;
            noise_stats.lambda_a = LAMBDA_A;
            noise_stats.comp_lambda_a = 1 - LAMBDA_A;
            noise_stats.lambda_b = LAMBDA_B;
            noise_stats.comp_lambda_b = 1 - LAMBDA_B;
            noise_stats.lambda_t = LAMBDA_T;
            noise_stats.mu_t = MU_T;
            noise_stats.max_gain = MAX_GAIN;
            noise_stats.inv_max_gain = 1.0 / MAX_GAIN;

            for (i = 1; i < 2 * SMOOTH_WINDOW + 1; i++)
            {
                noise_stats.smooth_scaling[i] = 1.0 / i;
            }

            return noise_stats;
        }

        internal static void fe_reset_noisestats(noise_stats_t noise_stats)
        {
            if (noise_stats != null)
                noise_stats.undefined = 1;
        }

        /**
         * For fixed point we are doing the computation in a fixlog domain,
         * so we have to add many processing cases.
         */
        internal static void fe_track_snr(fe_t fe, BoxedValueInt in_speech)
        {
            Pointer<double> signal;
            Pointer<double> gain;
            noise_stats_t noise_stats;
            double[] mfspec;
            int i, num_filts;
            short is_quiet;
            double lrt, snr;

            if (!(fe.remove_noise != 0 || fe.remove_silence != 0))
            {
                in_speech.Val = 1;
                return;
            }

            noise_stats = fe.noise_stats;
            mfspec = fe.mfspec;
            num_filts = (int)noise_stats.num_filters;

            signal = (Pointer<double>)CKDAlloc.ckd_calloc<double>(num_filts);

            if (noise_stats.undefined != 0)
            {
                noise_stats.slow_peak_sum = FixPoint.FIX2FLOAT(0);
                for (i = 0; i < num_filts; i++)
                {
                    noise_stats.power[i] = mfspec[i];
                    noise_stats.noise[i] = mfspec[i] / noise_stats.max_gain;
                    noise_stats.floor[i] = mfspec[i] / noise_stats.max_gain;
                    noise_stats.peak[i] = 0.0;
                }
                noise_stats.undefined = 0;
            }

            /* Calculate smoothed power */
            for (i = 0; i < num_filts; i++)
            {
                noise_stats.power[i] =
                    noise_stats.lambda_power * noise_stats.power[i] + noise_stats.comp_lambda_power * mfspec[i];
            }

            /* Noise estimation and vad decision */
            fe_lower_envelope(noise_stats, noise_stats.power, noise_stats.noise, num_filts);

            lrt = FixPoint.FLOAT2FIX(0.0);
            for (i = 0; i < num_filts; i++)
            {
                signal[i] = noise_stats.power[i] - noise_stats.noise[i];
                if (signal[i] < 1.0)
                    signal[i] = 1.0;
                snr = Math.Log(noise_stats.power[i] / noise_stats.noise[i]);
                if (snr > lrt)
                    lrt = snr;
            }
            is_quiet = fe_is_frame_quiet(noise_stats, signal, num_filts);

            if (fe.remove_silence != 0 && (lrt < fe.vad_threshold || is_quiet != 0))
            {
                in_speech.Val = 0;
            }
            else
            {
                in_speech.Val = 1;
            }

            fe_lower_envelope(noise_stats, signal, noise_stats.floor, num_filts);

            fe_temp_masking(noise_stats, signal, noise_stats.peak, num_filts);

            if (fe.remove_noise == 0)
            {
                /* no need for further calculations if noise cancellation disabled */
                return;
            }

            for (i = 0; i < num_filts; i++)
            {
                if (signal[i] < noise_stats.floor[i])
                    signal[i] = noise_stats.floor[i];
            }

            gain = (Pointer<double>)CKDAlloc.ckd_calloc<double>(num_filts);
            for (i = 0; i < num_filts; i++)
            {
                if (signal[i] < noise_stats.max_gain * noise_stats.power[i])
                    gain[i] = signal[i] / noise_stats.power[i];
                else
                    gain[i] = noise_stats.max_gain;
                if (gain[i] < noise_stats.inv_max_gain)
                    gain[i] = noise_stats.inv_max_gain;
            }

            /* Weight smoothing and time frequency normalization */
            fe_weight_smooth(noise_stats, mfspec.GetPointer(), gain, num_filts);
        }

        internal static void fe_vad_hangover(fe_t fe, Pointer<float> feat, int is_speech, int store_pcm)
        {
            if (fe.vad_data.in_speech == 0)
            {
                FrontendPrespeechBuf.fe_prespch_write_cep(fe.vad_data.prespch_buf, feat);
                if (store_pcm != 0)
                    FrontendPrespeechBuf.fe_prespch_write_pcm(fe.vad_data.prespch_buf, fe.spch);
            }

            /* track vad state and deal with cepstrum prespeech buffer */
            if (is_speech != 0)
            {
                fe.vad_data.post_speech_frames = 0;
                if (fe.vad_data.in_speech == 0)
                {
                    fe.vad_data.pre_speech_frames++;
                    /* check for transition sil.Deref.speech */
                    if (fe.vad_data.pre_speech_frames >= fe.start_speech)
                    {
                        fe.vad_data.pre_speech_frames = 0;
                        fe.vad_data.in_speech = 1;
                    }
                }
            }
            else
            {
                fe.vad_data.pre_speech_frames = 0;
                if (fe.vad_data.in_speech != 0)
                {
                    fe.vad_data.post_speech_frames++;
                    /* check for transition speech.Deref.sil */
                    if (fe.vad_data.post_speech_frames >= fe.post_speech)
                    {
                        fe.vad_data.post_speech_frames = 0;
                        fe.vad_data.in_speech = 0;
                        FrontendPrespeechBuf.fe_prespch_reset_cep(fe.vad_data.prespch_buf);
                        FrontendPrespeechBuf.fe_prespch_reset_pcm(fe.vad_data.prespch_buf);
                    }
                }
            }
        }
    }
}
