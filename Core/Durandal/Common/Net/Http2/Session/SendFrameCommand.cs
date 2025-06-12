using Durandal.Common.Net.Http2.Frames;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Session
{
    /// <summary>
    /// This should become obsolete since we want to have strongly-typed semantic commands for each session operation rather
    /// than just writing raw frames everywhere.
    /// </summary>
    internal class SendFrameCommand : ISessionCommand
    {
        public SendFrameCommand(Http2Frame frame)
        {
            ToSend = frame;
        }

        public Http2Frame ToSend { get; private set; }
    }
}
