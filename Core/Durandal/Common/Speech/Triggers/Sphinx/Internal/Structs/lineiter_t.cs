using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class lineiter_t
    {
        public Pointer<byte> buf;
        public FILE fh;
        public int bsiz;
        public int len;
        public int clean;
        public int lineno;
    }
}
