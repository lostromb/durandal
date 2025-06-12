using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.CPlusPlus
{
    internal static class SphinxAssert
    {
        [Conditional("DEBUG")]
        internal static void assert(bool condition)
        {
            Debug.Assert(condition);
        }
    }
}
