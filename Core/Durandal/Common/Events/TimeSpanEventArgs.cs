namespace Durandal.Common.Events
{
    using System;

    public class TimeSpanEventArgs : EventArgs
    {
        public TimeSpanEventArgs(TimeSpan time)
        {
            Time = time;
        }

        public TimeSpan Time { get; set; }
    }
}
