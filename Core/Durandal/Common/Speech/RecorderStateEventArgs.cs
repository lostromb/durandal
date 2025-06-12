using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Speech
{
    public class RecorderStateEventArgs : EventArgs
    {
        public RecorderStateEventArgs(RecorderState state)
        {
            this.State = state;
        }

        public RecorderState State { get; set; }
    }
}
