using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class CepstralMeanNormalizationLive
    {
		internal static void cmn_live_shiftwin(cmn_t cm)
		{
			float sf;
			int i;
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder logLine = pooledSb.Builder;
                logLine.Append("Update from < ");
                for (i = 0; i < cm.veclen; i++)
                {
                    logLine.AppendFormat("{0:F2}  ", (cm.cmn_mean[i]));
                }

                logLine.Append(">");
                cm.logger.E_INFO(logLine.ToString());

                sf = (1.0f) / cm.nframe;
                for (i = 0; i < cm.veclen; i++)
                {
                    cm.cmn_mean[i] = cm.sum[i] / cm.nframe; /* sum[i] * sf */
                }

                /* Make the accumulation decay exponentially */
                if (cm.nframe >= CepstralMeanNormalization.CMN_WIN_HWM)
                {
                    sf = CepstralMeanNormalization.CMN_WIN * sf;
                    for (i = 0; i < cm.veclen; i++)
                    {
                        cm.sum[i] = (cm.sum[i] * sf);
                    }

                    cm.nframe = CepstralMeanNormalization.CMN_WIN;
                }

                logLine.Clear();
                logLine.Append("Update to   < ");
                for (i = 0; i < cm.veclen; i++)
                {
                    logLine.AppendFormat("{0:F2}  ", (cm.cmn_mean[i]));
                }

                logLine.Append(">");
                cm.logger.E_INFO(logLine.ToString());
            }
		}

		internal static void cmn_live_update(cmn_t cm)
		{
			float sf;
			int i;

			if (cm.nframe <= 0)
				return;

            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                StringBuilder logLine = pooledSb.Builder;
                logLine.Append("Update from < ");
                for (i = 0; i < cm.veclen; i++)
                {
                    logLine.AppendFormat("{0:F2}  ", (cm.cmn_mean[i]));
                }

                logLine.Append(">");
                cm.logger.E_INFO(logLine.ToString());

                /* Update mean buffer */
                sf = (1.0f) / cm.nframe;
                for (i = 0; i < cm.veclen; i++)
                {
                    cm.cmn_mean[i] = cm.sum[i] / cm.nframe; /* sum[i] * sf; */
                }

                /* Make the accumulation decay exponentially */
                if (cm.nframe > CepstralMeanNormalization.CMN_WIN_HWM)
                {
                    sf = CepstralMeanNormalization.CMN_WIN * sf;
                    for (i = 0; i < cm.veclen; i++)
                    {
                        cm.sum[i] = (cm.sum[i] * sf);
                    }

                    cm.nframe = CepstralMeanNormalization.CMN_WIN;
                }

                logLine.Clear();
                logLine.Append("Update to   < ");
                for (i = 0; i < cm.veclen; i++)
                {
                    logLine.AppendFormat("{0:F2}  ", (cm.cmn_mean[i]));
                }

                logLine.Append(">");
                cm.logger.E_INFO(logLine.ToString());
            }
        }

		internal static void cmn_live_run(cmn_t cm, Pointer<Pointer<float>> incep, int varnorm, int nfr)
		{
			int i, j;

			if (nfr <= 0)
				return;

			if (varnorm != 0)
                cm.logger.E_FATAL
				("Variance normalization not implemented in live mode decode\n");

			for (i = 0; i < nfr; i++) {

				/* Skip zero energy frames */
				if (incep[i][0] < 0)
					continue;

				for (j = 0; j < cm.veclen; j++) {
					cm.sum[j] += incep[i][j];
                    incep[i].Set(j, incep[i][j] - cm.cmn_mean[j]);
				}

				++cm.nframe;
			}

			/* Shift buffer down if we have more than CMN_WIN_HWM frames */
			if (cm.nframe > CepstralMeanNormalization.CMN_WIN_HWM)
				cmn_live_shiftwin(cm);
		}

    }
}
