using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class LogMath
    {
        internal static logmath_t logmath_init(double _base, int shift, int use_table, SphinxLogger logger)
        {
            logmath_t lmath;
            uint maxyx, i;
            double byx;
            int width;

            /* Check that the base is correct. */
            if (_base <= 1.0)
            {
                logger.E_ERROR("Base must be greater than 1.0\n");
                return null;
            }

            /* Set up various necessary constants. */
            lmath = new logmath_t();
            lmath._base = _base;
            lmath.log_of_base = Math.Log(_base);
            lmath.log10_of_base = Math.Log10(_base);
            lmath.inv_log_of_base = 1.0 / lmath.log_of_base;
            lmath.inv_log10_of_base = 1.0 / lmath.log10_of_base;
            lmath.t.shift = checked((sbyte)shift);
            /* Shift this sufficiently that overflows can be avoided. */
            lmath.zero = int.MinValue >> (shift + 2);

            if (use_table == 0)
                return lmath;

            /* Create a logadd table with the appropriate width */
            maxyx = (uint)(Math.Log(2.0) / Math.Log(_base) + 0.5) >> shift;
            /* Poor man's log2 */
            if (maxyx < 256) width = 1;
            else if (maxyx < 65536) width = 2;
            else width = 4;

            lmath.t.width = checked((byte)width);
            /* Figure out size of add table required. */
            byx = 1.0; /* Maximum possible base^{y-x} value - note that this implies that y-x == 0 */
            for (i = 0; ; ++i)
            {
                double lobyx = Math.Log(1.0 + byx) * lmath.inv_log_of_base; /* log_{base}(1 + base^{y-x}); */
                int k = (int)(lobyx + 0.5 * (1 << shift)) >> shift; /* Round to shift */

                /* base^{y-x} has reached the smallest representable value. */
                if (k <= 0)
                    break;

                /* This table is indexed by -(y-x), so we multiply byx by
                    * base^{-1} here which is equivalent to subtracting one from
                    * (y-x). */
                byx /= _base;
            }
            i >>= shift;

            /* Never produce a table smaller than 256 entries. */
            if (i < 255) i = 255;

            if (width == 1)
            {
                lmath.t.table_uint8 = CKDAlloc.ckd_calloc<byte>(i + 1);
            }
            else if (width == 2)
            {
                lmath.t.table_uint16 = CKDAlloc.ckd_calloc<ushort>(i + 1);
            }
            else if (width == 4)
            {
                lmath.t.table_uint32 = CKDAlloc.ckd_calloc<uint>(i + 1);
            }

            lmath.t.table_size = i + 1;
            /* Create the add table (see above). */
            byx = 1.0;
            for (i = 0; ; ++i)
            {
                double lobyx = Math.Log(1.0 + byx) * lmath.inv_log_of_base;
                int k = (int)(lobyx + 0.5 * (1 << shift)) >> shift; /* Round to shift */
                uint prev = 0;

                /* Check any previous value - if there is a shift, we want to
                    * only store the highest one. */
                switch (width)
                {
                    case 1:
                        prev = (lmath.t.table_uint8)[i >> shift];
                        break;
                    case 2:
                        prev = (lmath.t.table_uint16)[i >> shift];
                        break;
                    case 4:
                        prev = (lmath.t.table_uint32)[i >> shift];
                        break;
                }
                if (prev == 0)
                {
                    switch (width)
                    {
                        case 1:
                            (lmath.t.table_uint8)[i >> shift] = (byte)k;
                            break;
                        case 2:
                            (lmath.t.table_uint16)[i >> shift] = (ushort)k;
                            break;
                        case 4:
                            (lmath.t.table_uint32)[i >> shift] = (uint)k;
                            break;
                    }
                }
                if (k <= 0)
                    break;

                /* Decay base^{y-x} exponentially according to base. */
                byx /= _base;
            }

            return lmath;
        }

        internal static double logmath_get_base(logmath_t lmath)
        {
            return lmath._base;
        }

        internal static int logmath_get_zero(logmath_t lmath)
        {
            return lmath.zero;
        }

        internal static int logmath_get_width(logmath_t lmath)
        {
            return lmath.t.width;
        }

        internal static int logmath_add(logmath_t lmath, int logb_x, int logb_y)
        {
            logadd_t t = lmath.t;
            int d, r;

            /* handle 0 + x = x case. */
            if (logb_x <= lmath.zero)
                return logb_y;
            if (logb_y <= lmath.zero)
                return logb_x;

            if ((t.width == 1 && t.table_uint8.IsNull) ||
                (t.width == 2 && t.table_uint16.IsNull) ||
                (t.width == 4 && t.table_uint32.IsNull))
                return logmath_add_exact(lmath, logb_x, logb_y);

            /* d must be positive, obviously. */
            if (logb_x > logb_y)
            {
                d = (logb_x - logb_y);
                r = logb_x;
            }
            else
            {
                d = (logb_y - logb_x);
                r = logb_y;
            }

            if (d < 0)
            {
                /* Some kind of overflow has occurred, fail gracefully. */
                return r;
            }
            if ((uint)d >= t.table_size)
            {
                /* If this happens, it's not actually an error, because the
                 * last entry in the logadd table is guaranteed to be zero.
                 * Therefore we just return the larger of the two values. */
                return r;
            }

            switch (t.width)
            {
                case 1:
                    return r + ((t.table_uint8)[d]);
                case 2:
                    return r + ((t.table_uint16)[d]);
                case 4:
                    return checked((int)(r + ((t.table_uint32)[d])));
            }
            return r;
        }

        internal static int logmath_add_exact(logmath_t lmath, int logb_p, int logb_q)
        {
            return logmath_log(lmath,
                               logmath_exp(lmath, logb_p)
                               + logmath_exp(lmath, logb_q));
        }

        internal static int logmath_log(logmath_t lmath, double p)
        {
            if (p <= 0)
            {
                return lmath.zero;
            }
            return (int)(Math.Log(p) * lmath.inv_log_of_base) >> lmath.t.shift;
        }

        internal static double logmath_exp(logmath_t lmath, int logb_p)
        {
            return Math.Pow(lmath._base, (double)(logb_p << lmath.t.shift));
        }

        internal static int logmath_ln_to_log(logmath_t lmath, double log_p)
        {
            return (int)(log_p * lmath.inv_log_of_base) >> lmath.t.shift;
        }
    }
}
