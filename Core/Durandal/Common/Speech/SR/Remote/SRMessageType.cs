using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Speech.SR.Remote
{
    public static class SRMessageType
    {
        public const ushort CLOSE_SOCKET = 0;
        public const ushort SR_START = 1;
        public const ushort SR_SEND_AUDIOHEADER = 2;
        public const ushort SR_SEND_AUDIO = 3;
        public const ushort SR_SEND_FINALAUDIO = 4;
        public const ushort SR_PARTIALRESULT = 5;
        public const ushort SR_FINALRESULT = 6;
    }
}
