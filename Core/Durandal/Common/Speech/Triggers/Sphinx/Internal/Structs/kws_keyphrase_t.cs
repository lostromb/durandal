using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class kws_keyphrase_t
    {
        public Pointer<byte> word;
        public int threshold;
        public hmm_t[] hmms;
        public int n_hmms;
    }
}
