using Durandal.Common.Net.Http2.Frames;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Net.Http2.Session
{
    internal class CloseConnectionCommand : ISessionCommand
    {
        public CloseConnectionCommand(Http2ErrorCode reason = Http2ErrorCode.NoError, string debugMessage = "")
        {
            Reason = reason;
            DebugMessage = debugMessage;
        }

        public Http2ErrorCode Reason { get; private set; }
        public string DebugMessage { get; private set; }
    }
}
