using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class cmn_t
    {
        public Pointer<float> cmn_mean;
        public Pointer<float> cmn_var;
        public Pointer<float> sum;
        public int nframe;
        public int veclen;
        public SphinxLogger logger;
    }
}
