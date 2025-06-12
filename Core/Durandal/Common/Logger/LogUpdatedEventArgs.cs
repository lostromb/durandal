namespace Durandal.Common.Logger
{
    using System;

    public class LogUpdatedEventArgs : EventArgs
    {
        public LogUpdatedEventArgs(LogEvent item)
        {
            this.LogEvent = item;
        }

        public LogEvent LogEvent { get; private set; }
    }
}
