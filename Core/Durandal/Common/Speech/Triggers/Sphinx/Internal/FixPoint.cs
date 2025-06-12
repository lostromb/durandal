using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class FixPoint
    {
        public const int DEFAULT_RADIX = 12;

        internal static int FLOAT2FIX_ANY(double x, int radix)
        {
			return (((x) < 0.0) ?
				((int)((x) * (double)(1 << (radix)) - 0.5))
				: ((int)((x) * (double)(1 << (radix)) + 0.5)));
        }

        internal static int FLOAT2FIX(double x)
        {
            return FLOAT2FIX_ANY(x, DEFAULT_RADIX);
        }

        internal static double FIX2FLOAT_ANY(int x, int radix)
        {
            return ((double)(x) / (1 << (radix)));
        }

        internal static double FIX2FLOAT(int x)
        {
            return FIX2FLOAT_ANY(x, DEFAULT_RADIX);
        }
    }
}
