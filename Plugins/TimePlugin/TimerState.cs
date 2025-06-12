using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Plugins.Time
{
    public class TimerState
    {
        public bool Valid;
        public long TargetTimeEpoch;
        public long MsOnTimer;
        public bool IsPaused;
        public bool IsRunning;
        public bool IsElapsed;
        public bool CountsDown;
    }
}
