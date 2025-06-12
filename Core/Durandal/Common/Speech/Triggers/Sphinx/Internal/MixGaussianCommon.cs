using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class MixGaussianCommon
    {
        internal static readonly Pointer<byte> MGAU_MIXW_VERSION = cstring.ToCString("1.0");   /* Sphinx-3 file format version for mixw */
        internal static readonly Pointer<byte> MGAU_PARAM_VERSION = cstring.ToCString("1.0");   /* Sphinx-3 file format version for mean/var */
        public const int NONE = -1;
        public const int WORST_DIST = unchecked((int)0x80000000);
        public const int MAX_NEG_MIXW = 159;
        public const int MAX_NEG_ASCR = 96;

        internal static int fast_logmath_add(logmath_t lmath, int mlx, int mly)
        {
            logadd_t t = lmath.t;
            int d, r;

            /* d must be positive, obviously. */
            if (mlx > mly)
            {
                d = (mlx - mly);
                r = mly;
            }
            else
            {
                d = (mly - mlx);
                r = mlx;
            }

            return r - ((t.table_uint8)[d]);
        }
    }
}
