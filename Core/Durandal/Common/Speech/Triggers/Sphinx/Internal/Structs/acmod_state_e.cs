using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.Triggers.Sphinx.Internal.Structs
{
    internal static class acmod_state_e
    {
        public const int ACMOD_IDLE = 0;
        public const int ACMOD_STARTED = 1;
        public const int ACMOD_PROCESSING = 2;
        public const int ACMOD_ENDED = 3;
    }
}
