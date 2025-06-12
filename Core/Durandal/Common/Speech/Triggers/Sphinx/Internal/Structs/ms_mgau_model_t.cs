using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class ms_mgau_model_t : ps_mgau_t
    {
        public gauden_t g;   /*< The codebook */
        public senone_t s;   /*< The senone */
        public int topn;      /*< Top-n gaussian will be computed */

        /*< Intermediate used in computation */
        public Pointer<Pointer<Pointer<gauden_dist_t>>> dist;
        public Pointer<byte> mgau_active;
        public cmd_ln_t config;
    }
}
