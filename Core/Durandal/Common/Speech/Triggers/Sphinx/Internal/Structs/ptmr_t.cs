using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal class ptmr_t
    {
        public Stopwatch stopwatch = new Stopwatch();
        public string name = string.Empty;
    }
}
