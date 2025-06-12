using Durandal.Common.Net.Http2.Frames;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Session
{
    internal class SendDataFrameCommand : ISessionCommand
    {
        public SendDataFrameCommand(Http2DataFrame frame)
        {
            ToSend = frame;
        }

        public Http2DataFrame ToSend { get; private set; }
    }
}
