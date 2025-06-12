using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class ps_segfuncs_t
    {
        public seg_next_func seg_next;
        public seg_free_func seg_free;

        public delegate ps_seg_t seg_next_func(ps_seg_t seg);
        public delegate void seg_free_func(ps_seg_t seg);
    }
}
