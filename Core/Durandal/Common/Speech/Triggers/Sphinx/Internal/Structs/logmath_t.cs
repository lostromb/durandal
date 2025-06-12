using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class logmath_t
    {
        public logadd_t t;
        public double _base;
        public double log_of_base;
        public double log10_of_base;
        public double inv_log_of_base;
        public double inv_log10_of_base;
        public int zero;

        public logmath_t()
        {
            t = new logadd_t();
        }
    }
}
