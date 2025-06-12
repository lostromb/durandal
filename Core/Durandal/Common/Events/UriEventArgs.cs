namespace Durandal.Common.Events
{
    using System;

    public class UriEventArgs : EventArgs
    {
        public UriEventArgs(Uri url)
        {
            this.Url = url;
        }

        public Uri Url { get; set; }
    }
}
