using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class ptm_fast_eval_t
    {
        public Pointer<Pointer<Pointer<ptm_topn_t>>> topn;     /*< Top-N for each codebook (mgau x feature x topn) */
        public Pointer<uint> mgau_active; /*< Set of active codebooks */
    }
}
