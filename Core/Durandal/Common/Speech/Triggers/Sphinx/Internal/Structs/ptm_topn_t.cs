using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal struct ptm_topn_t
    {
        public int cw;    /*< Codeword index. */
        public int score; /*< Score. */
    }
}
