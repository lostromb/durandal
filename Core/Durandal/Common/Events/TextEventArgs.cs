namespace Durandal.Common.Events
{
    using System;

    public class TextEventArgs : EventArgs
    {
        public TextEventArgs(string text)
        {
            this.Text = text;
        }

        public string Text { get; set; }
    }
}
