using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class AcousticGainControl
    {
        internal static Pointer<Pointer<byte>> agc_type_str = new Pointer<Pointer<byte>>(
            new Pointer<byte>[]
            {
                cstring.ToCString("none"),
                cstring.ToCString("max"),
                cstring.ToCString("emax"),
                cstring.ToCString("noise")
            });

        public const int n_agc_type_str = 4;

        // fixme use agc_type_e enum here
        internal static int agc_type_from_str(Pointer<byte> str, SphinxLogger logger)
        {
            int i;

            for (i = 0; i<n_agc_type_str; ++i) {
                if (0 == cstring.strcmp(str, agc_type_str[i]))
                    return i;
            }
            logger.E_FATAL(string.Format("Unknown AGC type '{0}'\n", cstring.FromCString(str)));
            return agc_type_e.AGC_NONE;
        }

        internal static agc_t agc_init(SphinxLogger logger)
        {
            agc_t agc = new agc_t();
            agc.logger = logger;
            agc.noise_thresh = (2.0f);

            return agc;
        }

        /**
         * Normalize c0 for all frames such that max(c0) = 0.
         */
        internal static void agc_max(agc_t agc, Pointer<Pointer<float>> mfc, int n_frame)
        {
            int i;

            if (n_frame <= 0)
                return;
            agc.obs_max = mfc[0][0];
            for (i = 1; i < n_frame; i++)
            {
                if (mfc[i][0] > agc.obs_max)
                {
                    agc.obs_max = mfc[i][0];
                    agc.obs_frame = 1;
                }
            }

            agc.logger.E_INFO(string.Format("AGCMax: obs=max= {0}\n", agc.obs_max));
            for (i = 0; i < n_frame; i++)
            {
                mfc[i].Set(0, mfc[i][0] - agc.obs_max);
            }
        }

        internal static void agc_emax_set(agc_t agc, float m)
        {
            agc.max = (m);
            agc.logger.E_INFO(string.Format("AGCEMax: max= {0}\n", m));
        }

        internal static float agc_emax_get(agc_t agc)
        {
            return (agc.max);
        }

        internal static void agc_emax(agc_t agc, Pointer<Pointer<float>> mfc, int n_frame)
        {
            int i;

            if (n_frame <= 0)
                return;
            for (i = 0; i < n_frame; ++i)
            {
                if (mfc[i][0] > agc.obs_max)
                {
                    agc.obs_max = mfc[i][0];
                    agc.obs_frame = 1;
                }

                mfc[i].Set(0, mfc[i][0] - agc.max);
            }
        }

        /* Update estimated max for next utterance */
        internal static void agc_emax_update(agc_t agc)
        {
            if (agc.obs_frame != 0)
            {            /* Update only if some data observed */
                agc.obs_max_sum += agc.obs_max;
                agc.obs_utt++;

                /* Re-estimate max over past history; decay the history */
                agc.max = agc.obs_max_sum / agc.obs_utt;
                if (agc.obs_utt == 16)
                {
                    agc.obs_max_sum /= 2;
                    agc.obs_utt = 8;
                }
            }

            agc.logger.E_INFO(string.Format("AGCEMax: obs= {0}, new= {1}\n", agc.obs_max, agc.max));

            /* Reset the accumulators for the next utterance. */
            agc.obs_frame = 0;
            agc.obs_max = -1000.0f; /* Less than any real C0 value (hopefully!!) */
        }

        internal static void agc_noise(agc_t agc,
                  Pointer<Pointer<float>> cep,
                  int nfr)
        {
            float min_energy; /* Minimum log-energy */
            float noise_level;        /* Average noise_level */
            int i;           /* frame index */
            int noise_frames;        /* Number of noise frames */

            /* Determine minimum log-energy in utterance */
            min_energy = cep[0][0];
            for (i = 0; i < nfr; ++i)
            {
                if (cep[i][0] < min_energy)
                    min_energy = cep[i][0];
            }

            /* Average all frames between min_energy and min_energy + agc.noise_thresh */
            noise_frames = 0;
            noise_level = 0;
            min_energy += agc.noise_thresh;
            for (i = 0; i < nfr; ++i)
            {
                if (cep[i][0] < min_energy)
                {
                    noise_level += cep[i][0];
                    noise_frames++;
                }
            }

            if (noise_frames > 0)
            {
                noise_level /= noise_frames;
                agc.logger.E_INFO(string.Format("AGC NOISE: max= {0}\n", noise_level));
                /* Subtract noise_level from all log_energy values */
                for (i = 0; i < nfr; i++)
                {
                    cep[i].Set(0, cep[i][0] - noise_level);
                }
            }
        }

        internal static void agc_set_threshold(agc_t agc, float threshold)
        {
            agc.noise_thresh = threshold;
        }

        internal static float agc_get_threshold(agc_t agc)
        {
            return agc.noise_thresh;
        }
    }
}
