using Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class gnode_t
    {
        public object data;     /* See prim_type.h */
        public Pointer<gnode_t> next;	/* Next node in list */
    }
}
