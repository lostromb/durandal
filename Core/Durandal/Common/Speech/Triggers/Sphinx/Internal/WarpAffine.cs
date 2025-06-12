using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class WarpAffine
    {
        public const int N_PARAM = 2;
        internal static Pointer<float> parameters = new Pointer<float>(new float[] { 1.0f, 0.0f });
        internal static int is_neutral = 1;
        internal static readonly Pointer<byte> p_str = PointerHelpers.Malloc<byte>(256);
        internal static float nyquist_frequency = 0.0f;

        internal static Pointer<byte> fe_warp_affine_doc()
        {
            return cstring.ToCString("affine :== < w' = a * x + b >");
        }

        internal static uint fe_warp_affine_id()
        {
            return Frontend.FE_WARP_ID_AFFINE;
        }

        internal static uint fe_warp_affine_n_param()
        {
            return N_PARAM;
        }

        internal static void fe_warp_affine_set_parameters(Pointer<byte> param_str, float sampling_rate, SphinxLogger logger)
        {
            Pointer<byte> tok;
            Pointer<byte> seps = cstring.ToCString(" \t");
            Pointer<byte> temp_param_str = PointerHelpers.Malloc<byte>(256);
            int param_index = 0;

            nyquist_frequency = sampling_rate / 2;
            if (param_str.IsNull)
            {
                is_neutral = 1;
                return;
            }
            /* The new parameters are the same as the current ones, so do nothing. */
            if (cstring.strcmp(param_str, p_str) == 0)
            {
                return;
            }
            is_neutral = 0;
            cstring.strcpy(temp_param_str, param_str);
            parameters.MemSet(0, N_PARAM);
            cstring.strcpy(p_str, param_str);
            /* FIXME: strtok() is not re-entrant... */
            tok = cstring.strtok(temp_param_str, seps);
            while (tok.IsNonNull)
            {
                parameters[param_index++] = (float)StringFuncs.atof_c(tok);
                tok = cstring.strtok(PointerHelpers.NULL<byte>(), seps);
                if (param_index >= N_PARAM)
                {
                    break;
                }
            }
            if (tok.IsNonNull)
            {
                logger.E_INFO(string.Format("Affine warping takes up to two arguments, {0} ignored.\n", cstring.FromCString(tok)));
            }
            if (parameters[0] == 0)
            {
                is_neutral = 1;
                logger.E_INFO
                    ("Affine warping cannot have slope zero, warping not applied.\n");
            }
        }

        internal static float fe_warp_affine_warped_to_unwarped(float nonlinear, SphinxLogger logger)
        {
            if (is_neutral != 0)
            {
                return nonlinear;
            }
            else
            {
                /* linear = (nonlinear - b) / a */
                float temp = nonlinear - parameters[1];
                temp /= parameters[0];
                if (temp > nyquist_frequency)
                {
                    logger.E_WARN
                        (string.Format("Warp factor {0} results in frequency ({1}) higher than Nyquist ({2})\n",
                         parameters[0], temp, nyquist_frequency));
                }
                return temp;
            }
        }

        internal static float fe_warp_affine_unwarped_to_warped(float linear)
        {
            if (is_neutral != 0)
            {
                return linear;
            }
            else
            {
                /* nonlinear = a * linear - b */
                float temp = linear * parameters[0];
                temp += parameters[1];
                return temp;
            }
        }

        internal static void fe_warp_affine_print(Pointer<byte> label, SphinxLogger logger)
        {
            uint i;

            for (i = 0; i < N_PARAM; i++)
            {
                logger.E_INFO(string.Format("{0}[{1}]: {2} ", cstring.FromCString(label), i, parameters[i]));
            }
        }
    }
}
