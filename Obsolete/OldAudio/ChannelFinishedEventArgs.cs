using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Durandal.Common.Audio
{
    public class ChannelFinishedEventArgs : EventArgs
    {
        public object ChannelToken { get; set;}
        public bool WasStream { get; set; }
        public IRealTimeProvider ThreadLocalTime { get; set; }

        public ChannelFinishedEventArgs(object channelToken, bool wasStream, IRealTimeProvider realTime)
        {
            ChannelToken = channelToken;
            WasStream = wasStream;
            ThreadLocalTime = realTime;
        }
    }
}
