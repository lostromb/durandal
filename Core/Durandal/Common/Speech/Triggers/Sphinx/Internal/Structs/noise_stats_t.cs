using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class noise_stats_t
    {
        /* Smoothed power */
        public Pointer<double> power;
        /* Noise estimate */
        public Pointer<double> noise;
        /* Signal floor estimate */
        public Pointer<double> floor;
        /* Peak for temporal masking */
        public Pointer<double> peak;

        /* Initialize it next time */
        public byte undefined;
        /* Number of items to process */
        public uint num_filters;

        /* Sum of slow peaks for VAD */
        public double slow_peak_sum;

        /* Precomputed constants */
        public double lambda_power;
        public double comp_lambda_power;
        public double lambda_a;
        public double comp_lambda_a;
        public double lambda_b;
        public double comp_lambda_b;
        public double lambda_t;
        public double mu_t;
        public double max_gain;
        public double inv_max_gain;

        public Pointer<double> smooth_scaling = PointerHelpers.Malloc<double>(2 * FrontendNoise.SMOOTH_WINDOW + 3);
    }
}
