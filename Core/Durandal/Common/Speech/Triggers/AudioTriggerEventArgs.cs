using Durandal.Common.Time;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Speech.Triggers
{
    public class AudioTriggerEventArgs : EventArgs
    {
        public AudioTriggerResult AudioTriggerResult { get; set; }
        public IRealTimeProvider ThreadLocalTime { get; set; }

        public AudioTriggerEventArgs(AudioTriggerResult result, IRealTimeProvider realTime)
        {
            AudioTriggerResult = result;
            ThreadLocalTime = realTime;
        }
    }
}
