using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class phone_loop_renorm_t
    {
        public int frame_idx;  /*< Frame of renormalization. */
        public int norm;     /*< Normalization constant. */
    }
}
