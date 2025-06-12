using Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal
{
    internal static class Profiler
    {
        // FIXME reimplement this
        internal static void ptmr_start(ptmr_t timer)
        {
            timer.stopwatch.Start();
        }

        internal static void ptmr_stop(ptmr_t timer)
        {
            timer.stopwatch.Stop();
        }

        internal static void ptmr_reset(ptmr_t timer)
        {
            timer.stopwatch.Reset();
        }

        internal static void ptmr_init(ptmr_t timer)
        {
            timer.stopwatch.Reset();
        }
    }
}
