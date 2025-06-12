using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class arg_t
    {
        public Pointer<byte> name;
        public int type;
        public Pointer<byte> deflt;
        public Pointer<byte> doc;
    }
}
