using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class kws_detection_t
    {
        public Pointer<byte> keyphrase;
        public int sf;
        public int ef;
        public int prob;
        public int ascr;
    }
}
