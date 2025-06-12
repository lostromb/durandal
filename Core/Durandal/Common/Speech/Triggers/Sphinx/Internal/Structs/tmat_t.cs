using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class tmat_t
    {
        public Pointer<Pointer<Pointer<byte>>> tp;
        public short n_tmat;
        public short n_state;
    }
}
