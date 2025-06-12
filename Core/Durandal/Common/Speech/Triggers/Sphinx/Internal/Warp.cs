using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class Warp
    {
        public const int FE_WARP_ID_MAX = 2;

        internal static Pointer<Pointer<byte>> __name2id = new Pointer<Pointer<byte>>(new Pointer<byte>[]
        {
            cstring.ToCString("inverse"),
            cstring.ToCString("linear"),
            cstring.ToCString("piecewise"),
            PointerHelpers.NULL<byte>()
        });

        internal static Pointer<Pointer<byte>> name2id = new Pointer<Pointer<byte>>(new Pointer<byte>[]
        {
            cstring.ToCString("inverse_linear"),
            cstring.ToCString("affine"),
            cstring.ToCString("piecewise_linear"),
            PointerHelpers.NULL<byte>()
        });

        internal static Pointer<fe_warp_conf_t> fe_warp_conf = new Pointer<fe_warp_conf_t>(new fe_warp_conf_t[]
        {
            new fe_warp_conf_t()
            {
                set_parameters = WarpInverseLinear.fe_warp_inverse_linear_set_parameters,
                doc = WarpInverseLinear.fe_warp_inverse_linear_doc,
                id = WarpInverseLinear.fe_warp_inverse_linear_id,
                n_param = WarpInverseLinear.fe_warp_inverse_linear_n_param,
                warped_to_unwarped = WarpInverseLinear.fe_warp_inverse_linear_warped_to_unwarped,
                unwarped_to_warped = WarpInverseLinear.fe_warp_inverse_linear_unwarped_to_warped,
                print = WarpInverseLinear.fe_warp_inverse_linear_print
            },     /* Inverse linear warping */
            new fe_warp_conf_t()
            {
                set_parameters = WarpAffine.fe_warp_affine_set_parameters,
                doc = WarpAffine.fe_warp_affine_doc,
                id = WarpAffine.fe_warp_affine_id,
                n_param = WarpAffine.fe_warp_affine_n_param,
                warped_to_unwarped = WarpAffine.fe_warp_affine_warped_to_unwarped,
                unwarped_to_warped = WarpAffine.fe_warp_affine_unwarped_to_warped,
                print = WarpAffine.fe_warp_affine_print
            },     /* Affine warping */
            new fe_warp_conf_t()
            {
                set_parameters = WarpPiecewiseLinear.fe_warp_piecewise_linear_set_parameters,
                doc = WarpPiecewiseLinear.fe_warp_piecewise_linear_doc,
                id = WarpPiecewiseLinear.fe_warp_piecewise_linear_id,
                n_param = WarpPiecewiseLinear.fe_warp_piecewise_linear_n_param,
                warped_to_unwarped = WarpPiecewiseLinear.fe_warp_piecewise_linear_warped_to_unwarped,
                unwarped_to_warped = WarpPiecewiseLinear.fe_warp_piecewise_linear_unwarped_to_warped,
                print = WarpPiecewiseLinear.fe_warp_piecewise_linear_print
            },   /* Piecewise_Linear warping */
        });

        internal static int fe_warp_set(melfb_t mel, Pointer<byte> id_name, SphinxLogger logger)
        {
            uint i;

            for (i = 0; name2id[i].IsNonNull; i++)
            {
                if (cstring.strcmp(id_name, name2id[i]) == 0)
                {
                    mel.warp_id = i;
                    break;
                }
            }

            if (name2id[i].IsNull)
            {
                for (i = 0; __name2id[i].IsNonNull; i++)
                {
                    if (cstring.strcmp(id_name, __name2id[i]) == 0)
                    {
                        mel.warp_id = i;
                        break;
                    }
                }
                if (__name2id[i].IsNull)
                {
                    logger.E_ERROR(string.Format("Unimplemented warping function {0}\n", cstring.FromCString(id_name)));
                    logger.E_ERROR("Implemented functions are:\n");
                    for (i = 0; name2id[i].IsNonNull; i++)
                    {
                        logger.E_ERROR(string.Format("\t{0}\n", cstring.FromCString(name2id[i])));
                    }
                    mel.warp_id = Frontend.FE_WARP_ID_NONE;

                    return Frontend.FE_START_ERROR;
                }
            }

            return Frontend.FE_SUCCESS;
        }

        internal static void fe_warp_set_parameters(melfb_t mel, Pointer<byte> param_str, float sampling_rate, SphinxLogger logger)
        {
            if (mel.warp_id <= FE_WARP_ID_MAX)
            {
                fe_warp_conf[mel.warp_id].set_parameters(param_str, sampling_rate, logger);
            }
            else if (mel.warp_id == Frontend.FE_WARP_ID_NONE)
            {
                logger.E_FATAL("feat module must be configured w/ a valid ID\n");
            }
            else
            {
                logger.E_FATAL
                    (string.Format("fe_warp module misconfigured with invalid fe_warp_id {0}\n",
                     mel.warp_id));
            }
        }

        internal static float fe_warp_warped_to_unwarped(melfb_t mel, float nonlinear, SphinxLogger logger)
        {
            if (mel.warp_id <= FE_WARP_ID_MAX)
            {
                return fe_warp_conf[mel.warp_id].warped_to_unwarped(nonlinear, logger);
            }
            else if (mel.warp_id == Frontend.FE_WARP_ID_NONE)
            {
                logger.E_FATAL("fe_warp module must be configured w/ a valid ID\n");
            }
            else
            {
                logger.E_FATAL
                    (string.Format("fe_warp module misconfigured with invalid fe_warp_id {0}\n",
                     mel.warp_id));
            }

            return 0;
        }

        internal static float fe_warp_unwarped_to_warped(melfb_t mel, float linear, SphinxLogger logger)
        {
            if (mel.warp_id <= FE_WARP_ID_MAX)
            {
                return fe_warp_conf[mel.warp_id].unwarped_to_warped(linear);
            }
            else if (mel.warp_id == Frontend.FE_WARP_ID_NONE)
            {
                logger.E_FATAL("fe_warp module must be configured w/ a valid ID\n");
            }
            else
            {
                logger.E_FATAL
                    (string.Format("fe_warp module misconfigured with invalid fe_warp_id {0}\n",
                     mel.warp_id));
            }

            return 0;
        }
    }
}
